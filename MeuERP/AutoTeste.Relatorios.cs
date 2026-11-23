using Dapper;
using MeuERP.Dados;
using MeuERP.Servicos;

namespace MeuERP;

internal static partial class AutoTeste
{
    /// <summary>Confere as consultas do dashboard e dos relatórios contra a base de demonstração.</summary>
    static partial void TestarRelatoriosImpl()
    {
        DateTime ini = DateTime.Today.AddMonths(-7), fim = DateTime.Today;
        using var c = Database.Abrir();
        decimal receita = c.ExecuteScalar<decimal>(
            "SELECT COALESCE(SUM(total), 0) FROM vendas WHERE status = 'FINALIZADA' AND data_venda >= @ini AND data_venda < @fim",
            new { ini, fim = fim.AddDays(1) });

        var r = RelatorioService.Resumo();
        Verificar("dashboard: há vendas no mês", r.VendasMes > 0 && r.QtdVendasMes > 0);
        Igual("dashboard: saldo de caixa", FinanceiroService.SaldoCaixa(), r.SaldoCaixa);
        Verificar("dashboard: estoque baixo", r.ProdutosEstoqueBaixo > 0);

        var dias = RelatorioService.VendasPorDia(30);
        Verificar("gráfico 30 dias tem 30 pontos", dias.Count == 30);
        Verificar("gráfico 12 meses tem 12 pontos", RelatorioService.VendasPorMes(12).Count == 12);

        Igual("vendas por dia somam a receita", receita, RelatorioService.VendasPorDiaPeriodo(ini, fim).Sum(x => x.Total));
        Igual("vendas por produto somam a receita", receita, RelatorioService.VendasPorProduto(ini, fim).Sum(x => x.Receita));
        Igual("vendas por forma somam a receita", receita, RelatorioService.VendasPorForma(ini, fim).Sum(x => x.Total));
        Igual("vendas por vendedor somam a receita", receita, RelatorioService.VendasPorVendedor(ini, fim).Sum(x => x.Total));

        var dre = RelatorioService.Resultado(ini, fim);
        Igual("DRE: receita", receita, dre.Receita);
        Verificar("DRE: lucro bruto positivo", dre.LucroBruto > 0 && dre.Cmv > 0);

        var fluxo = RelatorioService.FluxoCaixa(ini, fim, out decimal saldoAnterior);
        Igual("fluxo de caixa fecha com o saldo", FinanceiroService.SaldoCaixa(),
            fluxo.Count > 0 ? fluxo[^1].SaldoAcumulado : saldoAnterior);

        Verificar("posição de estoque lista produtos", RelatorioService.PosicaoEstoque().Count >= 20);
        Igual("títulos a receber em aberto",
            c.ExecuteScalar<decimal>("SELECT COALESCE(SUM(valor - valor_pago), 0) FROM contas_receber WHERE status = 'ABERTO'"),
            RelatorioService.TitulosEmAberto(TipoTitulo.Receber).Sum(x => x.Saldo));
    }
}
