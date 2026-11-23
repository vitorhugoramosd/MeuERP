using System.Globalization;
using Dapper;
using MeuERP.Dados;
using MeuERP.Nucleo;

namespace MeuERP.Servicos;

// ---------- DTOs do dashboard ----------

public class ResumoDashboard
{
    public decimal VendasHoje { get; set; }
    public int QtdVendasHoje { get; set; }
    public decimal VendasMes { get; set; }
    public int QtdVendasMes { get; set; }
    public decimal TicketMedioMes { get; set; }
    /// <summary>Vendas do mês anterior no mesmo período (do dia 1 até o mesmo dia de hoje), para comparação justa.</summary>
    public decimal VendasMesAnterior { get; set; }
    public decimal LucroMes { get; set; }
    public decimal ReceberAberto { get; set; }
    public decimal ReceberVencido { get; set; }
    public decimal PagarAberto { get; set; }
    public decimal PagarVencido { get; set; }
    public decimal SaldoCaixa { get; set; }
    public int ProdutosEstoqueBaixo { get; set; }
    public decimal ValorEstoque { get; set; }

    /// <summary>Variação % das vendas do mês contra o mesmo período do mês anterior (null se não houver base).</summary>
    public decimal? VariacaoMes => VendasMesAnterior > 0 ? Math.Round((VendasMes - VendasMesAnterior) / VendasMesAnterior * 100, 1) : null;
}

/// <summary>Ponto de uma série temporal (um dia ou um mês).</summary>
public class PontoSerie
{
    public DateTime Data { get; set; }
    public decimal Valor { get; set; }
    public int Quantidade { get; set; }
}

public class ProdutoVendido
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public decimal Quantidade { get; set; }
    public decimal Valor { get; set; }
}

public class VendaResumida
{
    public int Id { get; set; }
    public DateTime DataVenda { get; set; }
    public string ClienteNome { get; set; } = "";
    public string FormaPagamento { get; set; } = "";
    public decimal Total { get; set; }
}

public class VencimentoProximo
{
    /// <summary>"Receber" ou "Pagar".</summary>
    public string Tipo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Pessoa { get; set; } = "";
    public DateTime DataVencimento { get; set; }
    public decimal Saldo { get; set; }
    public bool Vencido => DataVencimento.Date < DateTime.Today;
}

public class ProdutoEstoqueBaixo
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Unidade { get; set; } = "";
    public decimal Estoque { get; set; }
    public decimal EstoqueMinimo { get; set; }
}

// ---------- DTOs dos relatórios ----------

public class RelVendasDia
{
    public DateTime Data { get; set; }
    public int Quantidade { get; set; }
    public decimal Total { get; set; }
    public decimal TicketMedio { get; set; }
}

public class RelVendasProduto
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public decimal Quantidade { get; set; }
    public decimal Receita { get; set; }
    public decimal Custo { get; set; }
    public decimal Lucro { get; set; }
    public decimal Margem { get; set; }
}

public class RelVendasCliente
{
    public string Cliente { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal Total { get; set; }
    public DateTime UltimaCompra { get; set; }
}

public class RelVendasForma
{
    public string Forma { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal Total { get; set; }
    public decimal Percentual { get; set; }
}

public class RelVendasVendedor
{
    public string Usuario { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal Total { get; set; }
    public decimal TicketMedio { get; set; }
}

public class RelComprasFornecedor
{
    public string Fornecedor { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal Total { get; set; }
    public DateTime UltimaCompra { get; set; }
}

public class RelPosicaoEstoque
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Unidade { get; set; } = "";
    public decimal Saldo { get; set; }
    public decimal Minimo { get; set; }
    public decimal Custo { get; set; }
    public decimal ValorEstoque { get; set; }
    public bool Baixo => Minimo > 0 && Saldo <= Minimo;
}

public class RelTituloAberto
{
    public string Pessoa { get; set; } = "";
    public string Descricao { get; set; } = "";
    public int Parcela { get; set; }
    public int TotalParcelas { get; set; }
    public DateTime Vencimento { get; set; }
    public int DiasAtraso { get; set; }
    public decimal Saldo { get; set; }
    public string ParcelaTexto => $"{Parcela}/{TotalParcelas}";
}

public class RelFluxoCaixa
{
    public DateTime Data { get; set; }
    public decimal Entradas { get; set; }
    public decimal Saidas { get; set; }
    public decimal SaldoDia { get; set; }
    public decimal SaldoAcumulado { get; set; }
}

public class RelResultado
{
    public string Descricao { get; set; } = "";
    public string Valor { get; set; } = "";
    public string Percentual { get; set; } = "";
    public bool Destaque { get; set; }
}

/// <summary>Consultas somente leitura para o dashboard e os relatórios gerenciais.</summary>
public static class RelatorioService
{
    private const string Finalizada = "v.status = 'FINALIZADA'";

