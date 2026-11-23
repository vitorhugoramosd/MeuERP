using System.Data;
using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;

namespace MeuERP.Servicos;

/// <summary>Vendas e orçamentos: gravação, finalização (estoque + financeiro) e cancelamento.</summary>
public static class VendaService
{
    private const string SelectVenda = @"
        SELECT v.*, COALESCE(c.nome, 'Consumidor final') AS cliente_nome, COALESCE(u.nome, '') AS usuario_nome
        FROM vendas v
        LEFT JOIN clientes c ON c.id = v.cliente_id
        LEFT JOIN usuarios u ON u.id = v.usuario_id ";

    private const string SelectItens = @"
        SELECT i.*, p.codigo AS produto_codigo, p.descricao AS produto_descricao, p.unidade AS unidade
        FROM venda_itens i JOIN produtos p ON p.id = i.produto_id
        WHERE i.venda_id = @id
        ORDER BY i.id";

    /// <summary>Lista as vendas do período (sem itens), mais recentes primeiro. Busca por nome do cliente ou número da venda.</summary>
    public static List<Venda> Listar(DateTime de, DateTime ate, string? status = null, string? busca = null)
    {
        string b = busca?.Trim() ?? "";
        int? numero = int.TryParse(b.TrimStart('#'), out int n) ? n : null;
        using var c = Database.Abrir();
        return c.Query<Venda>(SelectVenda + @"
            WHERE v.data_venda >= @de AND v.data_venda < @ate
              AND (@st = '' OR v.status = @st)
              AND (@b = '' OR COALESCE(c.nome, 'Consumidor final') LIKE @l OR c.nome_fantasia LIKE @l
                   OR (@num IS NOT NULL AND v.id = @num))
            ORDER BY v.data_venda DESC, v.id DESC",
            new { de = de.Date, ate = ate.Date.AddDays(1), st = status ?? "", b, l = $"%{b}%", num = numero }).ToList();
    }

    /// <summary>Venda completa, com itens.</summary>
    public static Venda? Obter(int id)
    {
        using var c = Database.Abrir();
        var v = c.QueryFirstOrDefault<Venda>(SelectVenda + "WHERE v.id = @id", new { id });
        if (v != null) v.Itens = c.Query<VendaItem>(SelectItens, new { id }).ToList();
        return v;
    }

    /// <summary>Insere ou atualiza um orçamento (não mexe em estoque nem financeiro). Devolve o id.</summary>
    public static int SalvarOrcamento(Venda v)
    {
        int idOriginal = v.Id;
        try
        {
            return Database.EmTransacao((c, tx) =>
            {
                Preparar(c, tx, v);
                Gravar(c, tx, v, StatusVenda.Orcamento, DateTime.Now, atualizarData: false);
                v.Status = StatusVenda.Orcamento;
                return v.Id;
            });
        }
        catch
        {
            v.Id = idOriginal;
            throw;
        }
    }

