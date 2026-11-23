using Dapper;
using MeuERP.Dados;
using MeuERP.Servicos;

namespace MeuERP.Telas.Financeiro;

/// <summary>Totais de títulos para os indicadores das páginas de contas a receber/pagar.</summary>
public class ResumoTitulos
{
    public decimal EmAberto { get; set; }
    public int QtdAberto { get; set; }
    public decimal Vencido { get; set; }
    public int QtdVencido { get; set; }
    public decimal VenceSemana { get; set; }
    public int QtdVenceSemana { get; set; }
    public decimal PagoMes { get; set; }
    public int QtdPagoMes { get; set; }
}

/// <summary>Consultas de leitura do módulo financeiro que o FinanceiroService não oferece.</summary>
public static class ConsultasFinanceiro
{
    private static string Tabela(TipoTitulo t) => t == TipoTitulo.Receber ? "contas_receber" : "contas_pagar";

    /// <summary>
    /// Resumo geral (independente dos filtros da tela): saldo em aberto, vencido, a vencer nos próximos 7 dias
    /// e soma de valor_pago dos títulos com data de pagamento no mês atual.
    /// </summary>
    public static ResumoTitulos Resumo(TipoTitulo tipo)
    {
        var hoje = DateTime.Today;
        var mesIni = new DateTime(hoje.Year, hoje.Month, 1);
        using var c = Database.Abrir();
        return c.QueryFirst<ResumoTitulos>($@"
            SELECT
              COALESCE(SUM(CASE WHEN status = 'ABERTO' THEN MAX(valor - valor_pago, 0) END), 0) AS em_aberto,
              COUNT(CASE WHEN status = 'ABERTO' THEN 1 END) AS qtd_aberto,
              COALESCE(SUM(CASE WHEN status = 'ABERTO' AND data_vencimento < @hoje THEN MAX(valor - valor_pago, 0) END), 0) AS vencido,
              COUNT(CASE WHEN status = 'ABERTO' AND data_vencimento < @hoje THEN 1 END) AS qtd_vencido,
              COALESCE(SUM(CASE WHEN status = 'ABERTO' AND data_vencimento >= @hoje AND data_vencimento < @limite
                                THEN MAX(valor - valor_pago, 0) END), 0) AS vence_semana,
              COUNT(CASE WHEN status = 'ABERTO' AND data_vencimento >= @hoje AND data_vencimento < @limite THEN 1 END) AS qtd_vence_semana,
              COALESCE(SUM(CASE WHEN status <> 'CANCELADO' AND data_pagamento >= @mesIni AND data_pagamento < @mesFim
                                THEN valor_pago END), 0) AS pago_mes,
              COUNT(CASE WHEN status <> 'CANCELADO' AND valor_pago > 0 AND data_pagamento >= @mesIni AND data_pagamento < @mesFim
                         THEN 1 END) AS qtd_pago_mes
            FROM {Tabela(tipo)}",
            new { hoje, limite = hoje.AddDays(8), mesIni, mesFim = mesIni.AddMonths(1) });
    }
}
