using System.Data;
using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;

namespace MeuERP.Servicos;

/// <summary>Pedidos de compra: gravação, recebimento (estoque + contas a pagar) e cancelamento.</summary>
public static class CompraService
{
    private const string SelectCompra = @"
        SELECT co.*, COALESCE(f.nome, '') AS fornecedor_nome, COALESCE(u.nome, '') AS usuario_nome
        FROM compras co
        LEFT JOIN fornecedores f ON f.id = co.fornecedor_id
        LEFT JOIN usuarios u ON u.id = co.usuario_id ";

    private const string SelectItens = @"
        SELECT i.*, p.codigo AS produto_codigo, p.descricao AS produto_descricao, p.unidade AS unidade
        FROM compra_itens i JOIN produtos p ON p.id = i.produto_id
        WHERE i.compra_id = @id
        ORDER BY i.id";

    /// <summary>Compras do período (sem itens), mais recentes primeiro. Busca por fornecedor, NF ou número.</summary>
    public static List<Compra> Listar(DateTime de, DateTime ate, string? status = null, string? busca = null)
    {
        string b = busca?.Trim() ?? "";
        int? numero = int.TryParse(b.TrimStart('#'), out int n) ? n : null;
        using var c = Database.Abrir();
        return c.Query<Compra>(SelectCompra + @"
            WHERE co.data_compra >= @de AND co.data_compra < @ate
              AND (@st = '' OR co.status = @st)
              AND (@b = '' OR f.nome LIKE @l OR f.nome_fantasia LIKE @l OR co.numero_nf LIKE @l
                   OR (@num IS NOT NULL AND co.id = @num))
            ORDER BY co.data_compra DESC, co.id DESC",
            new { de = de.Date, ate = ate.Date.AddDays(1), st = status ?? "", b, l = $"%{b}%", num = numero }).ToList();
    }

    /// <summary>Compra completa, com itens.</summary>
    public static Compra? Obter(int id)
    {
        using var c = Database.Abrir();
        var co = c.QueryFirstOrDefault<Compra>(SelectCompra + "WHERE co.id = @id", new { id });
        if (co != null) co.Itens = c.Query<CompraItem>(SelectItens, new { id }).ToList();
        return co;
    }

