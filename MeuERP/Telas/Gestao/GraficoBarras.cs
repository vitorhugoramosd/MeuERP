using System.Drawing.Drawing2D;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Gestao;

/// <summary>
/// Gráfico de barras simples desenhado com GDI+: grade no eixo Y com valores abreviados, rótulos do eixo X
/// sem sobreposição, destaque com dica (data e valor) ao passar o mouse e estado vazio.
/// </summary>
public class GraficoBarras : Control
{
    private List<PontoSerie> _pontos = new();
    private bool _mensal;
    private int _hover = -1;
    private readonly List<RectangleF> _barras = new();
    private readonly List<RectangleF> _colunas = new();

    /// <summary>Texto exibido quando não há valores.</summary>
    public string TextoVazio { get; set; } = "Sem vendas no período";

    public GraficoBarras()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Tema.Cartao;
        Font = Tema.FontePequena;
    }

    /// <summary>Define os pontos. <paramref name="mensal"/> muda o formato dos rótulos (mês/ano em vez de dia/mês).</summary>
    public void Definir(IEnumerable<PontoSerie> pontos, bool mensal)
    {
        _pontos = pontos.ToList();
        _mensal = mensal;
        _hover = -1;
        Invalidate();
    }

    /// <summary>Valor abreviado para o eixo: 850, 1,5 mil, 12 mil, 1,2 mi.</summary>
    public static string Abreviar(decimal v)
    {
        decimal a = Math.Abs(v);
        if (a >= 1_000_000) return (v / 1_000_000).ToString("#,##0.#", Formato.PtBr) + " mi";
        if (a >= 1_000) return (v / 1_000).ToString("#,##0.#", Formato.PtBr) + " mil";
        return v.ToString("#,##0", Formato.PtBr);
    }

    /// <summary>Calcula um teto "redondo" e o passo da grade (4 ou 5 divisões).</summary>
    private static (decimal teto, decimal passo, int divisoes) Escala(decimal max)
    {
        if (max <= 0) return (4, 1, 4);
        foreach (int div in new[] { 4, 5 })
        {
            double bruto = (double)max / div;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(bruto)));
            foreach (double f in new[] { 1, 2, 2.5, 5, 10 })
            {
                double passo = f * mag;
                if (passo * div >= (double)max) return ((decimal)(passo * div), (decimal)passo, div);
            }
        }
        return (max, max / 4, 4);
    }

    private string RotuloX(DateTime d) => _mensal
        ? d.ToString("MMM/yy", Formato.PtBr).Replace(".", "")
        : d.ToString("dd/MM", Formato.PtBr);

    private string RotuloDica(PontoSerie p)
    {
        string quando = _mensal
            ? Formato.PtBr.TextInfo.ToTitleCase(p.Data.ToString("MMMM 'de' yyyy", Formato.PtBr))
            : p.Data.ToString("ddd, dd/MM/yyyy", Formato.PtBr);
        return quando;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        _barras.Clear();
        _colunas.Clear();

        if (_pontos.Count == 0 || _pontos.All(p => p.Valor <= 0))
        {
            TextRenderer.DrawText(g, TextoVazio, Tema.Fonte, ClientRectangle, Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var (teto, passo, divisoes) = Escala(_pontos.Max(p => p.Valor));

        // margem esquerda conforme o maior rótulo do eixo Y
        int largY = 0;
        for (int i = 0; i <= divisoes; i++)
            largY = Math.Max(largY, TextRenderer.MeasureText(g, Abreviar(passo * i), Font).Width);
        var area = new Rectangle(largY + 10, 10, Width - largY - 10 - 8, Height - 10 - 26);
        if (area.Width < 20 || area.Height < 20) return;

        // grade e eixo Y
        using (var pGrade = new Pen(Tema.Borda) { DashStyle = DashStyle.Dash, DashPattern = new[] { 3f, 3f } })
        using (var pBase = new Pen(ColorTranslator.FromHtml("#D1D5DB")))
        {
            for (int i = 0; i <= divisoes; i++)
            {
                float y = area.Bottom - area.Height * i / (float)divisoes;
                g.DrawLine(i == 0 ? pBase : pGrade, area.Left, y, area.Right, y);
                var rTxt = new Rectangle(0, (int)y - 9, largY + 4, 18);
                TextRenderer.DrawText(g, Abreviar(passo * i), Font, rTxt, Tema.TextoSuave,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        // barras
        int n = _pontos.Count;
        float slot = area.Width / (float)n;
        float largBarra = Math.Max(2f, Math.Min(slot * 0.62f, 46f));
        for (int i = 0; i < n; i++)
        {
            var p = _pontos[i];
            float x = area.Left + slot * i;
            _colunas.Add(new RectangleF(x, area.Top, slot, area.Height));
            float h = teto > 0 ? (float)(p.Valor / teto) * area.Height : 0;
            var r = new RectangleF(x + (slot - largBarra) / 2, area.Bottom - h, largBarra, h);
            _barras.Add(r);
            if (i == _hover)
            {
                using var bFundo = new SolidBrush(Color.FromArgb(18, Tema.Primaria));
                g.FillRectangle(bFundo, x, area.Top, slot, area.Height);
            }
            if (h < 0.5f) continue;
            using var path = TopoArredondado(r, Math.Min(4f, largBarra / 3));
            using var b = new SolidBrush(i == _hover ? Tema.PrimariaHover : Tema.Primaria);
            g.FillPath(b, path);
        }

        // rótulos do eixo X: pula rótulos para não sobrepor, garantindo sempre o último
        int largRot = 0;
        foreach (var p in _pontos) largRot = Math.Max(largRot, TextRenderer.MeasureText(g, RotuloX(p.Data), Font).Width);
        int pulo = Math.Max(1, (int)Math.Ceiling((largRot + 8) / slot));
        for (int i = n - 1; i >= 0; i -= pulo)
        {
            float cx = area.Left + slot * i + slot / 2;
            var rTxt = new Rectangle((int)(cx - largRot / 2f - 4), area.Bottom + 5, largRot + 8, 18);
            if (rTxt.Left < area.Left - largY) continue;
            TextRenderer.DrawText(g, RotuloX(_pontos[i].Data), Font, rTxt, i == _hover ? Tema.Texto : Tema.TextoSuave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }

        if (_hover >= 0 && _hover < n) DesenharDica(g, area, _hover);
    }

    private void DesenharDica(Graphics g, Rectangle area, int i)
    {
        var p = _pontos[i];
        string l1 = RotuloDica(p);
        string l2 = Formato.Moeda(p.Valor);
        string l3 = p.Quantidade == 1 ? "1 venda" : $"{p.Quantidade} vendas";
        var s1 = TextRenderer.MeasureText(g, l1, Tema.FontePequena);
        var s2 = TextRenderer.MeasureText(g, l2, Tema.FonteNegrito);
        var s3 = TextRenderer.MeasureText(g, l3, Tema.FontePequena);
        int w = Math.Max(s1.Width, Math.Max(s2.Width, s3.Width)) + 20;
        int h = s1.Height + s2.Height + s3.Height + 14;

        var barra = _barras[i];
        float cx = barra.Left + barra.Width / 2;
        int x = (int)(cx - w / 2f);
        x = Math.Max(area.Left, Math.Min(x, Width - w - 4));
        int y = (int)Math.Max(area.Top, barra.Top - h - 8);
        if (y + h > area.Bottom) y = area.Bottom - h;
        var r = new Rectangle(x, y, w, h);

        using (var sombra = Tema.Arredondado(new Rectangle(r.X + 1, r.Y + 2, r.Width, r.Height), 6))
        using (var bs = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
            g.FillPath(bs, sombra);
        using (var path = Tema.Arredondado(r, 6))
        using (var b = new SolidBrush(Tema.Sidebar))
            g.FillPath(b, path);

        int ty = r.Y + 7;
        TextRenderer.DrawText(g, l1, Tema.FontePequena, new Point(r.X + 10, ty), Tema.SidebarTexto);
        ty += s1.Height;
        TextRenderer.DrawText(g, l2, Tema.FonteNegrito, new Point(r.X + 10, ty), Color.White);
        ty += s2.Height;
        TextRenderer.DrawText(g, l3, Tema.FontePequena, new Point(r.X + 10, ty), Tema.SidebarTexto);
    }

    private static GraphicsPath TopoArredondado(RectangleF r, float raio)
    {
        var path = new GraphicsPath();
        float d = raio * 2;
        if (raio <= 0.5f || r.Height < d || r.Width < d) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddLine(r.Right, r.Y + raio, r.Right, r.Bottom);
        path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int novo = -1;
        for (int i = 0; i < _colunas.Count; i++)
            if (_colunas[i].Contains(e.Location)) { novo = i; break; }
        if (novo != _hover) { _hover = novo; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1) { _hover = -1; Invalidate(); }
    }
}

/// <summary>Seletor de opções lado a lado (ex.: "30 dias" | "12 meses").</summary>
public class SeletorSegmentado : Control
{
    private readonly string[] _opcoes;
    private int _selecionado, _hover = -1;

    public event EventHandler? SelecaoMudou;

    public int Selecionado
    {
        get => _selecionado;
        set { if (value != _selecionado) { _selecionado = value; Invalidate(); SelecaoMudou?.Invoke(this, EventArgs.Empty); } }
    }

    public SeletorSegmentado(params string[] opcoes)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _opcoes = opcoes;
        Font = Tema.FontePequena;
        Cursor = Cursors.Hand;
        int w = 8;
        foreach (string o in opcoes) w += TextRenderer.MeasureText(o, Tema.FonteNegrito).Width + 22;
        Size = new Size(w, 28);
    }

    private Rectangle Segmento(int i)
    {
        int w = (Width - 6) / _opcoes.Length;
        return new Rectangle(3 + w * i, 3, w, Height - 7);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Tema.Cartao);
        using (var path = Tema.Arredondado(new Rectangle(0, 0, Width - 1, Height - 1), 7))
        using (var b = new SolidBrush(Tema.Fundo))
            g.FillPath(b, path);
        for (int i = 0; i < _opcoes.Length; i++)
        {
            var r = Segmento(i);
            bool sel = i == _selecionado;
            if (sel)
            {
                using var path = Tema.Arredondado(r, 5);
                using var b = new SolidBrush(Color.White);
                using var p = new Pen(Tema.Borda);
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, _opcoes[i], sel ? Tema.FonteNegrito : Tema.Fonte, r,
                sel ? Tema.Texto : (i == _hover ? Tema.Texto : Tema.TextoSuave),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = -1;
        for (int i = 0; i < _opcoes.Length; i++) if (Segmento(i).Contains(e.Location)) h = i;
        if (h != _hover) { _hover = h; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        for (int i = 0; i < _opcoes.Length; i++) if (Segmento(i).Contains(e.Location)) Selecionado = i;
    }
}
