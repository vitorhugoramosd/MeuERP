using System.ComponentModel;
using MeuERP.Nucleo;

namespace MeuERP.UI;

/// <summary>
/// Base de todas as páginas exibidas na área central da janela principal.
/// Tem um cabeçalho (título, subtítulo e <see cref="BarraAcoes"/> à direita) e a área <see cref="Conteudo"/>.
/// Uma nova instância é criada a cada navegação.
/// </summary>
public class PaginaBase : UserControl
{
    private readonly Label _lblTitulo, _lblSubtitulo;

    /// <summary>Botões de ação no canto superior direito (adicione com <see cref="AdicionarAcao"/>).</summary>
    protected FlowLayoutPanel BarraAcoes { get; }

    /// <summary>Área útil da página. Adicione seus controles aqui (não em this.Controls).</summary>
    protected Panel Conteudo { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Titulo { get => _lblTitulo.Text; set => _lblTitulo.Text = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Subtitulo { get => _lblSubtitulo.Text; set => _lblSubtitulo.Text = value; }

    public PaginaBase()
    {
        BackColor = Tema.Fundo;
        Font = Tema.Fonte;
        ForeColor = Tema.Texto;
        Dock = DockStyle.Fill;

        var cabecalho = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Tema.Fundo };
        _lblTitulo = new Label { AutoSize = true, Font = Tema.FonteTitulo, ForeColor = Tema.Texto, Location = new Point(22, 12) };
        _lblSubtitulo = new Label { AutoSize = true, Font = Tema.Fonte, ForeColor = Tema.TextoSuave, Location = new Point(25, 46) };
        BarraAcoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Right, Width = 620, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            Padding = new Padding(0, 22, 24, 0), BackColor = Tema.Fundo
        };
        cabecalho.Controls.Add(_lblTitulo);
        cabecalho.Controls.Add(_lblSubtitulo);
        cabecalho.Controls.Add(BarraAcoes);

        Conteudo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 0, 24, 20), BackColor = Tema.Fundo };
        Controls.Add(Conteudo);
        Controls.Add(cabecalho);
        Conteudo.BringToFront();
    }

    /// <summary>Adiciona um botão ao cabeçalho. O primeiro adicionado fica mais à direita.</summary>
    protected BotaoModerno AdicionarAcao(string texto, EstiloBotao estilo, Action aoClicar)
    {
        var b = new BotaoModerno(texto, estilo, aoClicar);
        BarraAcoes.Controls.Add(b);
        return b;
    }

    /// <summary>Chamado pela janela principal logo após a página ser exibida.</summary>
    public virtual void AoExibir() { }
}

