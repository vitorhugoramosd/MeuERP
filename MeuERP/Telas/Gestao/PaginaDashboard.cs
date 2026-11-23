using System.Drawing.Drawing2D;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Gestao;

/// <summary>Primeira tela do sistema: indicadores, gráfico de vendas e listas rápidas.</summary>
public class PaginaDashboard : PaginaBase
{
    private const int AlturaKpi = 116;           // 104 do cartão + 12 de espaçamento
    private const int LarguraLayoutLargo = 1180; // a partir daqui as listas ficam em 3 colunas

    private readonly bool _gerente = Sessao.PodeGerenciar;
    private readonly Panel _rolagem;
    private readonly TableLayoutPanel _raiz;
    private readonly TableLayoutPanel _gradeKpis;
    private readonly List<TableLayoutPanel> _linhas = new();
    private readonly List<int> _alturasMinimas = new();
    private bool? _largo;

    private readonly KpiCartao _kVendasHoje, _kVendasMes, _kTicket, _kLucro, _kReceber, _kPagar, _kCaixa, _kEstoque;
    private readonly CartaoGrafico _cGrafico;
    private readonly GraficoBarras _grafico;
    private readonly SeletorSegmentado _seletorPeriodo;
    private readonly CartaoLista _cTop, _cUltimas, _cContas, _cEstoque;

    public PaginaDashboard()
    {
        Titulo = "Dashboard";
        string nome = (Sessao.Usuario?.Nome ?? "").Trim();
        string primeiro = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        string hoje = DateTime.Today.ToString("dddd, d 'de' MMMM 'de' yyyy", Formato.PtBr);
        Subtitulo = (primeiro.Length > 0 ? $"Olá, {primeiro}!  " : "") + "Hoje é " + hoje + ".";
        AdicionarAcao("Atualizar", EstiloBotao.Secundario, () => Carregar());

        // ---- indicadores ----
        _kVendasHoje = new KpiCartao("Vendas hoje", Tema.Primaria, "Vendas");
        _kVendasMes = new KpiCartao("Vendas no mês", Tema.Primaria, "Vendas");
        _kTicket = new KpiCartao("Ticket médio no mês", Tema.Roxo, "Vendas");
        _kLucro = new KpiCartao("Lucro bruto no mês", Tema.Sucesso, "Relatórios");
        _kReceber = new KpiCartao("A receber", ColorTranslator.FromHtml("#0891B2"), "Contas a Receber");
        _kPagar = new KpiCartao("A pagar", Tema.Aviso, "Contas a Pagar");
        _kCaixa = new KpiCartao("Saldo em caixa", ColorTranslator.FromHtml("#0D9488"), "Caixa");
        _kEstoque = new KpiCartao("Estoque baixo", Tema.Perigo, "Produtos");

        var kpis = _gerente
            ? new[] { _kVendasHoje, _kVendasMes, _kTicket, _kLucro, _kReceber, _kPagar, _kCaixa, _kEstoque }
            : new[] { _kVendasHoje, _kVendasMes, _kTicket, _kEstoque };
        int linhasKpi = (kpis.Length + 3) / 4;
        _gradeKpis = NovaTabela(4, linhasKpi);
        for (int i = 0; i < 4; i++) _gradeKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        for (int i = 0; i < linhasKpi; i++) _gradeKpis.RowStyles.Add(new RowStyle(SizeType.Absolute, AlturaKpi));
        for (int i = 0; i < kpis.Length; i++)
        {
            var k = kpis[i];
            k.Dock = DockStyle.Fill;
            k.Margin = new Padding(0, 0, i % 4 == 3 ? 0 : 12, 12);
            _gradeKpis.Controls.Add(k, i % 4, i / 4);
        }

        // ---- gráfico ----
        _grafico = new GraficoBarras { Dock = DockStyle.Fill };
        _cGrafico = new CartaoGrafico("Vendas — últimos 30 dias");
        _cGrafico.Size = new Size(600, 300);
        _cGrafico.Controls.Add(_grafico);
        _seletorPeriodo = new SeletorSegmentado("30 dias", "12 meses");
        _seletorPeriodo.Location = new Point(_cGrafico.Width - _seletorPeriodo.Width - 12, 9);
        _seletorPeriodo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _cGrafico.Controls.Add(_seletorPeriodo);
        _seletorPeriodo.BringToFront();
        _seletorPeriodo.SelecaoMudou += (_, _) => Mensagem.Tentar(CarregarGrafico);

        // ---- listas ----
        _cTop = new CartaoLista("Produtos mais vendidos no mês", "Nenhuma venda neste mês.");
        _cTop.Grid.Coluna("Descricao", "Produto");
        _cTop.Grid.ColunaNumero("Quantidade", "Qtd", 60);
        _cTop.Grid.ColunaMoeda("Valor", "Valor", 104);

        _cUltimas = new CartaoLista("Últimas vendas", "Nenhuma venda registrada.");
        _cUltimas.Grid.Coluna("Id", "Nº", 54, null, DataGridViewContentAlignment.MiddleRight);
        _cUltimas.Grid.Coluna("DataVenda", "Data/hora", 98, "dd/MM HH:mm");
        _cUltimas.Grid.Coluna("ClienteNome", "Cliente");
        _cUltimas.Grid.ColunaMoeda("Total", "Total", 100);
        _cUltimas.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Ir("Vendas"); };

