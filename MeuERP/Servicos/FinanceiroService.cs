using System.Data;
using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;

namespace MeuERP.Servicos;

public enum TipoTitulo { Receber, Pagar }

public class FiltroTitulos
{
    public string? Busca { get; set; }
    /// <summary>null/"" = todos; ABERTO, VENCIDO, PAGO ou CANCELADO.</summary>
    public string? Situacao { get; set; }
    public DateTime? VencimentoDe { get; set; }
    public DateTime? VencimentoAte { get; set; }
    public int? PessoaId { get; set; }
}

/// <summary>Contas a receber, contas a pagar e caixa.</summary>
public static class FinanceiroService
{
    private static string Tabela(TipoTitulo t) => t == TipoTitulo.Receber ? "contas_receber" : "contas_pagar";
    private static string ColPessoa(TipoTitulo t) => t == TipoTitulo.Receber ? "cliente_id" : "fornecedor_id";
    private static string ColOrigem(TipoTitulo t) => t == TipoTitulo.Receber ? "venda_id" : "compra_id";
    private static string TabPessoa(TipoTitulo t) => t == TipoTitulo.Receber ? "clientes" : "fornecedores";
    private static string TipoCaixaBaixa(TipoTitulo t) => t == TipoTitulo.Receber ? TipoCaixa.Entrada : TipoCaixa.Saida;
    private static string TipoCaixaEstorno(TipoTitulo t) => t == TipoTitulo.Receber ? TipoCaixa.Saida : TipoCaixa.Entrada;
    private static string OrigemBaixa(TipoTitulo t) => t == TipoTitulo.Receber ? "RECEBIMENTO" : "PAGAMENTO";

    private static string Select(TipoTitulo t) => $@"
        SELECT t.id, t.descricao, t.{ColPessoa(t)} AS pessoa_id, COALESCE(p.nome, '') AS pessoa_nome, t.{ColOrigem(t)} AS origem_id,
               t.parcela, t.total_parcelas, t.valor, t.valor_pago, t.data_emissao, t.data_vencimento, t.data_pagamento,
               t.forma_pagamento, t.status, t.observacoes
        FROM {Tabela(t)} t LEFT JOIN {TabPessoa(t)} p ON p.id = t.{ColPessoa(t)} ";

    public static List<Titulo> Listar(TipoTitulo tipo, FiltroTitulos? f = null)
    {
        f ??= new FiltroTitulos();
        string b = f.Busca?.Trim() ?? "";
        string sit = f.Situacao ?? "";
        using var c = Database.Abrir();
        return c.Query<Titulo>(Select(tipo) + @"
            WHERE (@b = '' OR t.descricao LIKE @l OR p.nome LIKE @l)
              AND (@pessoa IS NULL OR t." + ColPessoa(tipo) + @" = @pessoa)
              AND (@de IS NULL OR t.data_vencimento >= @de)
              AND (@ate IS NULL OR t.data_vencimento < @ate)
              AND (@sit = ''
                   OR (@sit = 'VENCIDO' AND t.status = 'ABERTO' AND t.data_vencimento < @hoje)
                   OR (@sit = 'ABERTO' AND t.status = 'ABERTO')
                   OR (@sit NOT IN ('VENCIDO','ABERTO') AND t.status = @sit))
            ORDER BY t.data_vencimento, t.id",
            new
            {
                b, l = $"%{b}%", sit, pessoa = f.PessoaId, hoje = DateTime.Today,
                de = f.VencimentoDe?.Date, ate = f.VencimentoAte?.Date.AddDays(1)
            }).ToList();
    }

    public static Titulo? Obter(TipoTitulo tipo, int id)
    {
        using var c = Database.Abrir();
        return c.QueryFirstOrDefault<Titulo>(Select(tipo) + "WHERE t.id = @id", new { id });
    }

    /// <summary>
    /// Gera as parcelas (mensais) de uma venda/compra dentro da transação. Se <paramref name="quitado"/>,
    /// os títulos já nascem pagos e o valor entra/sai do caixa na hora.
    /// </summary>
    public static void GerarParcelas(IDbConnection c, IDbTransaction? tx, TipoTitulo tipo, string descricao, int? pessoaId,
        int? origemId, decimal total, int parcelas, DateTime primeiroVencimento, string forma, bool quitado = false)
    {
        if (total <= 0) return;
        parcelas = Math.Max(1, parcelas);
        decimal valorParcela = Math.Floor(total / parcelas * 100) / 100;
        decimal resto = total - valorParcela * parcelas;
        DateTime agora = DateTime.Now;

        for (int i = 1; i <= parcelas; i++)
        {
            decimal valor = i == parcelas ? valorParcela + resto : valorParcela;
            int id = c.ExecuteScalar<int>($@"INSERT INTO {Tabela(tipo)}
                (descricao, {ColPessoa(tipo)}, {ColOrigem(tipo)}, parcela, total_parcelas, valor, valor_pago, data_emissao,
                 data_vencimento, data_pagamento, forma_pagamento, status)
                VALUES (@descricao, @pessoaId, @origemId, @i, @parcelas, @valor, @pago, @emissao, @venc, @pagamento, @forma, @status);
                SELECT last_insert_rowid();",
                new
                {
                    descricao, pessoaId, origemId, i, parcelas, valor,
                    pago = quitado ? valor : 0m,
                    emissao = agora.Date,
                    venc = quitado ? agora.Date : primeiroVencimento.Date.AddMonths(i - 1),
                    pagamento = quitado ? agora : (DateTime?)null,
                    forma,
                    status = quitado ? StatusTitulo.Pago : StatusTitulo.Aberto
                }, tx);

            if (quitado)
                LancarCaixa(c, tx, TipoCaixaBaixa(tipo), descricao, valor, forma, OrigemBaixa(tipo), id, agora);
        }
    }