/// <summary>
/// Página de listagem padrão: busca, filtros extras, botões Novo/Editar/Excluir/Exportar e grid.
/// Implemente <see cref="ConfigurarGrid"/> e <see cref="Carregar"/>; sobrescreva Novo/Editar/Excluir conforme necessário.
/// ATENÇÃO: ConfigurarGrid e Carregar são chamados só depois do construtor da classe derivada (no OnLoad).
/// </summary>
public abstract class PaginaLista<T> : PaginaBase where T : class
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 300 };
    private readonly Label _lblRodape;
    private bool _carregado;

    protected GridModerno Grid { get; }
    protected TextBox TxtBusca { get; }
    /// <summary>Área ao lado da busca para filtros extras (combos, datas, checks). Use os helpers de <see cref="Ui"/>.</summary>
    protected FlowLayoutPanel Filtros { get; }
    protected BotaoModerno BtnNovo { get; }
    protected BotaoModerno BtnEditar { get; }
    protected BotaoModerno BtnExcluir { get; }
    protected BotaoModerno BtnExportar { get; }

    /// <summary>Nome usado no arquivo exportado.</summary>
    protected virtual string NomeExportacao => Titulo.ToLowerInvariant().Replace(' ', '-');

    protected PaginaLista()
    {
        // Botões no cabeçalho (ordem visual: Novo, Editar, Excluir, Exportar)
        BtnExportar = AdicionarAcao("Exportar", EstiloBotao.Secundario, () => Grid!.ExportarCsv(NomeExportacao));
        BtnExcluir = AdicionarAcao("Excluir", EstiloBotao.Secundario, AcaoExcluir);
        BtnEditar = AdicionarAcao("Editar", EstiloBotao.Secundario, AcaoEditar);
        BtnNovo = AdicionarAcao("+ Novo", EstiloBotao.Primario, AcaoNovo);

        Filtros = Ui.Barra(DockStyle.Top, 42);
        TxtBusca = Ui.Busca();
        Filtros.Controls.Add(TxtBusca);

        Grid = new GridModerno();
        var cartao = new Cartao { Dock = DockStyle.Fill, Padding = new Padding(6) };
        cartao.Controls.Add(Grid);

        _lblRodape = new Label { Dock = DockStyle.Bottom, Height = 24, ForeColor = Tema.TextoSuave, Font = Tema.FontePequena, TextAlign = ContentAlignment.BottomLeft };

        Conteudo.Controls.Add(cartao);
        Conteudo.Controls.Add(Filtros);
        Conteudo.Controls.Add(_lblRodape);
        cartao.BringToFront();

        _timer.Tick += (_, _) => { _timer.Stop(); Recarregar(); };
        TxtBusca.TextChanged += (_, _) => { _timer.Stop(); _timer.Start(); };
        TxtBusca.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _timer.Stop(); Recarregar(); }
            else if (e.KeyCode == Keys.Down) { Grid.Focus(); }
        };
        Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AcaoEditar(); };
        Grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.Handled = true; AcaoEditar(); }
            else if (e.KeyCode == Keys.Delete && BtnExcluir.Visible) AcaoExcluir();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_carregado) return;
        _carregado = true;
        ConfigurarGrid(Grid);
        Recarregar();
        TxtBusca.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Defina aqui as colunas do grid.</summary>
    protected abstract void ConfigurarGrid(GridModerno grid);

    /// <summary>Retorne os registros conforme o texto de busca e os filtros extras.</summary>
    protected abstract IEnumerable<T> Carregar(string busca);

    /// <summary>Abra o diálogo de inclusão. Retorne true se algo foi gravado.</summary>
    protected virtual bool Novo() => false;

    /// <summary>Abra o diálogo de edição. Retorne true se algo foi gravado.</summary>
    protected virtual bool Editar(T item) => false;

    /// <summary>Exclua o item (a confirmação já foi feita). Lance RegraException para avisos.</summary>
    protected virtual void Excluir(T item) { }

    /// <summary>Texto da pergunta de confirmação de exclusão.</summary>
    protected virtual string ConfirmacaoExclusao(T item) => "Deseja realmente excluir o registro selecionado?";

    /// <summary>Texto do rodapé (por padrão, a contagem de registros).</summary>
    protected virtual string Rodape(IReadOnlyList<T> itens) => itens.Count == 1 ? "1 registro" : $"{itens.Count} registros";

    protected T? Selecionado => Grid.Selecionado<T>();

    /// <summary>Recarrega o grid mantendo, se possível, a linha selecionada.</summary>
    public void Recarregar()
    {
        if (!_carregado) return;
        int linha = Grid.CurrentRow?.Index ?? 0;
        List<T> itens;
        try { itens = Carregar(TxtBusca.Text.Trim()).ToList(); }
        catch (Exception ex) { Mensagem.Erro("Erro ao carregar os dados:\n\n" + ex.Message); return; }
        Grid.Carregar(itens);
        if (Grid.Rows.Count > 0 && Grid.Columns.GetColumnCount(DataGridViewElementStates.Visible) > 0)
        {
            linha = Math.Min(linha, Grid.Rows.Count - 1);
            var col = Grid.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
            if (col != null) Grid.CurrentCell = Grid.Rows[linha].Cells[col.Index];
        }
        _lblRodape.Text = Rodape(itens);
    }

    /// <summary>Recarrega quando o valor de um filtro extra mudar.</summary>
    protected void RecarregarAoMudar(params Control[] controles)
    {
        foreach (var c in controles)
        {
            switch (c)
            {
                case ComboBox cb: cb.SelectedIndexChanged += (_, _) => Recarregar(); break;
                case DateTimePicker dp: dp.ValueChanged += (_, _) => Recarregar(); break;
                case CheckBox ck: ck.CheckedChanged += (_, _) => Recarregar(); break;
                default: c.TextChanged += (_, _) => Recarregar(); break;
            }
        }
    }

    private void AcaoNovo()
    {
        bool mudou = false;
        if (Mensagem.Tentar(() => mudou = Novo()) && mudou) Recarregar();
    }

    private void AcaoEditar()
    {
        if (!BtnEditar.Visible) return;
        var item = Selecionado;
        if (item == null) { Mensagem.Info("Selecione um registro."); return; }
        bool mudou = false;
        if (Mensagem.Tentar(() => mudou = Editar(item)) && mudou) Recarregar();
    }

    private void AcaoExcluir()
    {
        var item = Selecionado;
        if (item == null) { Mensagem.Info("Selecione um registro."); return; }
        if (!Mensagem.Confirmar(ConfirmacaoExclusao(item))) return;
        if (Mensagem.Tentar(() => Excluir(item))) Recarregar();
    }
}