    private class Agregado
    {
        public int Qtd { get; set; }
        public decimal Total { get; set; }
    }

    private class LinhaAgrupada
    {
        public string Chave { get; set; } = "";
        public int Qtd { get; set; }
        public decimal Total { get; set; }
    }

    private static decimal R(decimal v) => Formato.Arredondar(v);

    private static Agregado Vendas(System.Data.IDbConnection c, DateTime de, DateTime ateExclusivo)
    {
        var a = c.QueryFirst<Agregado>($@"SELECT COUNT(*) AS qtd, COALESCE(SUM(v.total), 0) AS total
            FROM vendas v WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate", new { de, ate = ateExclusivo });
        a.Total = R(a.Total);
        return a;
    }

    // ---------- Dashboard ----------

    public static ResumoDashboard Resumo()
    {
        DateTime hoje = DateTime.Today, amanha = hoje.AddDays(1);
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
        var inicioAnterior = inicioMes.AddMonths(-1);
        // mesmo período do mês anterior: do dia 1 até o mesmo dia (limitado ao fim daquele mês)
        var fimAnterior = inicioAnterior.AddDays(Math.Min(hoje.Day, DateTime.DaysInMonth(inicioAnterior.Year, inicioAnterior.Month)));

        using var c = Database.Abrir();
        var r = new ResumoDashboard();
        var dia = Vendas(c, hoje, amanha);
        r.VendasHoje = dia.Total;
        r.QtdVendasHoje = dia.Qtd;
        var mes = Vendas(c, inicioMes, amanha);
        r.VendasMes = mes.Total;
        r.QtdVendasMes = mes.Qtd;
        r.TicketMedioMes = mes.Qtd > 0 ? R(mes.Total / mes.Qtd) : 0;
        r.VendasMesAnterior = Vendas(c, inicioAnterior, fimAnterior).Total;

        decimal custoMes = c.ExecuteScalar<decimal>($@"SELECT COALESCE(SUM(vi.quantidade * vi.custo_unitario), 0)
            FROM venda_itens vi JOIN vendas v ON v.id = vi.venda_id
            WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate", new { de = inicioMes, ate = amanha });
        r.LucroMes = R(r.VendasMes - custoMes);

        const string aberto = "SELECT COALESCE(SUM(valor - valor_pago), 0) FROM {0} WHERE status = 'ABERTO'";
        const string vencido = "SELECT COALESCE(SUM(valor - valor_pago), 0) FROM {0} WHERE status = 'ABERTO' AND data_vencimento < @hoje";
        r.ReceberAberto = R(c.ExecuteScalar<decimal>(string.Format(aberto, "contas_receber")));
        r.ReceberVencido = R(c.ExecuteScalar<decimal>(string.Format(vencido, "contas_receber"), new { hoje }));
        r.PagarAberto = R(c.ExecuteScalar<decimal>(string.Format(aberto, "contas_pagar")));
        r.PagarVencido = R(c.ExecuteScalar<decimal>(string.Format(vencido, "contas_pagar"), new { hoje }));
        r.SaldoCaixa = R(c.ExecuteScalar<decimal>("SELECT COALESCE(SUM(CASE WHEN tipo = 'ENTRADA' THEN valor ELSE -valor END), 0) FROM caixa_lancamentos"));
        r.ProdutosEstoqueBaixo = c.ExecuteScalar<int>("SELECT COUNT(*) FROM produtos WHERE ativo = 1 AND estoque_minimo > 0 AND estoque <= estoque_minimo");
        r.ValorEstoque = R(c.ExecuteScalar<decimal>("SELECT COALESCE(SUM(estoque * preco_custo), 0) FROM produtos WHERE ativo = 1 AND estoque > 0"));
        return r;
    }

    /// <summary>Vendas finalizadas por dia nos últimos <paramref name="dias"/> dias (incluindo hoje); dias sem venda vêm com zero.</summary>
    public static List<PontoSerie> VendasPorDia(int dias)
    {
        dias = Math.Max(1, dias);
        DateTime de = DateTime.Today.AddDays(-(dias - 1)), ate = DateTime.Today.AddDays(1);
        using var c = Database.Abrir();
        var mapa = c.Query<LinhaAgrupada>($@"SELECT substr(v.data_venda, 1, 10) AS chave, COUNT(*) AS qtd, COALESCE(SUM(v.total), 0) AS total
            FROM vendas v WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate GROUP BY 1", new { de, ate })
            .ToDictionary(l => l.Chave);
        var lista = new List<PontoSerie>();
        for (var d = de; d < ate; d = d.AddDays(1))
        {
            mapa.TryGetValue(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), out var l);
            lista.Add(new PontoSerie { Data = d, Valor = R(l?.Total ?? 0), Quantidade = l?.Qtd ?? 0 });
        }
        return lista;
    }

    /// <summary>Vendas finalizadas por mês nos últimos <paramref name="meses"/> meses (incluindo o atual); meses sem venda vêm com zero.</summary>
    public static List<PontoSerie> VendasPorMes(int meses)
    {
        meses = Math.Max(1, meses);
        var atual = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime de = atual.AddMonths(-(meses - 1)), ate = atual.AddMonths(1);
        using var c = Database.Abrir();
        var mapa = c.Query<LinhaAgrupada>($@"SELECT substr(v.data_venda, 1, 7) AS chave, COUNT(*) AS qtd, COALESCE(SUM(v.total), 0) AS total
            FROM vendas v WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate GROUP BY 1", new { de, ate })
            .ToDictionary(l => l.Chave);
        var lista = new List<PontoSerie>();
        for (var d = de; d < ate; d = d.AddMonths(1))
        {
            mapa.TryGetValue(d.ToString("yyyy-MM", CultureInfo.InvariantCulture), out var l);
            lista.Add(new PontoSerie { Data = d, Valor = R(l?.Total ?? 0), Quantidade = l?.Qtd ?? 0 });
        }
        return lista;
    }

    /// <summary>Produtos com maior faturamento no período.</summary>
    public static List<ProdutoVendido> TopProdutos(DateTime de, DateTime ate, int limite = 5)
    {
        using var c = Database.Abrir();
        return c.Query<ProdutoVendido>($@"
            SELECT p.codigo, p.descricao, SUM(vi.quantidade) AS quantidade, SUM(vi.total) AS valor
            FROM venda_itens vi JOIN vendas v ON v.id = vi.venda_id JOIN produtos p ON p.id = vi.produto_id
            WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate
            GROUP BY p.id ORDER BY valor DESC, quantidade DESC LIMIT @limite",
            new { de = de.Date, ate = ate.Date.AddDays(1), limite })
            .Select(p => { p.Valor = R(p.Valor); return p; }).ToList();
    }

    public static List<VendaResumida> UltimasVendas(int limite = 8)
    {
        using var c = Database.Abrir();
        return c.Query<VendaResumida>($@"
            SELECT v.id, v.data_venda, COALESCE(cl.nome, 'Consumidor final') AS cliente_nome, v.forma_pagamento, v.total
            FROM vendas v LEFT JOIN clientes cl ON cl.id = v.cliente_id
            WHERE {Finalizada} ORDER BY v.data_venda DESC, v.id DESC LIMIT @limite", new { limite }).ToList();
    }

    /// <summary>Títulos em aberto (a receber e a pagar) vencidos ou que vencem nos próximos <paramref name="dias"/> dias.</summary>
    public static List<VencimentoProximo> ProximosVencimentos(int dias = 7, int limite = 60)
    {
        using var c = Database.Abrir();
        return c.Query<VencimentoProximo>(@"
            SELECT * FROM (
                SELECT 'Receber' AS tipo, t.descricao, COALESCE(cl.nome, '') AS pessoa, t.data_vencimento, t.valor - t.valor_pago AS saldo
                FROM contas_receber t LEFT JOIN clientes cl ON cl.id = t.cliente_id
                WHERE t.status = 'ABERTO' AND t.data_vencimento < @limiteData
                UNION ALL
                SELECT 'Pagar', t.descricao, COALESCE(f.nome, ''), t.data_vencimento, t.valor - t.valor_pago
                FROM contas_pagar t LEFT JOIN fornecedores f ON f.id = t.fornecedor_id
                WHERE t.status = 'ABERTO' AND t.data_vencimento < @limiteData
            ) ORDER BY data_vencimento, tipo LIMIT @limite",
            new { limiteData = DateTime.Today.AddDays(dias + 1), limite })
            .Select(v => { v.Saldo = R(v.Saldo); return v; }).ToList();
    }

    public static List<ProdutoEstoqueBaixo> EstoqueBaixo(int limite = 100)
    {
        using var c = Database.Abrir();
        return c.Query<ProdutoEstoqueBaixo>(@"
            SELECT codigo, descricao, unidade, estoque, estoque_minimo FROM produtos
            WHERE ativo = 1 AND estoque_minimo > 0 AND estoque <= estoque_minimo
            ORDER BY (estoque / estoque_minimo), descricao LIMIT @limite", new { limite }).ToList();
    }

    // ---------- Relatórios ----------

    private static object Periodo(DateTime de, DateTime ate) => new { de = de.Date, ate = ate.Date.AddDays(1) };

    /// <summary>(1) Vendas por dia (somente dias com venda).</summary>
    public static List<RelVendasDia> VendasPorDiaPeriodo(DateTime de, DateTime ate)
    {
        using var c = Database.Abrir();
        return c.Query<LinhaAgrupada>($@"SELECT substr(v.data_venda, 1, 10) AS chave, COUNT(*) AS qtd, COALESCE(SUM(v.total), 0) AS total
            FROM vendas v WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate GROUP BY 1 ORDER BY 1", Periodo(de, ate))
            .Select(l => new RelVendasDia
            {
                Data = DateTime.ParseExact(l.Chave, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Quantidade = l.Qtd,
                Total = R(l.Total),
                TicketMedio = l.Qtd > 0 ? R(l.Total / l.Qtd) : 0
            }).ToList();
    }

    /// <summary>(2) Vendas por produto. A receita considera o desconto geral da venda rateado proporcionalmente entre os itens.</summary>
    public static List<RelVendasProduto> VendasPorProduto(DateTime de, DateTime ate)
    {
        using var c = Database.Abrir();
        var linhas = c.Query<RelVendasProduto>($@"
            SELECT p.codigo, p.descricao, SUM(vi.quantidade) AS quantidade,
                   SUM(vi.total * CASE WHEN v.subtotal > 0 THEN v.total / v.subtotal ELSE 1 END) AS receita,
                   SUM(vi.quantidade * vi.custo_unitario) AS custo
            FROM venda_itens vi JOIN vendas v ON v.id = vi.venda_id JOIN produtos p ON p.id = vi.produto_id
            WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate
            GROUP BY p.id ORDER BY receita DESC", Periodo(de, ate)).ToList();

        foreach (var l in linhas) { l.Receita = R(l.Receita); l.Custo = R(l.Custo); }

        // O rateio do desconto, arredondado produto a produto, pode deixar centavos de diferença:
        // o resíduo vai para o produto de maior receita, para o total bater com o faturamento.
        if (linhas.Count > 0)
        {
            decimal faturado = R(c.ExecuteScalar<decimal>(
                $"SELECT COALESCE(SUM(v.total), 0) FROM vendas v WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate",
                Periodo(de, ate)));
            linhas[0].Receita += faturado - linhas.Sum(l => l.Receita);
        }

        foreach (var l in linhas)
        {
            l.Lucro = l.Receita - l.Custo;
            l.Margem = l.Receita > 0 ? Math.Round(l.Lucro / l.Receita * 100, 1) : 0;
        }
        return linhas;
    }

    /// <summary>(3) Vendas por cliente (vendas sem cliente aparecem como "Consumidor final").</summary>
    public static List<RelVendasCliente> VendasPorCliente(DateTime de, DateTime ate)
    {
        using var c = Database.Abrir();
        return c.Query<RelVendasCliente>($@"
            SELECT COALESCE(cl.nome, 'Consumidor final') AS cliente, COUNT(*) AS quantidade, SUM(v.total) AS total, MAX(v.data_venda) AS ultima_compra
            FROM vendas v LEFT JOIN clientes cl ON cl.id = v.cliente_id
            WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate
            GROUP BY v.cliente_id ORDER BY total DESC", Periodo(de, ate))
            .Select(l => { l.Total = R(l.Total); return l; }).ToList();
    }

    /// <summary>(4) Vendas por forma de pagamento, com participação no total.</summary>
    public static List<RelVendasForma> VendasPorForma(DateTime de, DateTime ate)
    {
        using var c = Database.Abrir();
        var lista = c.Query<RelVendasForma>($@"
            SELECT CASE WHEN v.forma_pagamento = '' THEN '(não informada)' ELSE v.forma_pagamento END AS forma,
                   COUNT(*) AS quantidade, SUM(v.total) AS total
            FROM vendas v WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate
            GROUP BY 1 ORDER BY total DESC", Periodo(de, ate)).ToList();
        decimal soma = lista.Sum(l => l.Total);
        foreach (var l in lista)
        {
            l.Total = R(l.Total);
            l.Percentual = soma > 0 ? Math.Round(l.Total / soma * 100, 1) : 0;
        }
        return lista;
    }

    /// <summary>(5) Vendas por vendedor (usuário que registrou a venda).</summary>
    public static List<RelVendasVendedor> VendasPorVendedor(DateTime de, DateTime ate)
    {
        using var c = Database.Abrir();
        return c.Query<RelVendasVendedor>($@"
            SELECT COALESCE(u.nome, '(sem usuário)') AS usuario, COUNT(*) AS quantidade, SUM(v.total) AS total
            FROM vendas v LEFT JOIN usuarios u ON u.id = v.usuario_id
            WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate
            GROUP BY v.usuario_id ORDER BY total DESC", Periodo(de, ate))
            .Select(l => { l.Total = R(l.Total); l.TicketMedio = l.Quantidade > 0 ? R(l.Total / l.Quantidade) : 0; return l; }).ToList();
    }

    /// <summary>(6) Compras recebidas por fornecedor (pela data de recebimento).</summary>
    public static List<RelComprasFornecedor> ComprasPorFornecedor(DateTime de, DateTime ate)
    {
        using var c = Database.Abrir();
        return c.Query<RelComprasFornecedor>(@"
            SELECT f.nome AS fornecedor, COUNT(*) AS quantidade, SUM(co.total) AS total,
                   MAX(COALESCE(co.data_recebimento, co.data_compra)) AS ultima_compra
            FROM compras co JOIN fornecedores f ON f.id = co.fornecedor_id
            WHERE co.status = 'RECEBIDA'
              AND COALESCE(co.data_recebimento, co.data_compra) >= @de AND COALESCE(co.data_recebimento, co.data_compra) < @ate
            GROUP BY co.fornecedor_id ORDER BY total DESC", Periodo(de, ate))
            .Select(l => { l.Total = R(l.Total); return l; }).ToList();
    }

    /// <summary>(7) Posição atual de estoque dos produtos ativos.</summary>
    public static List<RelPosicaoEstoque> PosicaoEstoque()
    {
        using var c = Database.Abrir();
        return c.Query<RelPosicaoEstoque>(@"
            SELECT p.codigo, p.descricao, COALESCE(ca.nome, '') AS categoria, p.unidade, p.estoque AS saldo,
                   p.estoque_minimo AS minimo, p.preco_custo AS custo
            FROM produtos p LEFT JOIN categorias ca ON ca.id = p.categoria_id
            WHERE p.ativo = 1 ORDER BY p.descricao")
            .Select(l => { l.Custo = R(l.Custo); l.ValorEstoque = l.Saldo > 0 ? R(l.Saldo * l.Custo) : 0; return l; }).ToList();
    }

    /// <summary>(8) e (9) Títulos em aberto, por vencimento.</summary>
    public static List<RelTituloAberto> TitulosEmAberto(TipoTitulo tipo)
    {
        string sql = tipo == TipoTitulo.Receber
            ? @"SELECT COALESCE(p.nome, 'Consumidor final') AS pessoa, t.descricao, t.parcela, t.total_parcelas,
                       t.data_vencimento AS vencimento, t.valor - t.valor_pago AS saldo
                FROM contas_receber t LEFT JOIN clientes p ON p.id = t.cliente_id"
            : @"SELECT COALESCE(p.nome, '(sem fornecedor)') AS pessoa, t.descricao, t.parcela, t.total_parcelas,
                       t.data_vencimento AS vencimento, t.valor - t.valor_pago AS saldo
                FROM contas_pagar t LEFT JOIN fornecedores p ON p.id = t.fornecedor_id";
        using var c = Database.Abrir();
        DateTime hoje = DateTime.Today;
        return c.Query<RelTituloAberto>(sql + " WHERE t.status = 'ABERTO' ORDER BY t.data_vencimento, t.id")
            .Select(l =>
            {
                l.Saldo = R(l.Saldo);
                l.DiasAtraso = Math.Max(0, (hoje - l.Vencimento.Date).Days);
                return l;
            }).ToList();
    }

    /// <summary>(10) Fluxo de caixa realizado por dia, partindo do saldo anterior ao período.</summary>
    public static List<RelFluxoCaixa> FluxoCaixa(DateTime de, DateTime ate, out decimal saldoAnterior)
    {
        using var c = Database.Abrir();
        saldoAnterior = R(c.ExecuteScalar<decimal>(
            "SELECT COALESCE(SUM(CASE WHEN tipo = 'ENTRADA' THEN valor ELSE -valor END), 0) FROM caixa_lancamentos WHERE data < @de",
            new { de = de.Date }));
        var linhas = c.Query<RelFluxoCaixa>(@"
            SELECT substr(data, 1, 10) AS data,
                   COALESCE(SUM(CASE WHEN tipo = 'ENTRADA' THEN valor END), 0) AS entradas,
                   COALESCE(SUM(CASE WHEN tipo = 'SAIDA' THEN valor END), 0) AS saidas
            FROM caixa_lancamentos WHERE data >= @de AND data < @ate GROUP BY 1 ORDER BY 1", Periodo(de, ate)).ToList();
        decimal acumulado = saldoAnterior;
        foreach (var l in linhas)
        {
            l.Entradas = R(l.Entradas);
            l.Saidas = R(l.Saidas);
            l.SaldoDia = l.Entradas - l.Saidas;
            acumulado += l.SaldoDia;
            l.SaldoAcumulado = acumulado;
        }
        return linhas;
    }

    public class ValoresResultado
    {
        public decimal Receita { get; set; }
        public decimal Cmv { get; set; }
        public decimal LucroBruto => Receita - Cmv;
        public decimal MargemBruta => Receita > 0 ? Math.Round(LucroBruto / Receita * 100, 1) : 0;
        public decimal Despesas { get; set; }
        public decimal Resultado => LucroBruto - Despesas;
        public int QtdVendas { get; set; }
    }

    /// <summary>Valores do resultado do período (DRE simplificado).</summary>
    public static ValoresResultado Resultado(DateTime de, DateTime ate)
    {
        using var c = Database.Abrir();
        var p = Periodo(de, ate);
        var vendas = c.QueryFirst<Agregado>($@"SELECT COUNT(*) AS qtd, COALESCE(SUM(v.total), 0) AS total
            FROM vendas v WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate", p);
        return new ValoresResultado
        {
            QtdVendas = vendas.Qtd,
            Receita = R(vendas.Total),
            Cmv = R(c.ExecuteScalar<decimal>($@"SELECT COALESCE(SUM(vi.quantidade * vi.custo_unitario), 0)
                FROM venda_itens vi JOIN vendas v ON v.id = vi.venda_id
                WHERE {Finalizada} AND v.data_venda >= @de AND v.data_venda < @ate", p)),
            // despesas: contas a pagar que não vieram de compra (aluguel, energia...), pelo que foi pago no período
            Despesas = R(c.ExecuteScalar<decimal>(@"SELECT COALESCE(SUM(valor_pago), 0) FROM contas_pagar
                WHERE compra_id IS NULL AND status <> 'CANCELADO' AND valor_pago > 0
                  AND data_pagamento >= @de AND data_pagamento < @ate", p))
        };
    }

    /// <summary>(11) Linhas do DRE simplificado, prontas para exibição.</summary>
    public static List<RelResultado> ResultadoPeriodo(DateTime de, DateTime ate)
    {
        var v = Resultado(de, ate);
        string Pct(decimal x) => v.Receita > 0 ? Formato.Percentual(Math.Round(x / v.Receita * 100, 1)) : "";
        return new List<RelResultado>
        {
            new() { Descricao = "Receita de vendas", Valor = Formato.Moeda(v.Receita), Percentual = v.Receita > 0 ? "100,0%" : "", Destaque = true },
            new() { Descricao = "(−) Custo das mercadorias vendidas", Valor = Formato.Moeda(-v.Cmv), Percentual = Pct(v.Cmv) },
            new() { Descricao = "(=) Lucro bruto", Valor = Formato.Moeda(v.LucroBruto), Percentual = Pct(v.LucroBruto), Destaque = true },
            new() { Descricao = "Margem bruta %", Valor = Formato.Percentual(v.MargemBruta), Percentual = "" },
            new() { Descricao = "(−) Despesas pagas", Valor = Formato.Moeda(-v.Despesas), Percentual = Pct(v.Despesas) },
            new() { Descricao = "(=) Resultado do período", Valor = Formato.Moeda(v.Resultado), Percentual = Pct(v.Resultado), Destaque = true },
        };
    }
}