    /// <summary>Inclui ou altera um título avulso (sem venda/compra). Só é possível alterar títulos em aberto e sem pagamentos.</summary>
    public static void SalvarManual(TipoTitulo tipo, Titulo t)
    {
        t.Descricao = t.Descricao.Trim();
        if (t.Descricao.Length == 0) throw new RegraException("Informe a descrição.");
        if (t.Valor <= 0) throw new RegraException("O valor deve ser maior que zero.");

        using var c = Database.Abrir();
        if (t.Id == 0)
        {
            t.Id = c.ExecuteScalar<int>($@"INSERT INTO {Tabela(tipo)}
                (descricao, {ColPessoa(tipo)}, parcela, total_parcelas, valor, data_emissao, data_vencimento, forma_pagamento, status, observacoes)
                VALUES (@Descricao, @PessoaId, 1, 1, @Valor, @emissao, @venc, @FormaPagamento, 'ABERTO', @Observacoes);
                SELECT last_insert_rowid();",
                new { t.Descricao, t.PessoaId, t.Valor, emissao = t.DataEmissao.Date, venc = t.DataVencimento.Date, t.FormaPagamento, t.Observacoes });
        }
        else
        {
            var atual = Obter(tipo, t.Id) ?? throw new RegraException("Título não encontrado.");
            if (atual.Status != StatusTitulo.Aberto || atual.ValorPago > 0)
                throw new RegraException("Só é possível alterar títulos em aberto e sem pagamentos.");
            if (atual.OrigemId != null)
                throw new RegraException("Este título foi gerado por uma " + (tipo == TipoTitulo.Receber ? "venda" : "compra") +
                                         " e não pode ser alterado manualmente.");
            c.Execute($@"UPDATE {Tabela(tipo)} SET descricao=@Descricao, {ColPessoa(tipo)}=@PessoaId, valor=@Valor,
                data_vencimento=@venc, forma_pagamento=@FormaPagamento, observacoes=@Observacoes WHERE id=@Id",
                new { t.Id, t.Descricao, t.PessoaId, t.Valor, venc = t.DataVencimento.Date, t.FormaPagamento, t.Observacoes });
        }
    }

    /// <summary>Baixa total ou parcial: registra o pagamento/recebimento e lança no caixa.</summary>
    public static void Baixar(TipoTitulo tipo, int id, decimal valor, DateTime data, string forma)
    {
        Database.EmTransacao((c, tx) =>
        {
            var t = c.QueryFirstOrDefault<Titulo>(Select(tipo) + "WHERE t.id = @id", new { id }, tx)
                    ?? throw new RegraException("Título não encontrado.");
            if (t.Status != StatusTitulo.Aberto) throw new RegraException("Este título não está em aberto.");
            valor = Formato.Arredondar(valor);
            if (valor <= 0) throw new RegraException("O valor deve ser maior que zero.");
            if (valor > t.Saldo + 0.001m) throw new RegraException($"O valor informado é maior que o saldo do título ({Formato.Moeda(t.Saldo)}).");

            decimal pago = t.ValorPago + valor;
            bool quitou = pago >= t.Valor - 0.001m;
            c.Execute($@"UPDATE {Tabela(tipo)} SET valor_pago = @pago, status = @status, data_pagamento = @data,
                forma_pagamento = @forma WHERE id = @id",
                new { id, pago, status = quitou ? StatusTitulo.Pago : StatusTitulo.Aberto, data, forma }, tx);

            string desc = $"{(tipo == TipoTitulo.Receber ? "Recebimento" : "Pagamento")}: {t.Descricao}" +
                          (t.TotalParcelas > 1 ? $" ({t.ParcelaTexto})" : "");
            LancarCaixa(c, tx, TipoCaixaBaixa(tipo), desc, valor, forma, OrigemBaixa(tipo), id, data);
        });
    }

