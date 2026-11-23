using System.ComponentModel;
using System.Drawing.Drawing2D;
using MeuERP.Nucleo;

namespace MeuERP.UI;

public enum EstiloBotao { Primario, Secundario, Sucesso, Perigo }

/// <summary>Botão plano com cantos arredondados. A largura se ajusta ao texto.</summary>
public class BotaoModerno : Button
{
    private bool _hover;
    private EstiloBotao _estilo = EstiloBotao.Primario;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EstiloBotao Estilo { get => _estilo; set { _estilo = value; Invalidate(); } }

    public BotaoModerno() : this("", EstiloBotao.Primario) { }

    public BotaoModerno(string texto, EstiloBotao estilo = EstiloBotao.Primario, Action? aoClicar = null)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _estilo = estilo;
        Font = Tema.FonteNegrito;
        Height = 34;
        Cursor = Cursors.Hand;
        Margin = new Padding(6, 0, 0, 0);
        Text = texto;
        if (aoClicar != null) Click += (_, _) => aoClicar();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Width = Math.Max(84, TextRenderer.MeasureText(Text, Font).Width + 30);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(CorDeFundo(Parent));

        Color fundo, texto, borda;
        switch (_estilo)
        {
            case EstiloBotao.Secundario: fundo = _hover ? Tema.Cabecalho : Color.White; texto = Tema.Texto; borda = ColorTranslator.FromHtml("#D1D5DB"); break;
            case EstiloBotao.Sucesso: fundo = _hover ? ColorTranslator.FromHtml("#15803D") : Tema.Sucesso; texto = Color.White; borda = fundo; break;
            case EstiloBotao.Perigo: fundo = _hover ? ColorTranslator.FromHtml("#B91C1C") : Tema.Perigo; texto = Color.White; borda = fundo; break;
            default: fundo = _hover ? Tema.PrimariaHover : Tema.Primaria; texto = Color.White; borda = fundo; break;
        }
        if (!Enabled) { fundo = ColorTranslator.FromHtml("#E5E7EB"); texto = ColorTranslator.FromHtml("#9CA3AF"); borda = fundo; }

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Tema.Arredondado(r, 6);
        using (var b = new SolidBrush(fundo)) g.FillPath(b, path);
        using (var p = new Pen(borda)) g.DrawPath(p, path);
        if (Focused && ShowFocusCues)
        {
            using var pf = new Pen(Tema.Texto) { DashStyle = DashStyle.Dot };
            var rf = Rectangle.Inflate(r, -3, -3);
            g.DrawRectangle(pf, rf);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, texto, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    /// <summary>Primeira cor de fundo opaca subindo pelos pais (painéis transparentes não têm cor própria).</summary>
    internal static Color CorDeFundo(Control? c)
    {
        for (; c != null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return Tema.Fundo;
    }
}

/// <summary>Painel branco com borda e cantos arredondados.</summary>
public class Cartao : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Titulo { get; set; } = "";

    public Cartao()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Cartao;
        Padding = new Padding(12);
    }

    public Cartao(string titulo) : this()
    {
        Titulo = titulo;
        Padding = new Padding(12, 44, 12, 12);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BotaoModerno.CorDeFundo(Parent));
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Tema.Arredondado(r, 8);
        using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
        using (var p = new Pen(Tema.Borda)) g.DrawPath(p, path);
        if (Titulo.Length > 0)
            TextRenderer.DrawText(g, Titulo, Tema.FonteSubtitulo, new Point(14, 12), Tema.Texto);
    }
}

