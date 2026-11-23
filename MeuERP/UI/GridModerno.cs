using System.Collections;
using System.Text;
using MeuERP.Nucleo;

namespace MeuERP.UI;

/// <summary>
/// DataGridView somente leitura já estilizado. Defina as colunas com <see cref="Coluna"/> e variantes
/// (o primeiro parâmetro é o nome da propriedade do objeto) e carregue os dados com <see cref="Carregar{T}"/>.
/// Clique no cabeçalho ordena. Valores bool aparecem como Sim/Não.
/// </summary>
public class GridModerno : DataGridView
{
    private IList? _dados;
    private Type? _tipo;
    private string? _colunaOrdenada;
    private bool _crescente = true;

    public GridModerno()
    {
        DoubleBuffered = true;
        AutoGenerateColumns = false;
        ReadOnly = true;
        AllowUserToAddRows = false;
        AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false;
        MultiSelect = false;
        SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        RowHeadersVisible = false;
        BorderStyle = BorderStyle.None;
        BackgroundColor = Color.White;
        CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        GridColor = Tema.Borda;
        EnableHeadersVisualStyles = false;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 36;
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Tema.Cabecalho, ForeColor = Tema.TextoSuave, Font = Tema.FonteNegrito,
            SelectionBackColor = Tema.Cabecalho, SelectionForeColor = Tema.TextoSuave,
            Padding = new Padding(6, 0, 6, 0), Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White, ForeColor = Tema.Texto, Font = Tema.Fonte,
            SelectionBackColor = Tema.Selecao, SelectionForeColor = Tema.Texto, Padding = new Padding(6, 0, 6, 0)
        };
        RowTemplate.Height = 32;
        Dock = DockStyle.Fill;
        StandardTab = true;
    }

    /// <summary>Adiciona uma coluna de texto. <paramref name="largura"/> = 0 faz a coluna preencher o espaço restante.</summary>
    public DataGridViewTextBoxColumn Coluna(string propriedade, string titulo, int largura = 0, string? formato = null,
        DataGridViewContentAlignment alinhamento = DataGridViewContentAlignment.MiddleLeft)
    {
        var col = new DataGridViewTextBoxColumn
        {
            Name = propriedade, DataPropertyName = propriedade, HeaderText = titulo,
            SortMode = DataGridViewColumnSortMode.Programmatic
        };
        if (largura > 0) col.Width = largura;
        else { col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; col.MinimumWidth = 120; }
        col.DefaultCellStyle.Alignment = alinhamento;
        col.HeaderCell.Style.Alignment = alinhamento;
        if (formato != null)
        {
            col.DefaultCellStyle.Format = formato;
            col.DefaultCellStyle.FormatProvider = Formato.PtBr;
        }
        Columns.Add(col);
        return col;
    }

    public DataGridViewTextBoxColumn ColunaMoeda(string propriedade, string titulo, int largura = 110) =>
        Coluna(propriedade, titulo, largura, "C2", DataGridViewContentAlignment.MiddleRight);

    public DataGridViewTextBoxColumn ColunaNumero(string propriedade, string titulo, int largura = 90, string formato = "#,##0.###") =>
        Coluna(propriedade, titulo, largura, formato, DataGridViewContentAlignment.MiddleRight);

    public DataGridViewTextBoxColumn ColunaData(string propriedade, string titulo, bool comHora = false) =>
        Coluna(propriedade, titulo, comHora ? 130 : 96, comHora ? "dd/MM/yyyy HH:mm" : "dd/MM/yyyy");

    /// <summary>Coluna de status: o código do banco é traduzido (Tema.TextoStatus) e colorido (Tema.CorStatus).</summary>
    public DataGridViewTextBoxColumn ColunaStatus(string propriedade, string titulo, int largura = 100)
    {
        var col = Coluna(propriedade, titulo, largura);
        col.Tag = "status";
        col.DefaultCellStyle.Font = Tema.FonteNegrito;
        return col;
    }

    public void Carregar<T>(IEnumerable<T> itens)
    {
        _tipo = typeof(T);
        var lista = itens.ToList();
        _dados = lista;
        _colunaOrdenada = null;
        foreach (DataGridViewColumn c in Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
        DataSource = lista;
    }

    /// <summary>Objeto da linha atual (ou null se não houver seleção).</summary>
    public T? Selecionado<T>() where T : class => CurrentRow?.DataBoundItem as T;

    /// <summary>Todos os objetos carregados, na ordem exibida.</summary>
    public IEnumerable<T> Itens<T>() => _dados?.Cast<T>() ?? Enumerable.Empty<T>();

    protected override void OnCellFormatting(DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
        {
            if (e.Value is bool b) { e.Value = b ? "Sim" : "Não"; e.FormattingApplied = true; }
            else if (Columns[e.ColumnIndex].Tag as string == "status" && e.Value is string s)
            {
                e.CellStyle!.ForeColor = Tema.CorStatus(s);
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
                e.Value = Tema.TextoStatus(s);
                e.FormattingApplied = true;
            }
        }
        base.OnCellFormatting(e);
    }

    protected override void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnColumnHeaderMouseClick(e);
        if (_dados == null || _tipo == null || e.Button != MouseButtons.Left) return;
        var col = Columns[e.ColumnIndex];
        if (col.SortMode == DataGridViewColumnSortMode.NotSortable) return;
        var prop = _tipo.GetProperty(col.DataPropertyName);
        if (prop == null) return;

        _crescente = _colunaOrdenada != col.Name || !_crescente;
        _colunaOrdenada = col.Name;
        var itens = _dados.Cast<object>();
        var ordenado = (_crescente
            ? itens.OrderBy(o => prop.GetValue(o), Comparador.Instancia)
            : itens.OrderByDescending(o => prop.GetValue(o), Comparador.Instancia)).ToList();

        var nova = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(_tipo))!;
        foreach (object o in ordenado) nova.Add(o);
        _dados = nova;
        DataSource = nova;
        foreach (DataGridViewColumn c in Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
        col.HeaderCell.SortGlyphDirection = _crescente ? SortOrder.Ascending : SortOrder.Descending;
    }

    private class Comparador : IComparer<object?>
    {
        public static readonly Comparador Instancia = new();
        public int Compare(object? x, object? y)
        {
            if (x == null) return y == null ? 0 : -1;
            if (y == null) return 1;
            if (x is string sx && y is string sy) return string.Compare(sx, sy, true, Formato.PtBr);
            return x is IComparable c ? c.CompareTo(y) : 0;
        }
    }

    /// <summary>Exporta o conteúdo visível para CSV (separador ';', abre direto no Excel).</summary>
    public void ExportarCsv(string nomeSugerido)
    {
        if (Rows.Count == 0) { Mensagem.Info("Não há dados para exportar."); return; }
        using var dlg = new SaveFileDialog
        {
            Filter = "Planilha CSV (*.csv)|*.csv", FileName = $"{nomeSugerido}-{DateTime.Now:yyyyMMdd-HHmm}.csv"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var cols = Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", cols.Select(c => Csv(c.HeaderText))));
        foreach (DataGridViewRow row in Rows)
            sb.AppendLine(string.Join(";", cols.Select(c => Csv(row.Cells[c.Index].FormattedValue?.ToString() ?? ""))));

        if (Mensagem.Tentar(() => File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true))))
            Mensagem.Info("Arquivo exportado com sucesso.");
    }

    private static string Csv(string s) =>
        s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