    /// <summary>Desfaz todos os pagamentos de um título (volta para ABERTO) e lança o estorno no caixa.</summary>
    public static void Estornar(TipoTitulo tipo, int id)
    {
        Database.EmTransacao((c, tx) =>
        {
            var t = c.QueryFirstOrDefault<Titulo>(Select(tipo) + "WHERE t.id = @id", new { id }, tx)
                    ?? throw new RegraException("Título não encontrado.");
            if (t.Status == StatusTitulo.Cancelado) throw new RegraException("Título cancelado não pode ser estornado.");
            if (t.ValorPago <= 0) throw new RegraException("Este título não possui pagamentos para estornar.");
            c.Execute($"UPDATE {Tabela(tipo)} SET valor_pago = 0, status = 'ABERTO', data_pagamento = NULL WHERE id = @id", new { id }, tx);
            LancarCaixa(c, tx, TipoCaixaEstorno(tipo), "Estorno: " + t.Descricao, t.ValorPago, t.FormaPagamento, "ESTORNO", id, DateTime.Now);
        });
    }

    /// <summary>Cancela um título em aberto e sem pagamentos.</summary>
    public static void Cancelar(TipoTitulo tipo, int id)
    {
        var t = Obter(tipo, id) ?? throw new RegraException("Título não encontrado.");
        if (t.Status != StatusTitulo.Aberto) throw new RegraException("Só é possível cancelar títulos em aberto.");
        if (t.ValorPago > 0) throw new RegraException("Este título possui pagamentos. Estorne-os antes de cancelar.");
        using var c = Database.Abrir();
        c.Execute($"UPDATE {Tabela(tipo)} SET status = 'CANCELADO' WHERE id = @id", new { id });
    }

    /// <summary>Cancela todos os títulos de uma venda/compra; o que já foi pago é estornado no caixa.</summary>
    public static void CancelarPorOrigem(IDbConnection c, IDbTransaction? tx, TipoTitulo tipo, int origemId)
    {
        var titulos = c.Query<Titulo>(Select(tipo) + $"WHERE t.{ColOrigem(tipo)} = @origemId AND t.status <> 'CANCELADO'", new { origemId }, tx).ToList();
        foreach (var t in titulos)
        {
            if (t.ValorPago > 0)
                LancarCaixa(c, tx, TipoCaixaEstorno(tipo), "Estorno: " + t.Descricao, t.ValorPago, t.FormaPagamento, "ESTORNO", t.Id, DateTime.Now);
            c.Execute($"UPDATE {Tabela(tipo)} SET status = 'CANCELADO' WHERE id = @Id", new { t.Id }, tx);
        }
    }

    // ---------- Caixa ----------

    public static void LancarCaixa(IDbConnection c, IDbTransaction? tx, string tipo, string descricao, decimal valor,
        string forma, string origem, int? origemId, DateTime data)
    {
        if (tipo is not (TipoCaixa.Entrada or TipoCaixa.Saida)) throw new RegraException("Tipo de lançamento inválido.");
        if (string.IsNullOrWhiteSpace(descricao)) throw new RegraException("Informe a descrição do lançamento.");
        if (valor <= 0) throw new RegraException("O valor deve ser maior que zero.");
        c.Execute(@"INSERT INTO caixa_lancamentos (data, tipo, descricao, valor, forma_pagamento, origem, origem_id, usuario_id)
            VALUES (@data, @tipo, @descricao, @valor, @forma, @origem, @origemId, @usuario)",
            new { data, tipo, descricao = descricao.Trim(), valor, forma, origem, origemId, usuario = Sessao.UsuarioId }, tx);
    }

    /// <summary>Lançamento avulso de caixa (suprimento, sangria, despesa miúda...).</summary>
    public static void LancarCaixaManual(string tipo, string descricao, decimal valor, string forma, DateTime data)
    {
        using var c = Database.Abrir();
        LancarCaixa(c, null, tipo, descricao, valor, forma, "MANUAL", null, data);
    }

    public static void ExcluirLancamentoManual(int id)
    {
        using var c = Database.Abrir();
        if (c.Execute("DELETE FROM caixa_lancamentos WHERE id = @id AND origem = 'MANUAL'", new { id }) == 0)
            throw new RegraException("Só é possível excluir lançamentos manuais. Para os demais, estorne o título de origem.");
    }

    public static List<LancamentoCaixa> ListarCaixa(DateTime de, DateTime ate, string? busca = null)
    {
        using var c = Database.Abrir();
        string b = busca?.Trim() ?? "";
        return c.Query<LancamentoCaixa>(@"
            SELECT l.*, COALESCE(u.nome, '') AS usuario_nome
            FROM caixa_lancamentos l LEFT JOIN usuarios u ON u.id = l.usuario_id
            WHERE l.data >= @de AND l.data < @ate AND (@b = '' OR l.descricao LIKE @l)
            ORDER BY l.data DESC, l.id DESC",
            new { de = de.Date, ate = ate.Date.AddDays(1), b, l = $"%{b}%" }).ToList();
    }

    /// <summary>Saldo acumulado do caixa até o fim do dia informado (ou total, se nulo).</summary>
    public static decimal SaldoCaixa(DateTime? ate = null)
    {
        using var c = Database.Abrir();
        return Formato.Arredondar(c.ExecuteScalar<decimal>(@"
            SELECT COALESCE(SUM(CASE WHEN tipo = 'ENTRADA' THEN valor ELSE -valor END), 0)
            FROM caixa_lancamentos WHERE (@ate IS NULL OR data < @ate)",
            new { ate = ate?.Date.AddDays(1) }));
    }
}
