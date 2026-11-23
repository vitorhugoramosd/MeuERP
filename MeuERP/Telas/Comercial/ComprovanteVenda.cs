using System.Drawing.Printing;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Comercial;

/// <summary>
/// Comprovante de venda / orçamento impresso via PrintDocument (com visualização prévia).
/// Suporta várias páginas: cabeçalho em todas, dados da venda na primeira e fechamento (totais,
/// pagamento, vencimentos e observações) após o último item.
/// </summary>
public sealed class ComprovanteVenda : IDisposable
{
    private readonly Venda _venda;
    private readonly Empresa _empresa;
    private readonly List<Titulo> _titulos;
    private readonly PrintDocument _doc = new();

    private readonly Font _fEmpresa = new("Segoe UI Semibold", 13f);
    private readonly Font _fTitulo = new("Segoe UI Semibold", 13f);
    private readonly Font _fNormal = new("Segoe UI", 9f);
    private readonly Font _fNegrito = new("Segoe UI Semibold", 9f);
    private readonly Font _fPequena = new("Segoe UI", 7.5f);
    private readonly Font _fTotal = new("Segoe UI Semibold", 12f);

    private int _proximoItem, _pagina;

    private ComprovanteVenda(Venda venda)
    {
        _venda = venda;
        _empresa = EmpresaRepository.Obter();
        _titulos = venda.Status == StatusVenda.Orcamento
            ? new List<Titulo>()
            : FinanceiroService.Listar(TipoTitulo.Receber, new FiltroTitulos { PessoaId = venda.ClienteId })
                .Where(t => t.OrigemId == venda.Id).OrderBy(t => t.Parcela).ThenBy(t => t.Id).ToList();

        _doc.DocumentName = $"{TituloDocumento} nº {venda.Id}";
        _doc.BeginPrint += (_, _) => { _proximoItem = 0; _pagina = 0; };
        _doc.PrintPage += ImprimirPagina;
    }

    /// <summary>Carrega a venda e abre a visualização de impressão.</summary>
    public static void Visualizar(int vendaId, IWin32Window? dono = null)
    {
        var v = VendaService.Obter(vendaId) ?? throw new RegraException("Venda não encontrada.");
        using var comprovante = new ComprovanteVenda(v);
        comprovante.Mostrar(dono);
    }

    private void Mostrar(IWin32Window? dono)
    {
        try
        {
            using var dlg = new PrintPreviewDialog
            {
                Document = _doc, ClientSize = new Size(900, 660), StartPosition = FormStartPosition.CenterParent,
                UseAntiAlias = true, ShowIcon = false, Text = "Visualizar impressão - " + _doc.DocumentName
            };
            dlg.ShowDialog(dono ?? Form.ActiveForm);
        }
        catch (InvalidPrinterException)
        {
            throw new RegraException("Nenhuma impressora disponível. Instale uma impressora (ou o \"Microsoft Print to PDF\") para imprimir.");
        }
    }

    public void Dispose()
    {
        _doc.Dispose();
        _fEmpresa.Dispose(); _fTitulo.Dispose(); _fNormal.Dispose(); _fNegrito.Dispose(); _fPequena.Dispose(); _fTotal.Dispose();
    }

    private string TituloDocumento => _venda.Status switch
    {
        StatusVenda.Orcamento => "ORÇAMENTO",
        StatusVenda.Cancelada => "CANCELADA",
        _ => "COMPROVANTE DE VENDA"
    };

    // ---------- Desenho ----------

    private void ImprimirPagina(object? sender, PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        var m = e.MarginBounds;
        _pagina++;
        int inicioPagina = _proximoItem;
        float limite = m.Bottom - 24; // espaço reservado ao rodapé da página

        float y = Cabecalho(g, m);
        if (_pagina == 1) y = DadosVenda(g, m, y);

        var itens = _venda.Itens;
        if (_proximoItem < itens.Count)
        {
            y = CabecalhoTabela(g, m, y);
            float h = _fNormal.GetHeight(g) + 5;
            while (_proximoItem < itens.Count && y + h <= limite)
            {
                LinhaItem(g, m, y, itens[_proximoItem]);
                y += h;
                _proximoItem++;
            }
            if (_proximoItem < itens.Count)
            {
                RodapePagina(g, m, continua: true);
                e.HasMorePages = true;
                return;
            }
            using var pen = new Pen(Color.Black, 1);
            g.DrawLine(pen, m.Left, y + 2, m.Right, y + 2);
            y += 10;
        }

        // fechamento: se não couber nesta página (e ela já tem itens), vai para a próxima
        float necessario = AlturaFechamento(g, m);
        bool paginaSoDeFechamento = inicioPagina >= itens.Count;
        if (y + necessario > limite && !paginaSoDeFechamento)
        {
            RodapePagina(g, m, continua: true);
            e.HasMorePages = true;
            return;
        }
        Fechamento(g, m, y);
        RodapePagina(g, m, continua: false);
        e.HasMorePages = false;
    }