    /// <summary>Insere ou atualiza um pedido de compra (somente status PEDIDO). Devolve o id.</summary>
    public static int SalvarPedido(Compra compra)
    {
        int idOriginal = compra.Id;
        try
        {
            return Database.EmTransacao((c, tx) =>
            {
                Preparar(c, tx, compra);
                var p = new
                {
                    compra.Id, compra.FornecedorId, usuario = Sessao.UsuarioId, data = DateTime.Now, compra.NumeroNf,
                    compra.Subtotal, compra.Desconto, compra.Frete, compra.Total, compra.FormaPagamento, compra.Parcelas,
                    compra.Observacoes, status = StatusCompra.Pedido
                };

                if (compra.Id <= 0)
                {
                    compra.Id = c.ExecuteScalar<int>(@"INSERT INTO compras
                        (fornecedor_id, usuario_id, data_compra, status, numero_nf, subtotal, desconto, frete, total, forma_pagamento, parcelas, observacoes)
                        VALUES (@FornecedorId, @usuario, @data, @status, @NumeroNf, @Subtotal, @Desconto, @Frete, @Total, @FormaPagamento, @Parcelas, @Observacoes);
                        SELECT last_insert_rowid();", p, tx);
                }
                else
                {
                    string? atual = c.ExecuteScalar<string?>("SELECT status FROM compras WHERE id = @Id", new { compra.Id }, tx);
                    if (atual == null) throw new RegraException("Compra não encontrada.");
                    if (atual != StatusCompra.Pedido)
                        throw new RegraException($"A compra nº {compra.Id} está {UI.Tema.TextoStatus(atual).ToLower()} e não pode mais ser alterada.");
                    c.Execute(@"UPDATE compras SET fornecedor_id = @FornecedorId, usuario_id = @usuario, numero_nf = @NumeroNf,
                        subtotal = @Subtotal, desconto = @Desconto, frete = @Frete, total = @Total,
                        forma_pagamento = @FormaPagamento, parcelas = @Parcelas, observacoes = @Observacoes
                        WHERE id = @Id", p, tx);
                    c.Execute("DELETE FROM compra_itens WHERE compra_id = @Id", new { compra.Id }, tx);
                }

                foreach (var i in compra.Itens)
                {
                    i.CompraId = compra.Id;
                    c.Execute(@"INSERT INTO compra_itens (compra_id, produto_id, quantidade, custo_unitario, total)
                        VALUES (@CompraId, @ProdutoId, @Quantidade, @CustoUnitario, @Total)", i, tx);
                }
                compra.Status = StatusCompra.Pedido;
                return compra.Id;
            });
        }
        catch
        {
            compra.Id = idOriginal;
            throw;
        }
    }

    /// <summary>Recebe a mercadoria de um pedido: entrada no estoque (atualizando o custo médio) e títulos a pagar.</summary>
    public static void Receber(int id, DateTime? primeiroVencimento = null)
    {
        Database.EmTransacao((c, tx) =>
        {
            var compra = c.QueryFirstOrDefault<Compra>("SELECT * FROM compras WHERE id = @id", new { id }, tx)
                         ?? throw new RegraException("Compra não encontrada.");
            if (compra.Status == StatusCompra.Recebida) throw new RegraException($"A compra nº {id} já foi recebida.");
            if (compra.Status != StatusCompra.Pedido) throw new RegraException("Somente pedidos de compra podem ser recebidos.");

            var itens = c.Query<CompraItem>("SELECT * FROM compra_itens WHERE compra_id = @id ORDER BY id", new { id }, tx).ToList();
            if (itens.Count == 0) throw new RegraException("O pedido não possui itens.");

            foreach (var i in itens)
                EstoqueService.Movimentar(c, tx, i.ProdutoId, TipoMov.Entrada, i.Quantidade, "COMPRA", id, $"Compra nº {id}",
                    custoUnitario: i.CustoUnitario);

            c.Execute("UPDATE compras SET status = @st, data_recebimento = @agora WHERE id = @id",
                new { id, st = StatusCompra.Recebida, agora = DateTime.Now }, tx);

            string nf = compra.NumeroNf?.Trim() ?? "";
            string descricao = $"Compra nº {id}" + (nf.Length > 0 ? " - NF " + nf : "");
            if (FormasPagamento.EhAVista(compra.FormaPagamento))
                FinanceiroService.GerarParcelas(c, tx, TipoTitulo.Pagar, descricao, compra.FornecedorId, id, compra.Total, 1,
                    DateTime.Today, compra.FormaPagamento, quitado: true);
            else
                FinanceiroService.GerarParcelas(c, tx, TipoTitulo.Pagar, descricao, compra.FornecedorId, id, compra.Total,
                    Math.Clamp(compra.Parcelas, 1, 24), (primeiroVencimento ?? DateTime.Today.AddDays(30)).Date,
                    compra.FormaPagamento, quitado: false);
        });
    }

    /// <summary>Cancela um pedido, ou uma compra recebida (retira do estoque e cancela os títulos a pagar).</summary>
    public static void Cancelar(int id)
    {
        Database.EmTransacao((c, tx) =>
        {
            var compra = c.QueryFirstOrDefault<Compra>("SELECT * FROM compras WHERE id = @id", new { id }, tx)
                         ?? throw new RegraException("Compra não encontrada.");
            switch (compra.Status)
            {
                case StatusCompra.Cancelada:
                    throw new RegraException($"A compra nº {id} já está cancelada.");

                case StatusCompra.Pedido:
                    break;

                case StatusCompra.Recebida:
                    var itens = c.Query<CompraItem>("SELECT * FROM compra_itens WHERE compra_id = @id ORDER BY id", new { id }, tx).ToList();
                    foreach (var i in itens)
                        EstoqueService.Movimentar(c, tx, i.ProdutoId, TipoMov.Saida, i.Quantidade, "CANCELAMENTO_COMPRA", id,
                            $"Cancelamento da compra nº {id}");
                    FinanceiroService.CancelarPorOrigem(c, tx, TipoTitulo.Pagar, id);
                    break;

                default:
                    throw new RegraException("Situação da compra inválida para cancelamento.");
            }
            c.Execute("UPDATE compras SET status = @st WHERE id = @id", new { id, st = StatusCompra.Cancelada }, tx);
        });
    }

    /// <summary>Valida e recalcula itens e totais.</summary>
    private static void Preparar(IDbConnection c, IDbTransaction tx, Compra compra)
    {
        if (compra.FornecedorId <= 0) throw new RegraException("Selecione o fornecedor.");
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM fornecedores WHERE id = @id", new { id = compra.FornecedorId }, tx) == 0)
            throw new RegraException("Fornecedor não encontrado.");
        if (compra.Itens == null || compra.Itens.Count == 0) throw new RegraException("Inclua pelo menos um item na compra.");

        decimal subtotal = 0;
        foreach (var i in compra.Itens)
        {
            var p = c.QueryFirstOrDefault<Produto>("SELECT * FROM produtos WHERE id = @id", new { id = i.ProdutoId }, tx)
                    ?? throw new RegraException($"Produto não encontrado (id {i.ProdutoId}).");
            if (i.Quantidade <= 0) throw new RegraException($"A quantidade de \"{p.Descricao}\" deve ser maior que zero.");
            if (i.CustoUnitario < 0) throw new RegraException($"O custo de \"{p.Descricao}\" não pode ser negativo.");
            i.Total = Formato.Arredondar(i.Quantidade * i.CustoUnitario);
            i.ProdutoCodigo = p.Codigo;
            i.ProdutoDescricao = p.Descricao;
            i.Unidade = p.Unidade;
            subtotal += i.Total;
        }

        compra.Subtotal = Formato.Arredondar(subtotal);
        if (compra.Desconto < 0) throw new RegraException("O desconto não pode ser negativo.");
        if (compra.Frete < 0) throw new RegraException("O frete não pode ser negativo.");
        if (compra.Desconto > compra.Subtotal) throw new RegraException("O desconto não pode ser maior que o subtotal.");
        compra.Total = Formato.Arredondar(compra.Subtotal - compra.Desconto + compra.Frete);

        compra.FormaPagamento = compra.FormaPagamento?.Trim() ?? "";
        if (!FormasPagamento.Todas.Contains(compra.FormaPagamento)) throw new RegraException("Forma de pagamento inválida.");
        if (FormasPagamento.EhAVista(compra.FormaPagamento)) compra.Parcelas = 1;
        else if (compra.Parcelas < 1 || compra.Parcelas > 24) throw new RegraException("O número de parcelas deve ser entre 1 e 24.");

        compra.NumeroNf = compra.NumeroNf?.Trim() ?? "";
        compra.Observacoes = compra.Observacoes?.Trim() ?? "";
    }
}
