using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Financeiro;

/// <summary>Base comum das páginas de contas a receber e contas a pagar.</summary>
public abstract class PaginaTitulos : PaginaLista<Titulo>
{
    private static readonly (string Texto, string Codigo)[] Situacoes =
    {
        ("Em aberto", StatusTitulo.Aberto), ("Vencidos", "VENCIDO"), ("Pagos", StatusTitulo.Pago),
        ("Cancelados", StatusTitulo.Cancelado), ("Todos", "")
    };

    protected TipoTitulo Tipo { get; }
    private bool Receber => Tipo == TipoTitulo.Receber;

    private readonly ComboBox _cbSituacao;
    private readonly CheckBox _ckPeriodo;
    private readonly DateTimePicker _dtDe, _dtAte;
    private readonly CartaoKpi _kAberto, _kVencido, _kSemana, _kPagoMes;
    private readonly BotaoModerno _btnBaixar, _btnEstornar;

    // true quando a última interação foi no grid (duplo clique/Enter abre a baixa); false quando foi no botão Editar.
    private bool _interacaoNoGrid;

    protected override string NomeExportacao => Receber ? "contas-a-receber" : "contas-a-pagar";

    protected PaginaTitulos(TipoTitulo tipo)
    {
        Tipo = tipo;
        Titulo = Receber ? "Contas a Receber" : "Contas a Pagar";
        Subtitulo = Receber ? "Títulos a receber de clientes" : "Títulos a pagar a fornecedores";

        // ---- Cabeçalho: Novo, Editar, Receber/Pagar, Estornar, Cancelar título, Exportar
        BarraAcoes.Width = 800;
        BtnNovo.Text = "+ Novo título";
        BtnExcluir.Text = "Cancelar título";
        _btnBaixar = AdicionarAcao(Receber ? "Receber" : "Pagar", EstiloBotao.Sucesso, AcaoBaixar);
        _btnEstornar = AdicionarAcao("Estornar", EstiloBotao.Secundario, AcaoEstornar);
        // Fluxo da direita para a esquerda: índice 0 é o mais à direita (Exportar).
        BarraAcoes.Controls.SetChildIndex(_btnEstornar, 2);
        BarraAcoes.Controls.SetChildIndex(_btnBaixar, 3);

        // ---- Indicadores (acima dos filtros: maior índice = encaixado primeiro no topo)
        var kpis = new PainelKpis();
        _kAberto = kpis.Adicionar("Em aberto", Tema.Primaria);
        _kVencido = kpis.Adicionar("Vencido", Tema.Perigo);
        _kSemana = kpis.Adicionar("Vence em 7 dias", Tema.Aviso);
        _kPagoMes = kpis.Adicionar(Receber ? "Recebido no mês" : "Pago no mês", Tema.Sucesso);
        Conteudo.Controls.Add(kpis);

        // ---- Filtros
        TxtBusca.PlaceholderText = Receber ? "Buscar por descrição ou cliente..." : "Buscar por descrição ou fornecedor...";
        _cbSituacao = Ui.Combo(120);
        _cbSituacao.CarregarTextos(Situacoes.Select(s => s.Texto));
        _ckPeriodo = Ui.Check("Filtrar vencimento");
        var hoje = DateTime.Today;
        var ini = new DateTime(hoje.Year, hoje.Month, 1);
        _dtDe = Ui.Data(ini);
        _dtAte = Ui.Data(ini.AddMonths(1).AddDays(-1));
        _dtDe.Enabled = _dtAte.Enabled = false;
        var lblAte = Ui.Rotulo("até");
        lblAte.Margin = new Padding(0, 8, 6, 0);
        Filtros.Controls.Add(Ui.Rotulo("Situação"));
        Filtros.Controls.Add(_cbSituacao);
        Filtros.Controls.Add(_ckPeriodo);
        Filtros.Controls.Add(_dtDe);
        Filtros.Controls.Add(lblAte);
        Filtros.Controls.Add(_dtAte);

        RecarregarAoMudar(_cbSituacao);
        _ckPeriodo.CheckedChanged += (_, _) => { _dtDe.Enabled = _dtAte.Enabled = _ckPeriodo.Checked; Recarregar(); };
        _dtDe.ValueChanged += (_, _) => { if (_ckPeriodo.Checked) Recarregar(); };
        _dtAte.ValueChanged += (_, _) => { if (_ckPeriodo.Checked) Recarregar(); };

        // Distingue duplo clique/Enter no grid (abre a baixa) do botão Editar (abre a edição).
        Grid.MouseDown += (_, _) => _interacaoNoGrid = true;
        Grid.GotFocus += (_, _) => _interacaoNoGrid = true;
        BtnEditar.MouseDown += (_, _) => _interacaoNoGrid = false;
        BtnEditar.GotFocus += (_, _) => _interacaoNoGrid = false;

        Grid.CellFormatting += FormatarCelula;
    }

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.ColunaData("DataVencimento", "Vencimento");
        grid.Coluna("Descricao", "Descrição");
        grid.Coluna("PessoaNome", Receber ? "Cliente" : "Fornecedor", 190);
        grid.Coluna("ParcelaTexto", "Parcela", 70, alinhamento: DataGridViewContentAlignment.MiddleCenter);
        grid.ColunaMoeda("Valor", "Valor");
        grid.ColunaMoeda("ValorPago", "Pago");
        grid.ColunaMoeda("Saldo", "Saldo");
        grid.Coluna("FormaPagamento", "Forma", 130);
        grid.ColunaStatus("Situacao", "Situação", 95);
        grid.ColunaData("DataPagamento", Receber ? "Recebido em" : "Pago em");
    }

    protected override IEnumerable<Titulo> Carregar(string busca)
    {
        AtualizarKpis();
        int i = Math.Max(0, _cbSituacao.SelectedIndex);
        var filtro = new FiltroTitulos
        {
            Busca = busca,
            Situacao = Situacoes[i].Codigo,
            VencimentoDe = _ckPeriodo.Checked ? _dtDe.Value.Date : null,
            VencimentoAte = _ckPeriodo.Checked ? _dtAte.Value.Date : null
        };
        return FinanceiroService.Listar(Tipo, filtro);
    }

    private void AtualizarKpis()
    {
        var r = ConsultasFinanceiro.Resumo(Tipo);
        _kAberto.Valor = Formato.Moeda(r.EmAberto);
        _kAberto.Detalhe = Qtd(r.QtdAberto);
        _kVencido.Valor = Formato.Moeda(r.Vencido);
        _kVencido.Detalhe = r.QtdVencido == 0 ? "nenhum título vencido" : Qtd(r.QtdVencido);
        _kSemana.Valor = Formato.Moeda(r.VenceSemana);
        _kSemana.Detalhe = r.QtdVenceSemana == 0 ? "nada a vencer" : Qtd(r.QtdVenceSemana);
        _kPagoMes.Valor = Formato.Moeda(r.PagoMes);
        _kPagoMes.Detalhe = DateTime.Today.ToString("MMMM 'de' yyyy", Formato.PtBr);
    }

    private static string Qtd(int n) => n == 1 ? "1 título" : $"{n} títulos";

    protected override string Rodape(IReadOnlyList<Titulo> itens) =>
        $"{Qtd(itens.Count)}   •   Valor total: {Formato.Moeda(itens.Sum(t => t.Valor))}   •   Saldo: {Formato.Moeda(itens.Sum(t => t.Saldo))}";

    private void FormatarCelula(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        if (Grid.Rows[e.RowIndex].DataBoundItem is not Titulo t) return;
        string col = Grid.Columns[e.ColumnIndex].Name;
        if (t.Vencido && col is "DataVencimento" or "Saldo")
        {
            e.CellStyle.ForeColor = Tema.Perigo;
            e.CellStyle.SelectionForeColor = Tema.Perigo;
        }
        else if (t.Status == StatusTitulo.Cancelado && col != "Situacao")
        {
            e.CellStyle.ForeColor = Tema.TextoSuave;
            e.CellStyle.SelectionForeColor = Tema.TextoSuave;
        }
    }

    // ---------- Ações ----------

    protected override bool Novo()
    {
        using var dlg = new DialogoTitulo(Tipo, null);
        return dlg.Abrir(FindForm());
    }

    protected override bool Editar(Titulo item)
    {
        if (_interacaoNoGrid && item.Status == StatusTitulo.Aberto)
            return Baixar(item);

        if (item.OrigemId != null)
            throw new RegraException(Receber
                ? "Este título foi gerado por uma venda e não pode ser editado aqui."
                : "Este título foi gerado por uma compra e não pode ser editado aqui.");
        if (item.Status != StatusTitulo.Aberto || item.ValorPago > 0)
            throw new RegraException("Só é possível alterar títulos em aberto e sem pagamentos.");
        var atual = FinanceiroService.Obter(Tipo, item.Id) ?? throw new RegraException("Título não encontrado.");
        using var dlg = new DialogoTitulo(Tipo, atual);
        return dlg.Abrir(FindForm());
    }

    private bool Baixar(Titulo item)
    {
        if (item.Status != StatusTitulo.Aberto) throw new RegraException("Este título não está em aberto.");
        var atual = FinanceiroService.Obter(Tipo, item.Id) ?? throw new RegraException("Título não encontrado.");
        using var dlg = new DialogoBaixa(Tipo, atual);
        return dlg.Abrir(FindForm());
    }

    private void AcaoBaixar()
    {
        var item = Selecionado;
        if (item == null) { Mensagem.Info("Selecione um título."); return; }
        bool mudou = false;
        if (Mensagem.Tentar(() => mudou = Baixar(item)) && mudou) Recarregar();
    }

    private void AcaoEstornar()
    {
        var item = Selecionado;
        if (item == null) { Mensagem.Info("Selecione um título."); return; }
        if (item.ValorPago <= 0) { Mensagem.Aviso("Este título não possui pagamentos para estornar."); return; }
        string acao = Receber ? "recebimentos" : "pagamentos";
        if (!Mensagem.Confirmar($"Estornar os {acao} do título \"{item.Descricao}\"?\n\n" +
                                $"O valor de {Formato.Moeda(item.ValorPago)} será lançado como estorno no caixa e o título voltará a ficar em aberto."))
            return;
        if (Mensagem.Tentar(() => FinanceiroService.Estornar(Tipo, item.Id))) Recarregar();
    }

    protected override string ConfirmacaoExclusao(Titulo item) =>
        $"Deseja cancelar o título \"{item.Descricao}\" ({Formato.Moeda(item.Valor)})?\n\nO título deixará de ser cobrado e não poderá ser reaberto.";

    protected override void Excluir(Titulo item) => FinanceiroService.Cancelar(Tipo, item.Id);
}