    private float Cabecalho(Graphics g, Rectangle m)
    {
        float y = m.Top;
        string nome = !string.IsNullOrWhiteSpace(_empresa.NomeFantasia) ? _empresa.NomeFantasia : _empresa.RazaoSocial;
        float larguraEsq = m.Width * 0.62f;
        var esq = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        g.DrawString(nome, _fEmpresa, Brushes.Black, new RectangleF(m.Left, y, larguraEsq, _fEmpresa.GetHeight(g) + 2), esq);
        float yEsq = y + _fEmpresa.GetHeight(g) + 2;

        var linhas = new List<string>();
        if (!string.IsNullOrWhiteSpace(_empresa.NomeFantasia) && !string.IsNullOrWhiteSpace(_empresa.RazaoSocial) &&
            _empresa.RazaoSocial != _empresa.NomeFantasia)
            linhas.Add(_empresa.RazaoSocial);
        var doc = new List<string>();
        if (Formato.SoDigitos(_empresa.Cnpj).Length > 0) doc.Add("CNPJ: " + Formato.Documento(_empresa.Cnpj));
        if (!string.IsNullOrWhiteSpace(_empresa.InscricaoEstadual)) doc.Add("IE: " + _empresa.InscricaoEstadual);
        if (!string.IsNullOrWhiteSpace(_empresa.Telefone)) doc.Add("Tel.: " + _empresa.Telefone);
        if (doc.Count > 0) linhas.Add(string.Join("   ", doc));
        var end = new List<string>();
        if (!string.IsNullOrWhiteSpace(_empresa.Endereco)) end.Add(_empresa.Endereco);
        string cidadeUf = string.Join("/", new[] { _empresa.Cidade, _empresa.Uf }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (cidadeUf.Length > 0) end.Add(cidadeUf);
        if (!string.IsNullOrWhiteSpace(_empresa.Cep)) end.Add("CEP " + _empresa.Cep);
        if (end.Count > 0) linhas.Add(string.Join(" - ", end));
        if (!string.IsNullOrWhiteSpace(_empresa.Email)) linhas.Add(_empresa.Email);

        float hP = _fNormal.GetHeight(g);
        foreach (string l in linhas)
        {
            g.DrawString(l, _fNormal, Brushes.Black, new RectangleF(m.Left, yEsq, larguraEsq, hP + 1), esq);
            yEsq += hP;
        }

        // à direita: tipo do documento, número e data
        var dir = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap };
        float xDir = m.Left + larguraEsq;
        float wDir = m.Width - larguraEsq;
        Brush corTitulo = _venda.Status == StatusVenda.Cancelada ? Brushes.DarkRed : Brushes.Black;
        float yDir = y;
        g.DrawString(TituloDocumento, _fTitulo, corTitulo, new RectangleF(xDir, yDir, wDir, _fTitulo.GetHeight(g) + 2), dir);
        yDir += _fTitulo.GetHeight(g) + 2;
        g.DrawString($"Nº {_venda.Id}", _fNegrito, Brushes.Black, new RectangleF(xDir, yDir, wDir, hP + 1), dir);
        yDir += hP;
        g.DrawString(Formato.DataHora(_venda.DataVenda), _fNormal, Brushes.Black, new RectangleF(xDir, yDir, wDir, hP + 1), dir);
        yDir += hP;
        if (_pagina > 1)
        {
            g.DrawString("(continuação)", _fPequena, Brushes.Gray, new RectangleF(xDir, yDir, wDir, hP), dir);
            yDir += hP;
        }

        y = Math.Max(yEsq, yDir) + 6;
        using var pen = new Pen(Color.Black, 1.5f);
        g.DrawLine(pen, m.Left, y, m.Right, y);
        return y + 8;
    }

