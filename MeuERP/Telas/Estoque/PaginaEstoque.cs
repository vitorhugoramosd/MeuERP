using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.Telas.Financeiro;
using MeuERP.UI;

namespace MeuERP.Telas.Estoque;

/// <summary>Posição de estoque e histórico de movimentações.</summary>
public class PaginaEstoque : PaginaBase
{
    private static readonly (string Texto, string Codigo)[] TiposFiltro =
    {
        ("Todos", ""), ("Entrada", TipoMov.Entrada), ("Saída", TipoMov.Saida), ("Ajuste", TipoMov.Ajuste)
    };

    private readonly CartaoKpi _kAtivos, _kItens, _kValor, _kBaixo;
    private readonly BotaoModerno _btnPosicao, _btnMov, _btnNova;

    // Visão 1: posição
    private readonly Panel _visaoPosicao;
    private readonly TextBox _txtBuscaPos;
    private readonly CheckBox _ckBaixo;
    private readonly GridModerno _gridPos = new();
    private readonly Label _rodapePos;

    // Visão 2: movimentações
    private readonly Panel _visaoMov;
    private readonly DateTimePicker _dtDe, _dtAte;
    private readonly ComboBox _cbTipo;
    private readonly TextBox _txtBuscaMov;
    private readonly Label _lblFiltro;
    private readonly BotaoModerno _btnLimparFiltro;
    private readonly GridModerno _gridMov = new();
    private readonly Label _rodapeMov;

    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 300 };
    private Produto? _produtoFiltro;
    private bool _carregado;
    private bool _emMovimentacoes;

    public PaginaEstoque()
    {
        Titulo = "Estoque";
        Subtitulo = "Posição atual e histórico de movimentações";

        AdicionarAcao("Exportar", EstiloBotao.Secundario, Exportar);
        _btnNova = AdicionarAcao("+ Nova movimentação", EstiloBotao.Primario, NovaMovimentacao);
        _btnNova.Visible = Sessao.PodeGerenciar;

        // ---- KPIs
        var kpis = new PainelKpis();
        _kAtivos = kpis.Adicionar("Produtos ativos", Tema.Primaria);
        _kItens = kpis.Adicionar("Itens em estoque", Tema.Roxo);
        _kValor = kpis.Adicionar("Valor em estoque", Tema.Sucesso);
        _kBaixo = kpis.Adicionar("Abaixo do mínimo", Tema.Aviso);

        // ---- Alternância de visões
        var barraVisoes = Ui.Barra(DockStyle.Top, 46);
        _btnPosicao = new BotaoModerno("Posição de estoque", EstiloBotao.Primario, () => MostrarVisao(false)) { Margin = new Padding(0, 0, 0, 0) };
        _btnMov = new BotaoModerno("Movimentações", EstiloBotao.Secundario, () => MostrarVisao(true)) { Margin = new Padding(6, 0, 0, 0) };
        barraVisoes.Controls.Add(_btnPosicao);
        barraVisoes.Controls.Add(_btnMov);

        // ---- Visão 1: posição de estoque
        var filtrosPos = Ui.Barra(DockStyle.Top, 42);
        _txtBuscaPos = Ui.Busca("Buscar por código, descrição ou código de barras...", 320);
        _ckBaixo = Ui.Check("Somente abaixo do mínimo");
        filtrosPos.Controls.Add(_txtBuscaPos);
        filtrosPos.Controls.Add(_ckBaixo);
        _rodapePos = NovoRodape();
        _visaoPosicao = MontarVisao(filtrosPos, _gridPos, _rodapePos);

        // ---- Visão 2: movimentações
        var filtrosMov = Ui.Barra(DockStyle.Top, 42);
        _txtBuscaMov = Ui.Busca("Buscar produto...", 220);
        _dtDe = Ui.Data(DateTime.Today.AddDays(-30));
        _dtAte = Ui.Data(DateTime.Today);
        var lblAte = Ui.Rotulo("até");
        lblAte.Margin = new Padding(0, 8, 6, 0);
        _cbTipo = Ui.Combo(110);
        _cbTipo.CarregarTextos(TiposFiltro.Select(t => t.Texto));
        _lblFiltro = Ui.Rotulo("", suave: false);
        _lblFiltro.Font = Tema.FonteNegrito;
        _lblFiltro.ForeColor = Tema.Primaria;
        _lblFiltro.Margin = new Padding(14, 8, 0, 0);
        _btnLimparFiltro = new BotaoModerno("Limpar filtro", EstiloBotao.Secundario, () => FiltrarProduto(null))
        {
            Height = 30, Margin = new Padding(6, 2, 0, 0)
        };
        _lblFiltro.Visible = _btnLimparFiltro.Visible = false;
        filtrosMov.Controls.Add(_txtBuscaMov);
        filtrosMov.Controls.Add(Ui.Rotulo("Período"));
        filtrosMov.Controls.Add(_dtDe);
        filtrosMov.Controls.Add(lblAte);
        filtrosMov.Controls.Add(_dtAte);
        filtrosMov.Controls.Add(Ui.Rotulo("Tipo"));
        filtrosMov.Controls.Add(_cbTipo);
        filtrosMov.Controls.Add(_lblFiltro);
        filtrosMov.Controls.Add(_btnLimparFiltro);
        _rodapeMov = NovoRodape();
        _visaoMov = MontarVisao(filtrosMov, _gridMov, _rodapeMov);
        _visaoMov.Visible = false;

        // Ordem visual (de cima para baixo): KPIs, alternância, visão (Fill).
        Conteudo.Controls.Add(_visaoPosicao);
        Conteudo.Controls.Add(_visaoMov);
        Conteudo.Controls.Add(barraVisoes);
        Conteudo.Controls.Add(kpis);
        _visaoPosicao.BringToFront();
        _visaoMov.BringToFront();

        ConfigurarGrids();

        // ---- Eventos
        _timer.Tick += (_, _) => { _timer.Stop(); if (_emMovimentacoes) CarregarMovimentacoes(); else CarregarPosicao(); };
        _txtBuscaPos.TextChanged += (_, _) => ReiniciarTimer();
        _txtBuscaMov.TextChanged += (_, _) => ReiniciarTimer();
        _txtBuscaPos.KeyDown += (_, e) => TeclaBusca(e, _gridPos, CarregarPosicao);
        _txtBuscaMov.KeyDown += (_, e) => TeclaBusca(e, _gridMov, CarregarMovimentacoes);
        _ckBaixo.CheckedChanged += (_, _) => CarregarPosicao();
        _dtDe.ValueChanged += (_, _) => CarregarMovimentacoes();
        _dtAte.ValueChanged += (_, _) => CarregarMovimentacoes();
        _cbTipo.SelectedIndexChanged += (_, _) => CarregarMovimentacoes();

        _gridPos.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AbrirHistoricoSelecionado(); };
        _gridPos.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; AbrirHistoricoSelecionado(); } };
        _gridPos.CellFormatting += FormatarPosicao;
        _gridMov.CellFormatting += FormatarMovimentacao;
    }

    private static Label NovoRodape() => new()
    {
        Dock = DockStyle.Bottom, Height = 24, ForeColor = Tema.TextoSuave, Font = Tema.FontePequena, TextAlign = ContentAlignment.BottomLeft
    };

    /// <summary>Monta uma visão: barra de filtros no topo, grid em cartão e rodapé (mesmo layout de PaginaLista).</summary>
    private static Panel MontarVisao(FlowLayoutPanel filtros, GridModerno grid, Label rodape)
    {
        var painel = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo };
        var cartao = new Cartao { Dock = DockStyle.Fill, Padding = new Padding(6) };
        cartao.Controls.Add(grid);
        painel.Controls.Add(cartao);
        painel.Controls.Add(filtros);
        painel.Controls.Add(rodape);
        cartao.BringToFront();
        return painel;
    }

    private void ConfigurarGrids()
    {
        var g = _gridPos;
        g.Coluna("Codigo", "Código", 90);
        g.Coluna("Descricao", "Descrição");
        g.Coluna("CategoriaNome", "Categoria", 140);
        g.Coluna("Unidade", "Un.", 50);
        g.ColunaNumero("Saldo", "Estoque", 90);
        g.ColunaNumero("EstoqueMinimo", "Mínimo", 90);
        g.ColunaMoeda("PrecoCusto", "Custo", 105);
        g.ColunaMoeda("ValorEmEstoque", "Valor em estoque", 130);
        var sit = g.Coluna("Situacao", "Situação", 90);
        sit.DefaultCellStyle.Font = Tema.FonteNegrito;

        var m = _gridMov;
        m.ColunaData("DataMovimento", "Data", comHora: true);
        m.Coluna("Produto", "Produto");
        m.ColunaStatus("Tipo", "Tipo", 85);
        m.ColunaNumero("Quantidade", "Quantidade", 95);
        m.ColunaNumero("SaldoAnterior", "Saldo anterior", 105);
        m.ColunaNumero("SaldoPosterior", "Saldo posterior", 110);
        m.Coluna("Origem", "Origem", 140);
        m.Coluna("Observacao", "Observação", 200);
        m.Coluna("UsuarioNome", "Usuário", 120);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_carregado) return;
        _carregado = true;
        RecarregarTudo();
        _txtBuscaPos.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    private void ReiniciarTimer() { _timer.Stop(); _timer.Start(); }

    private void TeclaBusca(KeyEventArgs e, GridModerno grid, Action carregar)
    {
        if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _timer.Stop(); carregar(); }
        else if (e.KeyCode == Keys.Down) grid.Focus();
    }

    private void RecarregarTudo()
    {
        AtualizarKpis();
        CarregarPosicao();
        CarregarMovimentacoes();
    }

    // ---------- KPIs ----------

    private void AtualizarKpis()
    {
        List<Produto> ativos;
        try { ativos = ProdutoRepository.Listar(somenteAtivos: true); }
        catch (Exception ex) { Mensagem.Erro("Erro ao carregar os indicadores:\n\n" + ex.Message); return; }

        int baixo = ativos.Count(p => p.EstoqueBaixo);
        _kAtivos.Valor = ativos.Count.ToString("N0", Formato.PtBr);
        _kAtivos.Detalhe = $"{ativos.Count(p => p.Estoque <= 0)} sem saldo";
        _kItens.Valor = Formato.Qtd(ativos.Sum(p => p.Estoque));
        _kItens.Detalhe = "soma dos saldos";
        _kValor.Valor = Formato.Moeda(Formato.Arredondar(ativos.Sum(p => p.Estoque * p.PrecoCusto)));
        _kValor.Detalhe = "a preço de custo";
        _kBaixo.Valor = baixo.ToString("N0", Formato.PtBr);
        _kBaixo.Cor = baixo > 0 ? Tema.Perigo : Tema.Sucesso;
        _kBaixo.Detalhe = baixo > 0 ? "precisam de reposição" : "tudo em ordem";
    }

    // ---------- Posição ----------

    private void CarregarPosicao()
    {
        if (!_carregado) return;
        List<LinhaPosicao> linhas;
        try
        {
            linhas = ProdutoRepository.Listar(_txtBuscaPos.Text.Trim(), somenteAtivos: true, somenteEstoqueBaixo: _ckBaixo.Checked)
                .Select(p => new LinhaPosicao(p)).ToList();
        }
        catch (Exception ex) { Mensagem.Erro("Erro ao carregar os dados:\n\n" + ex.Message); return; }
        CarregarMantendoLinha(_gridPos, linhas);
        _rodapePos.Text = $"{Plural(linhas.Count, "produto", "produtos")}   •   " +
                          $"Itens: {Formato.Qtd(linhas.Sum(l => l.Saldo))}   •   " +
                          $"Valor em estoque: {Formato.Moeda(linhas.Sum(l => l.ValorEmEstoque))}";
    }

    private void FormatarPosicao(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        if (_gridPos.Rows[e.RowIndex].DataBoundItem is not LinhaPosicao l) return;
        string col = _gridPos.Columns[e.ColumnIndex].Name;
        Color? cor = null;
        if (col == "Situacao")
            cor = l.Situacao switch { "Zerado" => Tema.Perigo, "Baixo" => Tema.Perigo, _ => Tema.Sucesso };
        else if (col == "Saldo" && l.Situacao != "Normal")
            cor = Tema.Perigo;
        if (cor is Color c) { e.CellStyle.ForeColor = c; e.CellStyle.SelectionForeColor = c; }
    }

    private void AbrirHistoricoSelecionado()
    {
        var l = _gridPos.Selecionado<LinhaPosicao>();
        if (l == null) return;
        FiltrarProduto(l.Produto);
        MostrarVisao(true);
    }

    // ---------- Movimentações ----------

    private void FiltrarProduto(Produto? p)
    {
        _produtoFiltro = p;
        _lblFiltro.Text = p == null ? "" : $"Filtrando: {p.Codigo} - {p.Descricao}";
        _lblFiltro.Visible = _btnLimparFiltro.Visible = p != null;
        CarregarMovimentacoes();
    }

    private void CarregarMovimentacoes()
    {
        if (!_carregado) return;
        DateTime de = _dtDe.Value.Date, ate = _dtAte.Value.Date;
        if (ate < de) (de, ate) = (ate, de);
        int i = Math.Max(0, _cbTipo.SelectedIndex);
        List<LinhaMovimentacao> linhas;
        try
        {
            linhas = EstoqueService.Historico(de, ate, _produtoFiltro?.Id, TiposFiltro[i].Codigo, _txtBuscaMov.Text.Trim())
                .Select(m => new LinhaMovimentacao(m)).ToList();
        }
        catch (Exception ex) { Mensagem.Erro("Erro ao carregar os dados:\n\n" + ex.Message); return; }
        CarregarMantendoLinha(_gridMov, linhas);

        int entradas = linhas.Count(l => l.Tipo == TipoMov.Entrada);
        int saidas = linhas.Count(l => l.Tipo == TipoMov.Saida);
        int ajustes = linhas.Count(l => l.Tipo == TipoMov.Ajuste);
        _rodapeMov.Text = $"{Plural(linhas.Count, "movimentação", "movimentações")}   •   " +
                          $"Entradas: {entradas}   •   Saídas: {saidas}   •   Ajustes: {ajustes}";
    }

    private void FormatarMovimentacao(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        if (_gridMov.Columns[e.ColumnIndex].Name != "Quantidade") return;
        if (_gridMov.Rows[e.RowIndex].DataBoundItem is not LinhaMovimentacao l) return;
        // Sinal na quantidade para leitura rápida (ajuste mostra a variação real do saldo).
        decimal delta = l.SaldoPosterior - l.SaldoAnterior;
        string sinal = delta > 0 ? "+" : delta < 0 ? "−" : "";
        e.Value = sinal + Formato.Qtd(l.Quantidade);
        e.CellStyle.ForeColor = delta < 0 ? Tema.Perigo : delta > 0 ? Tema.Sucesso : Tema.Texto;
        e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
        e.FormattingApplied = true;
    }

    // ---------- Comum ----------

    private void MostrarVisao(bool movimentacoes)
    {
        _emMovimentacoes = movimentacoes;
        _visaoMov.Visible = movimentacoes;
        _visaoPosicao.Visible = !movimentacoes;
        _btnMov.Estilo = movimentacoes ? EstiloBotao.Primario : EstiloBotao.Secundario;
        _btnPosicao.Estilo = movimentacoes ? EstiloBotao.Secundario : EstiloBotao.Primario;
        (movimentacoes ? _gridMov : _gridPos).Focus();
    }

    private static void CarregarMantendoLinha<TLinha>(GridModerno grid, List<TLinha> linhas)
    {
        int linha = grid.CurrentRow?.Index ?? 0;
        grid.Carregar(linhas);
        if (grid.Rows.Count == 0) return;
        var col = grid.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
        if (col != null) grid.CurrentCell = grid.Rows[Math.Min(linha, grid.Rows.Count - 1)].Cells[col.Index];
    }

    private static string Plural(int n, string singular, string plural) => n == 1 ? $"1 {singular}" : $"{n.ToString("N0", Formato.PtBr)} {plural}";

    private void Exportar()
    {
        if (_emMovimentacoes) _gridMov.ExportarCsv("estoque-movimentacoes");
        else _gridPos.ExportarCsv("estoque-posicao");
    }

    private void NovaMovimentacao()
    {
        if (!Sessao.PodeGerenciar) return;
        Produto? inicial = null;
        if (_produtoFiltro != null) inicial = ProdutoRepository.Obter(_produtoFiltro.Id);
        using var dlg = new DialogoMovimentacao(inicial);
        if (!dlg.Abrir(FindForm())) return;
        if (_produtoFiltro != null)
        {
            var atualizado = ProdutoRepository.Obter(_produtoFiltro.Id);
            if (atualizado != null) _produtoFiltro = atualizado;
        }
        RecarregarTudo();
    }
}
