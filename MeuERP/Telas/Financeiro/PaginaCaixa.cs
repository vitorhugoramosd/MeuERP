using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Financeiro;

public class PaginaCaixa : PaginaLista<LancamentoCaixa>
{
    private readonly DateTimePicker _dtDe, _dtAte;
    private readonly CartaoKpi _kAnterior, _kEntradas, _kSaidas, _kPeriodo, _kAtual;

    protected override string NomeExportacao => "caixa";

    public PaginaCaixa()
    {
        Titulo = "Caixa";
        Subtitulo = "Entradas e saídas de dinheiro";

        BtnNovo.Text = "+ Lançamento";
        BtnEditar.Visible = false;
        BtnExcluir.Text = "Excluir lançamento";

        var kpis = new PainelKpis();
        _kAnterior = kpis.Adicionar("Saldo anterior", Tema.TextoSuave);
        _kEntradas = kpis.Adicionar("Entradas", Tema.Sucesso);
        _kSaidas = kpis.Adicionar("Saídas", Tema.Perigo);
        _kPeriodo = kpis.Adicionar("Saldo do período", Tema.Primaria);
        _kAtual = kpis.Adicionar("Saldo atual", Tema.Roxo);
        Conteudo.Controls.Add(kpis); // maior índice: encaixado no topo, acima dos filtros

        TxtBusca.PlaceholderText = "Buscar pela descrição...";
        var hoje = DateTime.Today;
        var ini = new DateTime(hoje.Year, hoje.Month, 1);
        _dtDe = Ui.Data(ini);
        _dtAte = Ui.Data(ini.AddMonths(1).AddDays(-1));
        var lblAte = Ui.Rotulo("até");
        lblAte.Margin = new Padding(0, 8, 6, 0);
        Filtros.Controls.Add(Ui.Rotulo("Período"));
        Filtros.Controls.Add(_dtDe);
        Filtros.Controls.Add(lblAte);
        Filtros.Controls.Add(_dtAte);
        RecarregarAoMudar(_dtDe, _dtAte);

        Grid.CellFormatting += FormatarCelula;
    }

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.ColunaData("Data", "Data", comHora: true);
        grid.Coluna("Descricao", "Descrição");
        grid.ColunaStatus("Tipo", "Tipo", 90);
        grid.Coluna("FormaPagamento", "Forma", 130);
        grid.Coluna("Origem", "Origem", 110);
        grid.ColunaMoeda("ValorComSinal", "Valor", 120);
        grid.Coluna("UsuarioNome", "Usuário", 130);
    }

    protected override IEnumerable<LancamentoCaixa> Carregar(string busca)
    {
        DateTime de = _dtDe.Value.Date, ate = _dtAte.Value.Date;
        if (ate < de) (de, ate) = (ate, de);

        // Indicadores do período inteiro (independentes da busca).
        var periodo = FinanceiroService.ListarCaixa(de, ate);
        decimal anterior = FinanceiroService.SaldoCaixa(de.AddDays(-1));
        decimal entradas = Formato.Arredondar(periodo.Where(l => l.Tipo == TipoCaixa.Entrada).Sum(l => l.Valor));
        decimal saidas = Formato.Arredondar(periodo.Where(l => l.Tipo == TipoCaixa.Saida).Sum(l => l.Valor));
        decimal atual = FinanceiroService.SaldoCaixa();

        _kAnterior.Valor = Formato.Moeda(anterior);
        _kAnterior.Detalhe = $"até {Formato.Data(de.AddDays(-1))}";
        _kEntradas.Valor = Formato.Moeda(entradas);
        _kEntradas.Detalhe = Qtd(periodo.Count(l => l.Tipo == TipoCaixa.Entrada));
        _kSaidas.Valor = Formato.Moeda(saidas);
        _kSaidas.Detalhe = Qtd(periodo.Count(l => l.Tipo == TipoCaixa.Saida));
        _kPeriodo.Valor = Formato.Moeda(entradas - saidas);
        _kPeriodo.Detalhe = $"saldo final: {Formato.Moeda(anterior + entradas - saidas)}";
        _kPeriodo.Cor = entradas - saidas < 0 ? Tema.Perigo : Tema.Primaria;
        _kAtual.Valor = Formato.Moeda(atual);
        _kAtual.Detalhe = "todos os lançamentos";
        _kAtual.Cor = atual < 0 ? Tema.Perigo : Tema.Roxo;

        return busca.Length == 0 ? periodo : FinanceiroService.ListarCaixa(de, ate, busca);
    }

    private static string Qtd(int n) => n == 1 ? "1 lançamento" : $"{n} lançamentos";

    protected override string Rodape(IReadOnlyList<LancamentoCaixa> itens)
    {
        decimal e = itens.Where(l => l.Tipo == TipoCaixa.Entrada).Sum(l => l.Valor);
        decimal s = itens.Where(l => l.Tipo == TipoCaixa.Saida).Sum(l => l.Valor);
        return $"{Qtd(itens.Count)}   •   Entradas: {Formato.Moeda(e)}   •   Saídas: {Formato.Moeda(s)}   •   Saldo: {Formato.Moeda(e - s)}";
    }

    public static string TextoOrigem(string? origem) => origem switch
    {
        "MANUAL" => "Manual",
        "RECEBIMENTO" => "Recebimento",
        "PAGAMENTO" => "Pagamento",
        "ESTORNO" => "Estorno",
        _ => origem ?? ""
    };

    private void FormatarCelula(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        string col = Grid.Columns[e.ColumnIndex].Name;
        if (col == "Origem" && e.Value is string o)
        {
            e.Value = TextoOrigem(o);
            e.FormattingApplied = true;
        }
        else if (col == "ValorComSinal" && e.Value is decimal v && v < 0)
        {
            e.CellStyle.ForeColor = Tema.Perigo;
            e.CellStyle.SelectionForeColor = Tema.Perigo;
        }
    }

    protected override bool Novo()
    {
        using var dlg = new DialogoLancamento();
        return dlg.Abrir(FindForm());
    }

    protected override string ConfirmacaoExclusao(LancamentoCaixa item) =>
        $"Deseja excluir o lançamento \"{item.Descricao}\" ({Formato.Moeda(item.Valor)})?\n\nEsta operação não pode ser desfeita.";

    protected override void Excluir(LancamentoCaixa item)
    {
        if (item.Origem != "MANUAL")
            throw new RegraException("Só é possível excluir lançamentos manuais. Para os demais, estorne o título de origem.");
        FinanceiroService.ExcluirLancamentoManual(item.Id);
    }
}