    /// <summary>
    /// Grava (venda nova ou orçamento existente) e finaliza numa única transação: baixa o estoque e gera os títulos a receber.
    /// Devolve o id da venda.
    /// </summary>
    public static int Finalizar(Venda v, DateTime? primeiroVencimento = null)
    {
        int idOriginal = v.Id;
        string statusOriginal = v.Status;
        try
        {
            return Database.EmTransacao((c, tx) =>
            {
                var cliente = Preparar(c, tx, v);
                bool aVista = FormasPagamento.EhAVista(v.FormaPagamento);

                if ((v.FormaPagamento is FormasPagamento.Crediario or FormasPagamento.Boleto) && cliente == null)
                    throw new RegraException($"Para vender no {v.FormaPagamento.ToLower()} é obrigatório informar o cliente.");

                if (!aVista && cliente != null && cliente.LimiteCredito > 0 && v.Total > 0)
                {
                    decimal saldo = ClienteRepository.SaldoDevedor(cliente.Id);
                    decimal disponivel = Math.Max(0, cliente.LimiteCredito - saldo);
                    if (saldo + v.Total > cliente.LimiteCredito)
                        throw new RegraException(
                            $"O limite de crédito de \"{cliente.Nome}\" não permite esta venda a prazo.\n\n" +
                            $"Limite de crédito: {Formato.Moeda(cliente.LimiteCredito)}\n" +
                            $"Saldo devedor: {Formato.Moeda(saldo)}\n" +
                            $"Disponível: {Formato.Moeda(disponivel)}\n" +
                            $"Total da venda: {Formato.Moeda(v.Total)}");
                }

                DateTime agora = DateTime.Now;
                Gravar(c, tx, v, StatusVenda.Finalizada, agora, atualizarData: true);
                v.Status = StatusVenda.Finalizada;
                v.DataVenda = agora;

                foreach (var i in v.Itens)
                    EstoqueService.Movimentar(c, tx, i.ProdutoId, TipoMov.Saida, i.Quantidade, "VENDA", v.Id, $"Venda nº {v.Id}");

                string descricao = $"Venda nº {v.Id}";
                if (aVista)
                    FinanceiroService.GerarParcelas(c, tx, TipoTitulo.Receber, descricao, v.ClienteId, v.Id, v.Total, 1,
                        DateTime.Today, v.FormaPagamento, quitado: true);
                else
                    FinanceiroService.GerarParcelas(c, tx, TipoTitulo.Receber, descricao, v.ClienteId, v.Id, v.Total, v.Parcelas,
                        (primeiroVencimento ?? DateTime.Today.AddDays(30)).Date, v.FormaPagamento, quitado: false);

                return v.Id;
            });
        }
        catch
        {
            // nada foi gravado: devolve o objeto ao estado anterior para permitir nova tentativa
            v.Id = idOriginal;
            v.Status = statusOriginal;
            throw;
        }
    }

    /// <summary>Cancela um orçamento ou uma venda finalizada (devolve o estoque e cancela os títulos).</summary>
    public static void Cancelar(int id)
    {
        Database.EmTransacao((c, tx) =>
        {
            var v = c.QueryFirstOrDefault<Venda>("SELECT * FROM vendas WHERE id = @id", new { id }, tx)
                    ?? throw new RegraException("Venda não encontrada.");
            switch (v.Status)
            {
                case StatusVenda.Cancelada:
                    throw new RegraException($"A venda nº {id} já está cancelada.");

                case StatusVenda.Orcamento:
                    break;

                case StatusVenda.Finalizada:
                    if (!Sessao.PodeGerenciar)
                        throw new RegraException("Somente gerente ou administrador pode cancelar uma venda finalizada.");
                    var itens = c.Query<VendaItem>("SELECT * FROM venda_itens WHERE venda_id = @id ORDER BY id", new { id }, tx).ToList();
                    foreach (var i in itens)
                        EstoqueService.Movimentar(c, tx, i.ProdutoId, TipoMov.Entrada, i.Quantidade, "CANCELAMENTO_VENDA", id,
                            $"Cancelamento da venda nº {id}");
                    FinanceiroService.CancelarPorOrigem(c, tx, TipoTitulo.Receber, id);
                    break;

                default:
                    throw new RegraException("Situação da venda inválida para cancelamento.");
            }
            c.Execute("UPDATE vendas SET status = @st WHERE id = @id", new { id, st = StatusVenda.Cancelada }, tx);
        });
    }

    // ---------- auxiliares ----------