/// <summary>
/// Diálogo modal padrão: cabeçalho com título, área <see cref="Corpo"/> e rodapé com Salvar/Cancelar.
/// Sobrescreva <see cref="AoSalvar"/>; lance <see cref="RegraException"/> para validações (o diálogo permanece aberto).
/// </summary>
public class DialogoBase : Form
{
    /// <summary>Área do formulário. Adicione aqui um ou mais <see cref="PainelFormulario"/> (Dock = Top).</summary>
    protected Panel Corpo { get; }
    protected BotaoModerno BtnSalvar { get; }
    protected BotaoModerno BtnCancelar { get; }
    /// <summary>Rodapé (botões alinhados à direita). Pode receber botões extras.</summary>
    protected FlowLayoutPanel Rodape { get; }

    public DialogoBase() : this("", 640, 480) { }

    public DialogoBase(string titulo, int largura, int altura)
    {
        Text = titulo;
        ClientSize = new Size(largura, altura);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = Tema.Fonte;
        ForeColor = Tema.Texto;

        var cabecalho = new Label
        {
            Text = titulo, Dock = DockStyle.Top, Height = 52, Font = Tema.FonteSubtitulo, ForeColor = Tema.Texto,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 0, 0), BackColor = Tema.Cabecalho
        };

        Rodape = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            Padding = new Padding(0, 12, 16, 0), BackColor = Tema.Cabecalho
        };
        BtnCancelar = new BotaoModerno("Cancelar", EstiloBotao.Secundario);
        BtnSalvar = new BotaoModerno("Salvar", EstiloBotao.Primario);
        Rodape.Controls.Add(BtnCancelar);
        Rodape.Controls.Add(BtnSalvar);

        Corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 10, 8), BackColor = Color.White, AutoScroll = true };

        Controls.Add(Corpo);
        Controls.Add(cabecalho);
        Controls.Add(Rodape);
        Corpo.BringToFront();

        AcceptButton = BtnSalvar;
        CancelButton = BtnCancelar;
        BtnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        BtnSalvar.Click += (_, _) =>
        {
            if (Mensagem.Tentar(AoSalvar)) { DialogResult = DialogResult.OK; Close(); }
        };
    }

    /// <summary>Valide e grave. Lance RegraException para manter o diálogo aberto com um aviso.</summary>
    protected virtual void AoSalvar() { }

    /// <summary>Atalho: abre o diálogo modal e retorna true se o usuário salvou.</summary>
    public bool Abrir(IWin32Window? dono = null) => ShowDialog(dono ?? ActiveForm) == DialogResult.OK;
}