/// <summary>Cartão de indicador: título, valor grande e detalhe, com faixa colorida à esquerda.</summary>
public class CartaoKpi : Cartao
{
    private string _valor = "", _detalhe = "", _rotulo = "";
    private Color _cor = Tema.Primaria;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Rotulo { get => _rotulo; set { _rotulo = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Valor { get => _valor; set { _valor = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Detalhe { get => _detalhe; set { _detalhe = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Cor { get => _cor; set { _cor = value; Invalidate(); } }

    public CartaoKpi() { Size = new Size(230, 104); Margin = new Padding(0, 0, 12, 12); }

    public CartaoKpi(string rotulo, Color cor) : this() { _rotulo = rotulo; _cor = cor; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        var g = e.Graphics;
        using (var b = new SolidBrush(_cor))
        using (var path = Tema.Arredondado(new Rectangle(0, 14, 4, Height - 28), 2))
            g.FillPath(b, path);
        var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, _rotulo, Tema.Fonte, new Rectangle(16, 12, Width - 28, 20), Tema.TextoSuave, flags);
        TextRenderer.DrawText(g, _valor, Tema.FonteKpi, new Rectangle(16, 34, Width - 28, 36), Tema.Texto, flags);
        TextRenderer.DrawText(g, _detalhe, Tema.FontePequena, new Rectangle(16, 76, Width - 28, 18), _cor, flags);
    }
}

/// <summary>Caixa de texto numérica (pt-BR) com propriedade <see cref="Valor"/> decimal.</summary>
public class CampoDecimal : TextBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Casas { get; set; } = 2;

    public CampoDecimal() { TextAlign = HorizontalAlignment.Right; Text = "0,00"; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal Valor
    {
        get => Formato.TentarDecimal(Text, out decimal v) ? v : 0;
        set => Text = value.ToString("N" + Casas, Formato.PtBr);
    }

    /// <summary>False se o texto digitado não é um número válido.</summary>
    public bool Valido => Formato.TentarDecimal(Text, out _);

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '.' && e.KeyChar != '-')
            e.Handled = true;
        base.OnKeyPress(e);
    }

    protected override void OnEnter(EventArgs e) { base.OnEnter(e); BeginInvoke(SelectAll); }

    protected override void OnLeave(EventArgs e)
    {
        if (Formato.TentarDecimal(Text, out decimal v)) Text = v.ToString("N" + Casas, Formato.PtBr);
        base.OnLeave(e);
    }
}

/// <summary>Item de ComboBox com Id opcional.</summary>
public record ItemCombo(int? Id, string Texto)
{
    public override string ToString() => Texto;
}

public static class ComboExtensoes
{
    /// <summary>Preenche o combo com pares (Id, Texto). Se <paramref name="textoVazio"/> for informado, adiciona um primeiro item sem Id.</summary>
    public static void Carregar(this ComboBox combo, IEnumerable<(int Id, string Texto)> itens, string? textoVazio = null)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        if (textoVazio != null) combo.Items.Add(new ItemCombo(null, textoVazio));
        foreach (var (id, texto) in itens) combo.Items.Add(new ItemCombo(id, texto));
        combo.EndUpdate();
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    /// <summary>Preenche o combo com textos simples.</summary>
    public static void CarregarTextos(this ComboBox combo, IEnumerable<string> itens, string? selecionado = null)
    {
        combo.BeginUpdate();
        combo.Items.Clear();
        foreach (string s in itens) combo.Items.Add(s);
        combo.EndUpdate();
        if (selecionado != null && combo.Items.Contains(selecionado)) combo.SelectedItem = selecionado;
        else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    public static int? IdSelecionado(this ComboBox combo) => (combo.SelectedItem as ItemCombo)?.Id;

    public static void SelecionarId(this ComboBox combo, int? id)
    {
        for (int i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is ItemCombo it && it.Id == id) { combo.SelectedIndex = i; return; }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }
}

/// <summary>
/// Formulário em grade: cada campo ocupa <c>span</c> colunas e tem o rótulo acima do controle.
/// Os campos são adicionados da esquerda para a direita e quebram de linha automaticamente.
/// Use Dock = Top (padrão); a altura é calculada sozinha.
/// </summary>
public class PainelFormulario : TableLayoutPanel
{
    private int _linha, _coluna;

    public PainelFormulario(int colunas = 4)
    {
        ColumnCount = colunas;
        for (int i = 0; i < colunas; i++) ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colunas));
        Dock = DockStyle.Top;
        BackColor = Color.Transparent;
        Height = 0;
        Margin = Padding.Empty;
    }

    /// <summary>Força o próximo campo a começar em uma nova linha.</summary>
    public void NovaLinha()
    {
        if (_coluna > 0) { _coluna = 0; _linha++; }
    }

    /// <summary>Adiciona um controle qualquer com rótulo. <paramref name="altura"/> é a altura total da célula.</summary>
    public T Adicionar<T>(string rotulo, T controle, int span = 1, int altura = 56) where T : Control
    {
        span = Math.Min(span, ColumnCount);
        if (_coluna + span > ColumnCount) { _coluna = 0; _linha++; }

        var celula = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 6), BackColor = Color.Transparent };
        controle.Font = Tema.Fonte;
        if (controle is CheckBox)
        {
            controle.Dock = DockStyle.Bottom;
            controle.Height = 28;
            celula.Controls.Add(controle);
        }
        else
        {
            var lbl = new Label { Text = rotulo, Dock = DockStyle.Top, Height = 20, Font = Tema.FontePequena, ForeColor = Tema.TextoSuave };
            controle.Dock = DockStyle.Fill;
            celula.Controls.Add(controle);
            celula.Controls.Add(lbl);
            controle.BringToFront();
        }

        while (RowStyles.Count <= _linha) RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        RowCount = Math.Max(RowCount, _linha + 1);
        if (RowStyles[_linha].Height < altura) RowStyles[_linha].Height = altura;
        Controls.Add(celula, _coluna, _linha);
        if (span > 1) SetColumnSpan(celula, span);
        _coluna += span;
        if (_coluna >= ColumnCount) { _coluna = 0; _linha++; }

        int total = 0;
        foreach (RowStyle rs in RowStyles) total += (int)rs.Height;
        Height = total;
        return controle;
    }