        _cContas = new CartaoLista("Contas vencendo", "Nada vencido nem vencendo nos próximos 7 dias.");
        _cContas.Grid.Coluna("Tipo", "Tipo", 70).DefaultCellStyle.Font = Tema.FonteNegrito;
        _cContas.Grid.Coluna("Descricao", "Descrição");
        _cContas.Grid.ColunaData("DataVencimento", "Vencimento");
        _cContas.Grid.ColunaMoeda("Saldo", "Saldo", 100);
        _cContas.Grid.CellFormatting += FormatarConta;
        _cContas.Grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            var v = _cContas.Grid.Selecionado<VencimentoProximo>();
            if (v != null) Ir(v.Tipo == "Receber" ? "Contas a Receber" : "Contas a Pagar");
        };

        _cEstoque = new CartaoLista("Estoque baixo", "Nenhum produto abaixo do estoque mínimo.");
        _cEstoque.Grid.Coluna("Descricao", "Produto");
        _cEstoque.Grid.ColunaNumero("Estoque", "Saldo", 66);
        _cEstoque.Grid.ColunaNumero("EstoqueMinimo", "Mínimo", 70);
        _cEstoque.Grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && _cEstoque.Grid.Columns[e.ColumnIndex].Name == "Estoque")
            {
                e.CellStyle!.ForeColor = Tema.Perigo;
                e.CellStyle.Font = Tema.FonteNegrito;
            }
        };

        // ---- estrutura rolável ----
        _raiz = NovaTabela(1, 0);
        _raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _raiz.Dock = DockStyle.Top;

        _rolagem = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Fundo };
        _rolagem.Controls.Add(_raiz);
        Conteudo.Controls.Add(_rolagem);
        _rolagem.BringToFront();
        _rolagem.Resize += (_, _) => Reorganizar();
    }

    private static TableLayoutPanel NovaTabela(int colunas, int linhas) => new()
    {
        ColumnCount = colunas, RowCount = linhas, Margin = Padding.Empty, Padding = Padding.Empty,
        BackColor = Tema.Fundo, Dock = DockStyle.Fill
    };

    // ---------- layout responsivo ----------

    /// <summary>Monta uma linha com cartões lado a lado (larguras em %).</summary>
    private TableLayoutPanel Linha(params (Control controle, float pct)[] itens)
    {
        var t = NovaTabela(itens.Length, 1);
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        for (int i = 0; i < itens.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, itens[i].pct));
            var c = itens[i].controle;
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(0, 0, i == itens.Length - 1 ? 0 : 12, 12);
            t.Controls.Add(c, i, 0);
        }
        return t;
    }

    private void Reorganizar()
    {
        if (_rolagem.Width <= 0) return;
        bool largo = _rolagem.Width >= LarguraLayoutLargo;
        if (_largo != largo)
        {
            _largo = largo;
            _raiz.SuspendLayout();
            _raiz.Controls.Clear();
            _raiz.RowStyles.Clear();
            foreach (var l in _linhas) { l.Controls.Clear(); l.Dispose(); }
            _linhas.Clear();
            _alturasMinimas.Clear();

            if (largo)
            {
                AdicionarLinha(Linha((_cGrafico, 64), (_cTop, 36)), 290);
                AdicionarLinha(_gerente
                    ? Linha((_cUltimas, 34), (_cContas, 38), (_cEstoque, 28))
                    : Linha((_cUltimas, 55), (_cEstoque, 45)), 260);
            }
            else
            {
                AdicionarLinha(Linha((_cGrafico, 100)), 290);
                AdicionarLinha(Linha((_cTop, 55), (_cEstoque, 45)), 250);
                AdicionarLinha(_gerente ? Linha((_cUltimas, 50), (_cContas, 50)) : Linha((_cUltimas, 100)), 260);
            }

            _raiz.RowCount = _linhas.Count + 1;
            _raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, _gradeKpis.RowCount * AlturaKpi));
            _raiz.Controls.Add(_gradeKpis, 0, 0);
            for (int i = 0; i < _linhas.Count; i++)
            {
                _raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, _alturasMinimas[i]));
                _raiz.Controls.Add(_linhas[i], 0, i + 1);
            }
            _raiz.ResumeLayout(false);
        }
        AjustarAlturas();
    }

    private void AdicionarLinha(TableLayoutPanel linha, int alturaMinima)
    {
        _linhas.Add(linha);
        _alturasMinimas.Add(alturaMinima);
    }

    /// <summary>Distribui a altura disponível entre as linhas (com mínimos); se não couber, a página rola.</summary>
    private void AjustarAlturas()
    {
        int alturaKpis = _gradeKpis.RowCount * AlturaKpi;
        int disponivel = _rolagem.Height - alturaKpis - 4;
        int somaMin = _alturasMinimas.Sum();
        int total = alturaKpis;
        for (int i = 0; i < _linhas.Count; i++)
        {
            int h = disponivel > somaMin ? (int)(disponivel * (_alturasMinimas[i] / (double)somaMin)) : _alturasMinimas[i];
            _raiz.RowStyles[i + 1].Height = h;
            total += h;
        }
        _raiz.Height = total;
        _rolagem.AutoScrollMinSize = new Size(0, total);
    }

    // ---------- dados ----------

    public override void AoExibir()
    {
        Reorganizar();
        Carregar();
    }

    private void Carregar()
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            var r = RelatorioService.Resumo();
            _kVendasHoje.Definir(Formato.Moeda(r.VendasHoje), r.QtdVendasHoje == 1 ? "1 venda" : $"{r.QtdVendasHoje} vendas", Tema.TextoSuave);

            var variacao = r.VariacaoMes;
            if (variacao is decimal v)
                _kVendasMes.Definir(Formato.Moeda(r.VendasMes),
                    $"{(v >= 0 ? "▲" : "▼")} {Formato.Percentual(Math.Abs(v))} vs. mês anterior",
                    v >= 0 ? Tema.Sucesso : Tema.Perigo);
            else
                _kVendasMes.Definir(Formato.Moeda(r.VendasMes), r.QtdVendasMes == 1 ? "1 venda" : $"{r.QtdVendasMes} vendas", Tema.TextoSuave);

            _kTicket.Definir(Formato.Moeda(r.TicketMedioMes), r.QtdVendasMes == 1 ? "em 1 venda" : $"em {r.QtdVendasMes} vendas", Tema.TextoSuave);

            if (_gerente)
            {
                decimal margem = r.VendasMes > 0 ? Math.Round(r.LucroMes / r.VendasMes * 100, 1) : 0;
                _kLucro.Definir(Formato.Moeda(r.LucroMes), r.VendasMes > 0 ? $"margem de {Formato.Percentual(margem)}" : "sem vendas no mês",
                    r.LucroMes < 0 ? Tema.Perigo : Tema.TextoSuave);
                _kReceber.Definir(Formato.Moeda(r.ReceberAberto),
                    r.ReceberVencido > 0 ? $"{Formato.Moeda(r.ReceberVencido)} vencido" : "nada vencido",
                    r.ReceberVencido > 0 ? Tema.Perigo : Tema.TextoSuave);
                _kPagar.Definir(Formato.Moeda(r.PagarAberto),
                    r.PagarVencido > 0 ? $"{Formato.Moeda(r.PagarVencido)} vencido" : "nada vencido",
                    r.PagarVencido > 0 ? Tema.Perigo : Tema.TextoSuave);
                _kCaixa.Definir(Formato.Moeda(r.SaldoCaixa), "saldo acumulado", r.SaldoCaixa < 0 ? Tema.Perigo : Tema.TextoSuave);
            }

            _kEstoque.Cor = r.ProdutosEstoqueBaixo > 0 ? Tema.Perigo : Tema.Sucesso;
            _kEstoque.Definir(r.ProdutosEstoqueBaixo == 1 ? "1 produto" : $"{r.ProdutosEstoqueBaixo} produtos",
                _gerente ? $"estoque vale {Formato.Moeda(r.ValorEstoque)}" : (r.ProdutosEstoqueBaixo > 0 ? "abaixo do mínimo" : "tudo em ordem"),
                Tema.TextoSuave);

            CarregarGrafico();

            var inicioMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _cTop.Carregar(RelatorioService.TopProdutos(inicioMes, DateTime.Today, 5));
            _cUltimas.Carregar(RelatorioService.UltimasVendas(10));
            if (_gerente) _cContas.Carregar(RelatorioService.ProximosVencimentos(7));
            _cEstoque.Carregar(RelatorioService.EstoqueBaixo());
        }
        catch (Exception ex)
        {
            Mensagem.Erro("Não foi possível carregar o dashboard:\n\n" + ex.Message);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void CarregarGrafico()
    {
        bool mensal = _seletorPeriodo.Selecionado == 1;
        var pontos = mensal ? RelatorioService.VendasPorMes(12) : RelatorioService.VendasPorDia(30);
        _cGrafico.Titulo = mensal ? "Vendas — últimos 12 meses" : "Vendas — últimos 30 dias";
        decimal total = pontos.Sum(p => p.Valor);
        _cGrafico.Complemento = total > 0 ? $"{Formato.Moeda(total)} no período" : "";
        _cGrafico.Invalidate();
        _grafico.Definir(pontos, mensal);
    }

    private void FormatarConta(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        var grid = _cContas.Grid;
        if (grid.Rows[e.RowIndex].DataBoundItem is not VencimentoProximo v) return;
        string col = grid.Columns[e.ColumnIndex].Name;
        if (col == "Tipo")
        {
            e.CellStyle.ForeColor = v.Tipo == "Receber" ? Tema.Sucesso : Tema.Aviso;
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
        }
        else if (col == "DataVencimento" && v.Vencido)
        {
            e.CellStyle.ForeColor = Tema.Perigo;
            e.CellStyle.SelectionForeColor = Tema.Perigo;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // cartões que este perfil não exibe (nunca entraram na árvore de controles)
            foreach (Control c in new Control[] { _kLucro, _kReceber, _kPagar, _kCaixa, _cContas })
                if (c.Parent == null) c.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Ir(string menu)
    {
        if (FindForm() is TelaPrincipal tela) tela.BeginInvoke(() => tela.IrPara(menu));
    }

    // ---------- componentes internos ----------

    /// <summary>KPI com cor própria para o detalhe, realce ao passar o mouse e atalho para a tela relacionada.</summary>
    private class KpiCartao : CartaoKpi
    {
        private Color _corDetalhe = Tema.TextoSuave;
        private bool _hover;

        public KpiCartao(string rotulo, Color cor, string destino) : base(rotulo, cor)
        {
            Cursor = Cursors.Hand;
            Click += (_, _) =>
            {
                if (FindForm() is TelaPrincipal tela) tela.BeginInvoke(() => tela.IrPara(destino));
            };
        }

        public void Definir(string valor, string detalhe, Color corDetalhe)
        {
            _corDetalhe = corDetalhe;
            Valor = valor;
            Detalhe = detalhe;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            var g = e.Graphics;
            // redesenha o detalhe com a cor própria (a base usa a cor da faixa)
            var rDet = new Rectangle(16, 76, Width - 28, 18);
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, rDet);
            TextRenderer.DrawText(g, Detalhe, Tema.FontePequena, rDet, _corDetalhe,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (_hover)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = Tema.Arredondado(new Rectangle(0, 0, Width - 1, Height - 1), 8);
                using var p = new Pen(Color.FromArgb(140, Cor));
                g.DrawPath(p, path);
            }
        }
    }

    /// <summary>Cartão com título e um complemento discreto ao lado (ex.: total do período).</summary>
    private class CartaoGrafico : Cartao
    {
        public string Complemento { get; set; } = "";

        public CartaoGrafico(string titulo) : base(titulo) { Padding = new Padding(12, 48, 14, 10); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Complemento.Length == 0) return;
            int x = 14 + TextRenderer.MeasureText(e.Graphics, Titulo, Tema.FonteSubtitulo).Width + 6;
            TextRenderer.DrawText(e.Graphics, Complemento, Tema.Fonte, new Point(x, 15), Tema.TextoSuave);
        }
    }

    /// <summary>Cartão com título, grid e mensagem de lista vazia.</summary>
    private class CartaoLista : Cartao
    {
        private readonly Label _vazio;
        public GridModerno Grid { get; }

        public CartaoLista(string titulo, string textoVazio) : base(titulo)
        {
            Padding = new Padding(12, 44, 12, 10);
            Grid = new GridModerno { ColumnHeadersHeight = 32 };
            Grid.RowTemplate.Height = 30;
            Grid.DataBindingComplete += (_, _) => Grid.ClearSelection();
            _vazio = new Label
            {
                Dock = DockStyle.Fill, Text = textoVazio, TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Tema.TextoSuave, Font = Tema.Fonte, BackColor = Tema.Cartao, Visible = false
            };
            Controls.Add(Grid);
            Controls.Add(_vazio);
            _vazio.BringToFront();
        }

        public void Carregar<T>(List<T> itens)
        {
            // cartões estreitos: a coluna que preenche pode encolher mais que o padrão do grid
            foreach (DataGridViewColumn c in Grid.Columns)
                if (c.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) c.MinimumWidth = 70;
            Grid.Carregar(itens);
            bool vazio = itens.Count == 0;
            _vazio.Visible = vazio;
            Grid.Visible = !vazio;
        }
    }
}