/// <summary>Diálogo genérico de pesquisa e seleção de um registro (produto, cliente, fornecedor...).</summary>
public class SeletorDialogo<T> : Form where T : class
{
    private readonly GridModerno _grid = new();
    private readonly TextBox _txt;
    private readonly Func<string, IEnumerable<T>> _buscar;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };

    public T? Selecionado { get; private set; }

    public SeletorDialogo(string titulo, Func<string, IEnumerable<T>> buscar, Action<GridModerno> colunas, string buscaInicial = "")
    {
        _buscar = buscar;
        Text = titulo;
        ClientSize = new Size(760, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;
        Font = Tema.Fonte;
        KeyPreview = true;

        var topo = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 12, 12, 6) };
        _txt = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Digite para pesquisar...", Text = buscaInicial, Font = Tema.Fonte };
        topo.Controls.Add(_txt);

        var dica = new Label
        {
            Dock = DockStyle.Bottom, Height = 28, Text = "  Enter ou duplo clique para selecionar  •  Esc para cancelar",
            ForeColor = Tema.TextoSuave, Font = Tema.FontePequena, TextAlign = ContentAlignment.MiddleLeft, BackColor = Tema.Cabecalho
        };
        var meio = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 8) };
        meio.Controls.Add(_grid);
        colunas(_grid);

        Controls.Add(meio);
        Controls.Add(topo);
        Controls.Add(dica);
        meio.BringToFront();

        _timer.Tick += (_, _) => { _timer.Stop(); Carregar(); };
        _txt.TextChanged += (_, _) => { _timer.Stop(); _timer.Start(); };
        _txt.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down) { _grid.Focus(); e.Handled = true; }
        };
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Confirmar(); };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _timer.Stop(); if (_txt.Focused && _grid.Rows.Count == 0) Carregar(); Confirmar(); }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Shown += (_, _) => { Carregar(); _txt.Focus(); _txt.SelectionStart = _txt.TextLength; };
        FormClosed += (_, _) => _timer.Dispose();
    }

    private void Carregar()
    {
        Mensagem.Tentar(() => _grid.Carregar(_buscar(_txt.Text.Trim())));
    }

    private void Confirmar()
    {
        var item = _grid.Selecionado<T>();
        if (item == null) return;
        Selecionado = item;
        DialogResult = DialogResult.OK;
        Close();
    }
}

public static class Seletor
{
    /// <summary>Abre o diálogo de pesquisa e devolve o item escolhido (ou null se cancelado).</summary>
    public static T? Escolher<T>(string titulo, Func<string, IEnumerable<T>> buscar, Action<GridModerno> colunas, string buscaInicial = "") where T : class
    {
        using var dlg = new SeletorDialogo<T>(titulo, buscar, colunas, buscaInicial);
        return dlg.ShowDialog(Form.ActiveForm) == DialogResult.OK ? dlg.Selecionado : null;
    }

    public static Models.Produto? Produto(string buscaInicial = "") => Escolher<Models.Produto>("Selecionar produto",
        b => Dados.ProdutoRepository.Listar(b, somenteAtivos: true),
        g =>
        {
            g.Coluna("Codigo", "Código", 90);
            g.Coluna("Descricao", "Descrição");
            g.Coluna("Unidade", "Un.", 50);
            g.ColunaNumero("Estoque", "Estoque", 80);
            g.ColunaMoeda("PrecoCusto", "Custo", 95);
            g.ColunaMoeda("PrecoVenda", "Preço", 95);
        }, buscaInicial);

    public static Models.Cliente? Cliente() => Escolher<Models.Cliente>("Selecionar cliente",
        b => Dados.ClienteRepository.Listar(b, somenteAtivos: true),
        g =>
        {
            g.Coluna("Nome", "Nome / Razão social");
            g.Coluna("DocumentoFormatado", "CPF/CNPJ", 140);
            g.Coluna("Telefone", "Telefone", 120);
            g.Coluna("CidadeUf", "Cidade", 150);
        });

    public static Models.Fornecedor? Fornecedor() => Escolher<Models.Fornecedor>("Selecionar fornecedor",
        b => Dados.FornecedorRepository.Listar(b, somenteAtivos: true),
        g =>
        {
            g.Coluna("Nome", "Razão social / Nome");
            g.Coluna("DocumentoFormatado", "CNPJ/CPF", 140);
            g.Coluna("Telefone", "Telefone", 120);
            g.Coluna("CidadeUf", "Cidade", 150);
        });
}