    public TextBox Texto(string rotulo, int span = 1, int maxLength = 0)
    {
        var t = new TextBox();
        if (maxLength > 0) t.MaxLength = maxLength;
        return Adicionar(rotulo, t, span);
    }

    public TextBox Senha(string rotulo, int span = 1) => Adicionar(rotulo, new TextBox { UseSystemPasswordChar = true }, span);

    public TextBox Multilinha(string rotulo, int span, int altura = 96) =>
        Adicionar(rotulo, new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true }, span, altura);

    public CampoDecimal Decimal(string rotulo, int span = 1, int casas = 2)
    {
        var c = new CampoDecimal { Casas = casas };
        c.Valor = 0;
        return Adicionar(rotulo, c, span);
    }

    public NumericUpDown Inteiro(string rotulo, int span = 1, int minimo = 0, int maximo = 9999, int valor = 0) =>
        Adicionar(rotulo, new NumericUpDown { Minimum = minimo, Maximum = maximo, Value = Math.Clamp(valor, minimo, maximo) }, span);

    /// <summary>ComboBox somente seleção (DropDownList).</summary>
    public ComboBox Combo(string rotulo, int span = 1) =>
        Adicionar(rotulo, new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, span);

    public DateTimePicker Data(string rotulo, int span = 1, DateTime? valor = null) =>
        Adicionar(rotulo, new DateTimePicker { Format = DateTimePickerFormat.Short, Value = valor ?? DateTime.Today }, span);

    public CheckBox Check(string texto, int span = 1, bool marcado = false) =>
        Adicionar("", new CheckBox { Text = texto, Checked = marcado }, span);

    /// <summary>Rótulo somente leitura com valor em destaque (ex.: totais).</summary>
    public Label Rotulo(string rotulo, int span = 1, string valor = "") =>
        Adicionar(rotulo, new Label { Text = valor, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Tema.Texto, AutoEllipsis = true }, span);
}

/// <summary>Controles avulsos já estilizados para barras de filtro.</summary>
public static class Ui
{
    public static Label Rotulo(string texto, bool suave = true) => new()
    {
        Text = texto, AutoSize = true, Font = Tema.Fonte, ForeColor = suave ? Tema.TextoSuave : Tema.Texto,
        Margin = new Padding(8, 8, 4, 0)
    };

    public static ComboBox Combo(int largura = 150) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = largura, Font = Tema.Fonte, Margin = new Padding(0, 4, 6, 0)
    };

    public static DateTimePicker Data(DateTime valor) => new()
    {
        Format = DateTimePickerFormat.Short, Width = 110, Font = Tema.Fonte, Value = valor, Margin = new Padding(0, 4, 6, 0)
    };

    public static TextBox Busca(string placeholder = "Buscar...", int largura = 260) => new()
    {
        PlaceholderText = placeholder, Width = largura, Font = Tema.Fonte, Margin = new Padding(0, 4, 6, 0)
    };

    public static CheckBox Check(string texto) => new()
    {
        Text = texto, AutoSize = true, Font = Tema.Fonte, ForeColor = Tema.Texto, Margin = new Padding(8, 7, 6, 0)
    };

    /// <summary>FlowLayoutPanel horizontal sem quebra, para barras de ferramentas.</summary>
    public static FlowLayoutPanel Barra(DockStyle dock = DockStyle.Top, int altura = 44, bool direitaParaEsquerda = false) => new()
    {
        Dock = dock, Height = altura, WrapContents = false, BackColor = Color.Transparent,
        FlowDirection = direitaParaEsquerda ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
        Padding = new Padding(0, 2, 0, 0)
    };
}