    private float DadosVenda(Graphics g, Rectangle m, float y)
    {
        float h = _fNormal.GetHeight(g) + 2;
        string cliente = _venda.ClienteNome;
        if (_venda.ClienteId is int idCliente)
        {
            var c = ClienteRepository.Obter(idCliente);
            if (c != null)
            {
                cliente = c.Nome + (c.Documento.Length > 0 ? "   CPF/CNPJ: " + c.DocumentoFormatado : "");
                string contato = string.Join("   ", new[]
                {
                    string.Join(", ", new[] { c.Endereco, c.Numero, c.Bairro }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    c.CidadeUf, string.IsNullOrWhiteSpace(c.Telefone) ? "" : "Tel.: " + c.Telefone
                }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (contato.Length > 0) cliente += "\n" + contato;
            }
        }

        y = Campo(g, m, y, "Cliente:", cliente);
        y = Campo(g, m, y, "Vendedor:", string.IsNullOrWhiteSpace(_venda.UsuarioNome) ? "-" : _venda.UsuarioNome);
        y = Campo(g, m, y, "Situação:", Tema.TextoStatus(_venda.Status));
        return y + h / 2;
    }

    private float Campo(Graphics g, Rectangle m, float y, string rotulo, string valor)
    {
        const float larguraRotulo = 80;
        g.DrawString(rotulo, _fNegrito, Brushes.Black, m.Left, y);
        var area = new SizeF(m.Width - larguraRotulo, 1000);
        var tam = g.MeasureString(valor, _fNormal, area);
        g.DrawString(valor, _fNormal, Brushes.Black, new RectangleF(m.Left + larguraRotulo, y, area.Width, tam.Height + 1));
        return y + Math.Max(tam.Height, _fNormal.GetHeight(g)) + 2;
    }

    // colunas da tabela de itens: x relativo e largura
    private (float X, float W)[] Colunas(Rectangle m)
    {
        float wCod = 80, wQtd = 70, wUnit = 85, wDesc = 75, wTot = 90;
        float wDescricao = m.Width - wCod - wQtd - wUnit - wDesc - wTot;
        float x = m.Left;
        var cols = new (float, float)[6];
        float[] larguras = { wCod, wDescricao, wQtd, wUnit, wDesc, wTot };
        for (int i = 0; i < 6; i++) { cols[i] = (x, larguras[i]); x += larguras[i]; }
        return cols;
    }

    private float CabecalhoTabela(Graphics g, Rectangle m, float y)
    {
        var cols = Colunas(m);
        float h = _fNegrito.GetHeight(g) + 6;
        using (var fundo = new SolidBrush(Color.FromArgb(235, 235, 235)))
            g.FillRectangle(fundo, m.Left, y, m.Width, h);
        string[] titulos = { "Código", "Descrição", "Qtd", "Unitário", "Desconto", "Total" };
        for (int i = 0; i < titulos.Length; i++)
            Texto(g, titulos[i], _fNegrito, cols[i], y + 3, i >= 2);
        return y + h + 2;
    }

    private void LinhaItem(Graphics g, Rectangle m, float y, VendaItem i)
    {
        var cols = Colunas(m);
        string desc = i.ProdutoDescricao + (string.IsNullOrWhiteSpace(i.Unidade) ? "" : $" ({i.Unidade})");
        Texto(g, i.ProdutoCodigo, _fNormal, cols[0], y, false);
        Texto(g, desc, _fNormal, cols[1], y, false);
        Texto(g, Formato.Qtd(i.Quantidade), _fNormal, cols[2], y, true);
        Texto(g, Formato.Moeda(i.PrecoUnitario), _fNormal, cols[3], y, true);
        Texto(g, i.Desconto > 0 ? Formato.Moeda(i.Desconto) : "-", _fNormal, cols[4], y, true);
        Texto(g, Formato.Moeda(i.Total), _fNormal, cols[5], y, true);
    }

    private static void Texto(Graphics g, string s, Font f, (float X, float W) col, float y, bool direita)
    {
        using var sf = new StringFormat
        {
            Alignment = direita ? StringAlignment.Far : StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap
        };
        g.DrawString(s, f, Brushes.Black, new RectangleF(col.X + 2, y, col.W - 4, f.GetHeight(g) + 2), sf);
    }

    private List<string> LinhasPagamento()
    {
        var l = new List<string>();
        bool aVista = FormasPagamento.EhAVista(_venda.FormaPagamento);
        l.Add($"Forma de pagamento: {_venda.FormaPagamento}" +
              (aVista ? " (à vista)" : $" - {_venda.Parcelas} parcela{(_venda.Parcelas > 1 ? "s" : "")}"));
        foreach (var t in _titulos)
            l.Add($"   Parcela {t.ParcelaTexto}   Vencimento {Formato.Data(t.DataVencimento)}   {Formato.Moeda(t.Valor)}   {Tema.TextoStatus(t.Situacao)}");
        return l;
    }

    private float AlturaFechamento(Graphics g, Rectangle m)
    {
        float h = _fNormal.GetHeight(g) + 2;
        float total = 3 * h + _fTotal.GetHeight(g) + 12;          // subtotal, desconto, total
        total += LinhasPagamento().Count * h + 10;
        if (!string.IsNullOrWhiteSpace(_venda.Observacoes))
            total += h + g.MeasureString(_venda.Observacoes, _fNormal, m.Width).Height + 6;
        if (_venda.Status == StatusVenda.Orcamento) total += h * 2;
        return total;
    }

    private void Fechamento(Graphics g, Rectangle m, float y)
    {
        float h = _fNormal.GetHeight(g) + 2;
        float wRot = 140, wVal = 120;
        float xVal = m.Right - wVal, xRot = xVal - wRot;
        var dir = new StringFormat { Alignment = StringAlignment.Far };

        void Linha(string rot, string val, Font f)
        {
            float hl = f.GetHeight(g) + 2;
            g.DrawString(rot, f, Brushes.Black, new RectangleF(xRot, y, wRot, hl), dir);
            g.DrawString(val, f, Brushes.Black, new RectangleF(xVal, y, wVal, hl), dir);
            y += hl;
        }

        int qtdItens = _venda.Itens.Count;
        g.DrawString($"{qtdItens} {(qtdItens == 1 ? "item" : "itens")}  •  {Formato.Qtd(_venda.Itens.Sum(i => i.Quantidade))} unidades",
            _fNormal, Brushes.Black, m.Left, y);
        Linha("Subtotal:", Formato.Moeda(_venda.Subtotal), _fNormal);
        Linha("Desconto:", _venda.Desconto > 0 ? "- " + Formato.Moeda(_venda.Desconto) : Formato.Moeda(0), _fNormal);
        y += 4;
        Linha("TOTAL:", Formato.Moeda(_venda.Total), _fTotal);
        y += 10;

        foreach (string l in LinhasPagamento())
        {
            g.DrawString(l, l.StartsWith("   ") ? _fNormal : _fNegrito, Brushes.Black, m.Left, y);
            y += h;
        }
        y += 6;

        if (!string.IsNullOrWhiteSpace(_venda.Observacoes))
        {
            g.DrawString("Observações:", _fNegrito, Brushes.Black, m.Left, y);
            y += h;
            var tam = g.MeasureString(_venda.Observacoes, _fNormal, m.Width);
            g.DrawString(_venda.Observacoes, _fNormal, Brushes.Black, new RectangleF(m.Left, y, m.Width, tam.Height + 2));
            y += tam.Height + 6;
        }

        if (_venda.Status == StatusVenda.Orcamento)
        {
            y += h / 2;
            g.DrawString("Orçamento sujeito à disponibilidade de estoque. Preços podem ser alterados sem aviso prévio.",
                _fPequena, Brushes.Gray, m.Left, y);
        }
    }

    private void RodapePagina(Graphics g, Rectangle m, bool continua)
    {
        float y = m.Bottom - _fPequena.GetHeight(g);
        using var pen = new Pen(Color.Gray, 0.5f);
        g.DrawLine(pen, m.Left, y - 4, m.Right, y - 4);
        g.DrawString($"Impresso em {Formato.DataHora(DateTime.Now)}" + (continua ? "  •  continua na próxima página" : ""),
            _fPequena, Brushes.Gray, m.Left, y);
        var dir = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString($"Página {_pagina}", _fPequena, Brushes.Gray, new RectangleF(m.Left, y, m.Width, _fPequena.GetHeight(g) + 2), dir);
    }
}
