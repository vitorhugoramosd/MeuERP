using System.Drawing.Printing;
using MeuERP.Dados;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Gestao;

/// <summary>
/// Impressão genérica de um grid: título, empresa, período, colunas visíveis com larguras proporcionais,
/// quebra de página, numeração e linha de totais no final. Abre a visualização de impressão.
/// </summary>
public class ImpressaoGrid
{
    private readonly string _titulo, _subtitulo, _totais, _empresa;
    private readonly List<(string titulo, float peso, StringAlignment alinhamento)> _colunas = new();
    private readonly List<string[]> _linhas = new();
    private int _proximaLinha, _pagina;

    /// <param name="subtitulo">Ex.: "Período: 01/11/2026 a 30/11/2026".</param>
    /// <param name="totais">Texto impresso após a última linha (pode ser vazio).</param>
    public ImpressaoGrid(DataGridView grid, string titulo, string subtitulo, string totais)
    {
        _titulo = titulo;
        _subtitulo = subtitulo;
        _totais = totais;
        var emp = EmpresaRepository.Obter();
        _empresa = string.Join("  •  ", new[]
        {
            string.IsNullOrWhiteSpace(emp.NomeFantasia) ? emp.RazaoSocial : emp.NomeFantasia,
            Formato.SoDigitos(emp.Cnpj).Length == 14 ? "CNPJ " + Formato.Documento(emp.Cnpj) : ""
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // captura o conteúdo já formatado (como aparece na tela), na ordem exibida
        var cols = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
        foreach (var c in cols)
        {
            var al = c.DefaultCellStyle.Alignment switch
            {
                DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.TopRight or DataGridViewContentAlignment.BottomRight => StringAlignment.Far,
                DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.BottomCenter => StringAlignment.Center,
                _ => StringAlignment.Near
            };
            // colunas que preenchem o espaço contam como mais largas
            float peso = c.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill ? Math.Max(c.Width, 200) : c.Width;
            _colunas.Add((c.HeaderText, peso, al));
        }
        foreach (DataGridViewRow row in grid.Rows)
            _linhas.Add(cols.Select(c => row.Cells[c.Index].FormattedValue?.ToString() ?? "").ToArray());
    }

    /// <summary>Mostra a visualização de impressão (de onde o usuário imprime).</summary>
    public void Visualizar()
    {
        if (_colunas.Count == 0) { Mensagem.Info("Não há dados para imprimir."); return; }
        using var doc = new PrintDocument { DocumentName = _titulo };
        doc.DefaultPageSettings.Landscape = _colunas.Count > 6;
        doc.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);
        doc.BeginPrint += (_, _) => { _proximaLinha = 0; _pagina = 0; };
        doc.PrintPage += ImprimirPagina;
        using var dlg = new PrintPreviewDialog
        {
            Document = doc, Width = 1100, Height = 800, StartPosition = FormStartPosition.CenterParent,
            Text = "Visualizar impressão - " + _titulo, ShowIcon = false
        };
        dlg.PrintPreviewControl.Zoom = 1.0;
        dlg.ShowDialog(Form.ActiveForm);
    }

    private void ImprimirPagina(object? sender, PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        var m = e.MarginBounds;
        _pagina++;
        using var fTitulo = new Font("Segoe UI Semibold", 14f);
        using var fSub = new Font("Segoe UI", 9f);
        using var fCab = new Font("Segoe UI Semibold", 8.5f);
        using var fTexto = new Font("Segoe UI", 8.5f);
        using var fTotal = new Font("Segoe UI Semibold", 9f);
        using var pLinha = new Pen(Color.FromArgb(210, 210, 210), 0.5f);
        using var pForte = new Pen(Color.FromArgb(90, 90, 90), 1f);
        using var bTexto = new SolidBrush(Color.Black);
        using var bSuave = new SolidBrush(Color.FromArgb(100, 100, 100));
        using var bCab = new SolidBrush(Color.FromArgb(243, 244, 246));

        float y = m.Top;
        // cabeçalho
        g.DrawString(_titulo, fTitulo, bTexto, m.Left, y);
        var fmtDir = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString(_empresa, fSub, bSuave, new RectangleF(m.Left, y + 4, m.Width, 20), fmtDir);
        y += fTitulo.GetHeight(g) + 2;
        if (_subtitulo.Length > 0) { g.DrawString(_subtitulo, fSub, bSuave, m.Left, y); }
        g.DrawString("Emitido em " + Formato.DataHora(DateTime.Now), fSub, bSuave, new RectangleF(m.Left, y, m.Width, 20), fmtDir);
        y += fSub.GetHeight(g) + 8;
        g.DrawLine(pForte, m.Left, y, m.Right, y);
        y += 6;

        // larguras proporcionais
        float soma = _colunas.Sum(c => c.peso);
        var larguras = _colunas.Select(c => m.Width * c.peso / soma).ToArray();
        float alturaLinha = fTexto.GetHeight(g) + 6;

        void Celulas(string[] textos, Font f, Brush b, float yy)
        {
            float x = m.Left;
            for (int i = 0; i < _colunas.Count; i++)
            {
                var r = new RectangleF(x + 3, yy + 3, larguras[i] - 6, alturaLinha - 3);
                var fmt = new StringFormat(StringFormatFlags.NoWrap) { Alignment = _colunas[i].alinhamento, Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString(i < textos.Length ? textos[i] : "", f, b, r, fmt);
                x += larguras[i];
            }
        }

        // cabeçalho da tabela
        g.FillRectangle(bCab, m.Left, y, m.Width, alturaLinha + 2);
        Celulas(_colunas.Select(c => c.titulo).ToArray(), fCab, bTexto, y + 1);
        y += alturaLinha + 2;
        g.DrawLine(pForte, m.Left, y, m.Right, y);

        float rodape = m.Bottom - 22;
        float espacoTotais = _totais.Length > 0 ? fTotal.GetHeight(g) * 3 + 10 : 0;
        while (_proximaLinha < _linhas.Count)
        {
            if (y + alturaLinha > rodape) break;
            Celulas(_linhas[_proximaLinha], fTexto, bTexto, y);
            y += alturaLinha;
            g.DrawLine(pLinha, m.Left, y, m.Right, y);
            _proximaLinha++;
        }

        bool acabou = _proximaLinha >= _linhas.Count;
        if (acabou && _totais.Length > 0)
        {
            if (y + espacoTotais > rodape) { e.HasMorePages = true; Rodape(g, m, fSub, bSuave); return; }
            y += 6;
            g.DrawLine(pForte, m.Left, y, m.Right, y);
            g.DrawString(_totais, fTotal, bTexto, new RectangleF(m.Left, y + 4, m.Width, espacoTotais));
        }
        Rodape(g, m, fSub, bSuave);
        e.HasMorePages = !acabou;
    }

    private void Rodape(Graphics g, Rectangle m, Font f, Brush b)
    {
        var fmt = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString($"Página {_pagina}", f, b, new RectangleF(m.Left, m.Bottom - 16, m.Width, 18), fmt);
        g.DrawString("MeuERP", f, b, m.Left, m.Bottom - 16);
    }
}