    /// <summary>Valida e recalcula itens e totais. Devolve o cliente (ou null para consumidor final).</summary>
    private static Cliente? Preparar(IDbConnection c, IDbTransaction tx, Venda v)
    {
        if (v.Itens == null || v.Itens.Count == 0) throw new RegraException("Inclua pelo menos um item na venda.");
        if (v.Itens.GroupBy(i => i.ProdutoId).Any(g => g.Count() > 1))
            throw new RegraException("O mesmo produto aparece mais de uma vez na venda. Some as quantidades em um único item.");

        decimal subtotal = 0;
        foreach (var i in v.Itens)
        {
            var p = c.QueryFirstOrDefault<Produto>("SELECT * FROM produtos WHERE id = @id", new { id = i.ProdutoId }, tx)
                    ?? throw new RegraException($"Produto não encontrado (id {i.ProdutoId}).");
            if (i.Quantidade <= 0) throw new RegraException($"A quantidade de \"{p.Descricao}\" deve ser maior que zero.");
            if (i.PrecoUnitario < 0) throw new RegraException($"O preço de \"{p.Descricao}\" não pode ser negativo.");
            if (i.Desconto < 0) throw new RegraException($"O desconto de \"{p.Descricao}\" não pode ser negativo.");
            if (i.Desconto > i.Quantidade * i.PrecoUnitario)
                throw new RegraException($"O desconto de \"{p.Descricao}\" não pode ser maior que o valor do item.");

            i.Total = Formato.Arredondar(i.Quantidade * i.PrecoUnitario - i.Desconto);
            i.CustoUnitario = p.PrecoCusto;
            i.ProdutoCodigo = p.Codigo;
            i.ProdutoDescricao = p.Descricao;
            i.Unidade = p.Unidade;
            subtotal += i.Total;
        }

        v.Subtotal = Formato.Arredondar(subtotal);
        if (v.Desconto < 0) throw new RegraException("O desconto geral não pode ser negativo.");
        if (v.Desconto > v.Subtotal) throw new RegraException("O desconto geral não pode ser maior que o subtotal.");
        v.Total = Formato.Arredondar(v.Subtotal - v.Desconto);

        v.FormaPagamento = v.FormaPagamento?.Trim() ?? "";
        if (!FormasPagamento.Todas.Contains(v.FormaPagamento)) throw new RegraException("Forma de pagamento inválida.");
        if (FormasPagamento.EhAVista(v.FormaPagamento)) v.Parcelas = 1;
        else if (v.Parcelas < 1 || v.Parcelas > 24) throw new RegraException("O número de parcelas deve ser entre 1 e 24.");

        v.Observacoes = v.Observacoes?.Trim() ?? "";

        if (v.ClienteId is null or <= 0) { v.ClienteId = null; return null; }
        return c.QueryFirstOrDefault<Cliente>("SELECT * FROM clientes WHERE id = @id", new { id = v.ClienteId }, tx)
               ?? throw new RegraException("Cliente não encontrado.");
    }

    /// <summary>Insere a venda (ou atualiza o orçamento existente) e regrava os itens.</summary>
    private static void Gravar(IDbConnection c, IDbTransaction tx, Venda v, string status, DateTime data, bool atualizarData)
    {
        var p = new
        {
            v.Id, v.ClienteId, usuario = Sessao.UsuarioId, data, status, v.Subtotal, v.Desconto, v.Total,
            v.FormaPagamento, v.Parcelas, v.Observacoes, atualizar = atualizarData
        };

        if (v.Id <= 0)
        {
            v.Id = c.ExecuteScalar<int>(@"INSERT INTO vendas
                (cliente_id, usuario_id, data_venda, status, subtotal, desconto, total, forma_pagamento, parcelas, observacoes)
                VALUES (@ClienteId, @usuario, @data, @status, @Subtotal, @Desconto, @Total, @FormaPagamento, @Parcelas, @Observacoes);
                SELECT last_insert_rowid();", p, tx);
        }
        else
        {
            string? atual = c.ExecuteScalar<string?>("SELECT status FROM vendas WHERE id = @Id", new { v.Id }, tx);
            if (atual == null) throw new RegraException("Venda não encontrada.");
            if (atual != StatusVenda.Orcamento)
                throw new RegraException($"A venda nº {v.Id} está {TextoStatus(atual)} e não pode mais ser alterada.");

            c.Execute(@"UPDATE vendas SET cliente_id = @ClienteId, usuario_id = @usuario,
                data_venda = CASE WHEN @atualizar = 1 THEN @data ELSE data_venda END,
                status = @status, subtotal = @Subtotal, desconto = @Desconto, total = @Total,
                forma_pagamento = @FormaPagamento, parcelas = @Parcelas, observacoes = @Observacoes
                WHERE id = @Id", p, tx);
            c.Execute("DELETE FROM venda_itens WHERE venda_id = @Id", new { v.Id }, tx);
        }

        foreach (var i in v.Itens)
        {
            i.VendaId = v.Id;
            c.Execute(@"INSERT INTO venda_itens (venda_id, produto_id, quantidade, preco_unitario, desconto, total, custo_unitario)
                VALUES (@VendaId, @ProdutoId, @Quantidade, @PrecoUnitario, @Desconto, @Total, @CustoUnitario)", i, tx);
        }
    }

    private static string TextoStatus(string status) => UI.Tema.TextoStatus(status).ToLower();
}
