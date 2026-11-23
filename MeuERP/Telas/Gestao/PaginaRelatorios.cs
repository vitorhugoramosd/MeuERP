using System.Globalization;
using System.Text;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Gestao;

/// <summary>Relatórios gerenciais: lista à esquerda, filtros de período, grid, totais, exportação e impressão.</summary>
public class PaginaRelatorios : PaginaBase
{
    private readonly List<DefinicaoRelatorio> _relatorios;
    private readonly ListBox _lista = new();
    private readonly DateTimePicker _de, _ate;
    private readonly Control[] _controlesPeriodo;
    private readonly Label _lblSemPeriodo, _lblTitulo, _lblDescricao, _lblResumo;
    private readonly GridModerno _grid = new();
    private readonly BotaoModerno _btnExportar, _btnImprimir;
    private DefinicaoRelatorio? _atual;
    private string _resumo = "", _periodoGerado = "";
    private decimal _saldoAnteriorFluxo;
    private readonly bool _permitido = Sessao.PodeGerenciar;

    public PaginaRelatorios()
    {
        Titulo = "Relatórios";
        Subtitulo = "Análises de vendas, compras, estoque e financeiro";

        _btnImprimir = AdicionarAcao("Imprimir", EstiloBotao.Secundario, Imprimir);
        _btnExportar = AdicionarAcao("Exportar CSV", EstiloBotao.Secundario, Exportar);

        _relatorios = CriarRelatorios();

        if (!_permitido)
        {
            _btnImprimir.Visible = _btnExportar.Visible = false;
            Conteudo.Controls.Add(new Label
            {
                Dock = DockStyle.Fill, Text = "Os relatórios estão disponíveis apenas para gerentes e administradores.",
                TextAlign = ContentAlignment.MiddleCenter, ForeColor = Tema.TextoSuave, Font = Tema.FonteSubtitulo
            });
            _de = new DateTimePicker(); _ate = new DateTimePicker();
            _controlesPeriodo = Array.Empty<Control>();
            _lblSemPeriodo = _lblTitulo = _lblDescricao = _lblResumo = new Label();
            return;
        }

        // ---- lista de relatórios (esquerda) ----
        var cartaoLista = new Cartao("Escolha o relatório") { Dock = DockStyle.Left, Width = 262, Padding = new Padding(8, 44, 8, 8) };
        _lista.Dock = DockStyle.Fill;
        _lista.BorderStyle = BorderStyle.None;
        _lista.DrawMode = DrawMode.OwnerDrawFixed;
        _lista.ItemHeight = 46;
        _lista.IntegralHeight = false;
        _lista.BackColor = Tema.Cartao;
        _lista.Font = Tema.Fonte;
        foreach (var r in _relatorios) _lista.Items.Add(r);
        _lista.DrawItem += DesenharItem;
        _lista.SelectedIndexChanged += (_, _) => Selecionar(_lista.SelectedItem as DefinicaoRelatorio);
        cartaoLista.Controls.Add(_lista);

        // ---- área do relatório (direita) ----
        var direita = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo };
        _lblTitulo = new Label { Dock = DockStyle.Top, Height = 28, Font = Tema.FonteSubtitulo, ForeColor = Tema.Texto, TextAlign = ContentAlignment.BottomLeft };
        _lblDescricao = new Label { Dock = DockStyle.Top, Height = 22, Font = Tema.Fonte, ForeColor = Tema.TextoSuave, TextAlign = ContentAlignment.TopLeft };

        var filtros = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
            BackColor = Tema.Fundo, Padding = new Padding(0, 2, 0, 6)
        };
        var primeiroDia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var lblDe = Ui.Rotulo("De");
        lblDe.Margin = new Padding(0, 10, 4, 0);
        _de = Ui.Data(primeiroDia);
        _de.Margin = new Padding(0, 6, 6, 0);
        var lblAte = Ui.Rotulo("até");
        lblAte.Margin = new Padding(2, 10, 4, 0);
        _ate = Ui.Data(primeiroDia.AddMonths(1).AddDays(-1));
        _ate.Margin = new Padding(0, 6, 10, 0);
        var btnGerar = new BotaoModerno("Gerar", EstiloBotao.Primario, Gerar) { Margin = new Padding(0, 2, 14, 4) };
        BotaoModerno Atalho(string texto, Func<(DateTime, DateTime)> periodo) =>
            new(texto, EstiloBotao.Secundario, () => { var (d, a) = periodo(); _de.Value = d; _ate.Value = a; Gerar(); })
            {
                Height = 30, Margin = new Padding(0, 4, 6, 4)
            };
        DateTime hoje = DateTime.Today;
        var mesAtual = new DateTime(hoje.Year, hoje.Month, 1);
        var atalhos = new Control[]
        {
            Atalho("Hoje", () => (hoje, hoje)),
            Atalho("Este mês", () => (mesAtual, mesAtual.AddMonths(1).AddDays(-1))),
            Atalho("Mês passado", () => (mesAtual.AddMonths(-1), mesAtual.AddDays(-1))),
            Atalho("Este ano", () => (new DateTime(hoje.Year, 1, 1), new DateTime(hoje.Year, 12, 31))),
        };
        _lblSemPeriodo = new Label
        {
            AutoSize = true, Text = "Posição atual — este relatório não usa período.", ForeColor = Tema.TextoSuave,
            Font = Tema.Fonte, Margin = new Padding(0, 10, 14, 0), Visible = false
        };
        _controlesPeriodo = new Control[] { lblDe, _de, lblAte, _ate }.Concat(atalhos).ToArray();
        filtros.Controls.AddRange(new Control[] { lblDe, _de, lblAte, _ate, _lblSemPeriodo, btnGerar });
        filtros.Controls.AddRange(atalhos);

        var cartaoGrid = new Cartao { Dock = DockStyle.Fill, Padding = new Padding(6) };
        cartaoGrid.Controls.Add(_grid);
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.CellStyle != null && _atual != null)
                _atual.Formatar(_grid, e);
        };

        _lblResumo = new Label
        {
            Dock = DockStyle.Bottom, Height = 40, Font = Tema.FonteNegrito, ForeColor = Tema.Texto,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0), AutoEllipsis = true
        };

        direita.Controls.Add(cartaoGrid);
        direita.Controls.Add(_lblResumo);
        direita.Controls.Add(filtros);
        direita.Controls.Add(_lblDescricao);
        direita.Controls.Add(_lblTitulo);
        cartaoGrid.BringToFront();

        Conteudo.Controls.Add(direita);
        Conteudo.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 16, BackColor = Tema.Fundo });
        Conteudo.Controls.Add(cartaoLista);
        direita.BringToFront();

        _ate.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Gerar(); } };
        _de.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Gerar(); } };
    }

    public override void AoExibir()
    {
        if (!_permitido || _lista.Items.Count == 0) return;
        _lista.SelectedIndex = 0; // dispara Selecionar → Gerar
        _lista.Focus();
    }

    // ---------- interação ----------

    private void Selecionar(DefinicaoRelatorio? def)
    {
        if (def == null || def == _atual) return;
        _atual = def;
        _lblTitulo.Text = def.Nome;
        _lblDescricao.Text = def.Descricao;
        foreach (var c in _controlesPeriodo) c.Visible = def.UsaPeriodo;
        _lblSemPeriodo.Visible = !def.UsaPeriodo;

        _grid.DataSource = null;
        _grid.Columns.Clear();
        def.ConfigurarColunas(_grid);
        Gerar();
    }

    private void Gerar()
    {
        var def = _atual;
        if (def == null) return;
        if (def.UsaPeriodo && _de.Value.Date > _ate.Value.Date)
        {
            Mensagem.Aviso("A data inicial não pode ser maior que a data final.");
            return;
        }
        DateTime de = _de.Value.Date, ate = _ate.Value.Date;
        Cursor = Cursors.WaitCursor;
        try
        {
            bool ok = Mensagem.Tentar(() =>
            {
                _resumo = def.Gerar(_grid, de, ate);
                _periodoGerado = def.UsaPeriodo
                    ? $"Período: {Formato.Data(de)} a {Formato.Data(ate)}"
                    : $"Posição em {Formato.DataHora(DateTime.Now)}";
            });
            if (!ok) { _resumo = ""; _grid.DataSource = null; }
            _lblResumo.Text = _resumo;
            _grid.ClearSelection();
        }
        finally { Cursor = Cursors.Default; }
    }

    private void Exportar()
    {
        if (_atual == null) return;
        _grid.ExportarCsv(Slug(_atual.Nome));
    }

    private void Imprimir()
    {
        if (_atual == null) return;
        if (_grid.Rows.Count == 0) { Mensagem.Info("Não há dados para imprimir. Gere o relatório primeiro."); return; }
        Mensagem.Tentar(() => new ImpressaoGrid(_grid, _atual.Nome, _periodoGerado, _resumo).Visualizar());
    }

    private static string Slug(string texto)
    {
        var sb = new StringBuilder();
        foreach (char ch in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private void DesenharItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var def = (DefinicaoRelatorio)_lista.Items[e.Index];
        var g = e.Graphics;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(Tema.Cartao)) g.FillRectangle(b, e.Bounds);
        var r = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4);
        if (sel)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Tema.Arredondado(r, 6);
            using var b = new SolidBrush(Tema.Selecao);
            g.FillPath(b, path);
            using var bf = new SolidBrush(Tema.Primaria);
            g.FillRectangle(bf, r.X, r.Y + 8, 3, r.Height - 16);
        }
        var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, def.Nome, sel ? Tema.FonteNegrito : Tema.Fonte, new Rectangle(r.X + 12, r.Y + 5, r.Width - 16, 20),
            sel ? Tema.PrimariaHover : Tema.Texto, flags);
        TextRenderer.DrawText(g, def.Grupo, Tema.FontePequena, new Rectangle(r.X + 12, r.Y + 23, r.Width - 16, 16), Tema.TextoSuave, flags);
    }

    // ---------- definições ----------

    private static string N(int n, string singular, string plural) => n == 1 ? $"1 {singular}" : $"{n} {plural}";
    private const string Sep = "   •   ";

    private List<DefinicaoRelatorio> CriarRelatorios() => new()
    {
        new Relatorio<RelVendasDia>("Vendas por dia", "Vendas", "Quantidade de vendas, total e ticket médio de cada dia com venda.", true,
            g =>
            {
                g.Coluna("Data", "Data", 0, "dd/MM/yyyy  -  dddd");
                g.ColunaNumero("Quantidade", "Nº de vendas", 120, "N0");
                g.ColunaMoeda("Total", "Total", 140);
                g.ColunaMoeda("TicketMedio", "Ticket médio", 140);
            },
            RelatorioService.VendasPorDiaPeriodo,
            l =>
            {
                int qtd = l.Sum(x => x.Quantidade);
                decimal total = l.Sum(x => x.Total);
                return N(l.Count, "dia com venda", "dias com venda") + Sep + N(qtd, "venda", "vendas") + Sep +
                       "Total: " + Formato.Moeda(total) + Sep + "Ticket médio: " + Formato.Moeda(qtd > 0 ? Formato.Arredondar(total / qtd) : 0);
            }),

        new Relatorio<RelVendasProduto>("Vendas por produto", "Vendas", "Quantidade, receita (líquida de descontos), custo, lucro e margem por produto.", true,
            g =>
            {
                g.Coluna("Codigo", "Código", 90);
                g.Coluna("Descricao", "Descrição");
                g.ColunaNumero("Quantidade", "Qtd", 80);
                g.ColunaMoeda("Receita", "Receita", 120);
                g.ColunaMoeda("Custo", "Custo", 120);
                g.ColunaMoeda("Lucro", "Lucro", 120);
                g.ColunaNumero("Margem", "Margem %", 90, "N1");
            },
            RelatorioService.VendasPorProduto,
            l =>
            {
                decimal rec = l.Sum(x => x.Receita), custo = l.Sum(x => x.Custo);
                return N(l.Count, "produto", "produtos") + Sep + "Receita: " + Formato.Moeda(rec) + Sep + "Custo: " + Formato.Moeda(custo) + Sep +
                       "Lucro: " + Formato.Moeda(rec - custo) + Sep + "Margem: " + Formato.Percentual(rec > 0 ? Math.Round((rec - custo) / rec * 100, 1) : 0);
            },
            (g, e, item) =>
            {
                if (g.Columns[e.ColumnIndex].Name is "Lucro" or "Margem" && item.Lucro < 0) e.CellStyle!.ForeColor = Tema.Perigo;
            }),

        new Relatorio<RelVendasCliente>("Vendas por cliente", "Vendas", "Número de vendas, total e data da última compra de cada cliente.", true,
            g =>
            {
                g.Coluna("Cliente", "Cliente");
                g.ColunaNumero("Quantidade", "Nº de vendas", 120, "N0");
                g.ColunaMoeda("Total", "Total", 140);
                g.ColunaData("UltimaCompra", "Última compra");
            },
            RelatorioService.VendasPorCliente,
            l => N(l.Count, "cliente", "clientes") + Sep + N(l.Sum(x => x.Quantidade), "venda", "vendas") + Sep + "Total: " + Formato.Moeda(l.Sum(x => x.Total)),
            (g, e, item) =>
            {
                if (item.Cliente == "Consumidor final" && g.Columns[e.ColumnIndex].Name == "Cliente") e.CellStyle!.ForeColor = Tema.TextoSuave;
            }),

        new Relatorio<RelVendasForma>("Vendas por forma de pagamento", "Vendas", "Participação de cada forma de pagamento no faturamento.", true,
            g =>
            {
                g.Coluna("Forma", "Forma de pagamento");
                g.ColunaNumero("Quantidade", "Nº de vendas", 120, "N0");
                g.ColunaMoeda("Total", "Total", 140);
                g.ColunaNumero("Percentual", "% do total", 100, "N1");
            },
            RelatorioService.VendasPorForma,
            l => N(l.Sum(x => x.Quantidade), "venda", "vendas") + Sep + "Total: " + Formato.Moeda(l.Sum(x => x.Total))),

        new Relatorio<RelVendasVendedor>("Vendas por vendedor", "Vendas", "Desempenho de cada usuário que registrou vendas.", true,
            g =>
            {
                g.Coluna("Usuario", "Vendedor");
                g.ColunaNumero("Quantidade", "Nº de vendas", 120, "N0");
                g.ColunaMoeda("Total", "Total", 140);
                g.ColunaMoeda("TicketMedio", "Ticket médio", 140);
            },
            RelatorioService.VendasPorVendedor,
            l => N(l.Count, "vendedor", "vendedores") + Sep + N(l.Sum(x => x.Quantidade), "venda", "vendas") + Sep + "Total: " + Formato.Moeda(l.Sum(x => x.Total))),

        new Relatorio<RelComprasFornecedor>("Compras por fornecedor", "Compras", "Compras recebidas no período, por fornecedor (data de recebimento).", true,
            g =>
            {
                g.Coluna("Fornecedor", "Fornecedor");
                g.ColunaNumero("Quantidade", "Nº de compras", 120, "N0");
                g.ColunaMoeda("Total", "Total", 140);
                g.ColunaData("UltimaCompra", "Último recebimento");
            },
            RelatorioService.ComprasPorFornecedor,
            l => N(l.Count, "fornecedor", "fornecedores") + Sep + N(l.Sum(x => x.Quantidade), "compra", "compras") + Sep + "Total: " + Formato.Moeda(l.Sum(x => x.Total))),

        new Relatorio<RelPosicaoEstoque>("Posição de estoque", "Estoque", "Saldo atual, estoque mínimo e valor em estoque (a custo) dos produtos ativos.", false,
            g =>
            {
                g.Coluna("Codigo", "Código", 90);
                g.Coluna("Descricao", "Descrição");
                g.Coluna("Categoria", "Categoria", 140);
                g.Coluna("Unidade", "Un.", 50);
                g.ColunaNumero("Saldo", "Saldo", 90);
                g.ColunaNumero("Minimo", "Mínimo", 90);
                g.ColunaMoeda("Custo", "Custo", 110);
                g.ColunaMoeda("ValorEstoque", "Valor em estoque", 140);
            },
            (_, _) => RelatorioService.PosicaoEstoque(),
            l => N(l.Count, "produto", "produtos") + Sep + N(l.Count(x => x.Baixo), "abaixo do mínimo", "abaixo do mínimo") + Sep +
                 "Valor em estoque: " + Formato.Moeda(l.Sum(x => x.ValorEstoque)),
            (g, e, item) =>
            {
                if (item.Baixo && g.Columns[e.ColumnIndex].Name == "Saldo") { e.CellStyle!.ForeColor = Tema.Perigo; e.CellStyle.Font = Tema.FonteNegrito; }
            }),

        TitulosAbertos(TipoTitulo.Receber, "Contas a receber em aberto", "Cliente"),
        TitulosAbertos(TipoTitulo.Pagar, "Contas a pagar em aberto", "Fornecedor"),

        new Relatorio<RelFluxoCaixa>("Fluxo de caixa por dia", "Financeiro", "Entradas e saídas realizadas no caixa, com saldo acumulado a partir do saldo anterior.", true,
            g =>
            {
                g.Coluna("Data", "Data", 0, "dd/MM/yyyy  -  dddd");
                g.ColunaMoeda("Entradas", "Entradas", 130);
                g.ColunaMoeda("Saidas", "Saídas", 130);
                g.ColunaMoeda("SaldoDia", "Saldo do dia", 130);
                g.ColunaMoeda("SaldoAcumulado", "Saldo acumulado", 140);
            },
            (de, ate) =>
            {
                var l = RelatorioService.FluxoCaixa(de, ate, out decimal anterior);
                _saldoAnteriorFluxo = anterior;
                return l;
            },
            l =>
            {
                decimal ent = l.Sum(x => x.Entradas), sai = l.Sum(x => x.Saidas);
                return "Saldo anterior: " + Formato.Moeda(_saldoAnteriorFluxo) + Sep + "Entradas: " + Formato.Moeda(ent) + Sep +
                       "Saídas: " + Formato.Moeda(sai) + Sep + "Saldo final: " + Formato.Moeda(_saldoAnteriorFluxo + ent - sai);
            },
            (g, e, item) =>
            {
                string col = g.Columns[e.ColumnIndex].Name;
                if (col == "Entradas") e.CellStyle!.ForeColor = Tema.Sucesso;
                else if (col == "Saidas") e.CellStyle!.ForeColor = Tema.Perigo;
                else if ((col == "SaldoDia" && item.SaldoDia < 0) || (col == "SaldoAcumulado" && item.SaldoAcumulado < 0)) e.CellStyle!.ForeColor = Tema.Perigo;
            }),

        new Relatorio<RelResultado>("Resultado do período (DRE)", "Financeiro", "Demonstrativo simplificado: receita, custo das mercadorias, lucro bruto, despesas pagas e resultado.", true,
            g =>
            {
                g.Coluna("Descricao", "Descrição");
                g.Coluna("Valor", "Valor", 170, null, DataGridViewContentAlignment.MiddleRight);
                g.Coluna("Percentual", "% da receita", 130, null, DataGridViewContentAlignment.MiddleRight);
                foreach (DataGridViewColumn c in g.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            },
            RelatorioService.ResultadoPeriodo,
            l => l.Count > 0 ? "Resultado do período: " + l[^1].Valor : "",
            (g, e, item) =>
            {
                if (item.Destaque) e.CellStyle!.Font = Tema.FonteNegrito;
                if (item.Descricao.StartsWith("(=) Resultado") && item.Valor.Contains('-'))
                    e.CellStyle!.ForeColor = Tema.Perigo;
                else if (item.Descricao.StartsWith("(=) Resultado"))
                    e.CellStyle!.ForeColor = Tema.Sucesso;
            }),
    };

    private static DefinicaoRelatorio TitulosAbertos(TipoTitulo tipo, string nome, string tituloPessoa) =>
        new Relatorio<RelTituloAberto>(nome, "Financeiro",
            tipo == TipoTitulo.Receber ? "Títulos a receber em aberto, por vencimento, com dias em atraso." : "Títulos a pagar em aberto, por vencimento, com dias em atraso.",
            false,
            g =>
            {
                g.Coluna("Pessoa", tituloPessoa);
                g.Coluna("Descricao", "Descrição");
                g.Coluna("ParcelaTexto", "Parcela", 70, null, DataGridViewContentAlignment.MiddleCenter);
                g.ColunaData("Vencimento", "Vencimento");
                g.ColunaNumero("DiasAtraso", "Dias em atraso", 110, "N0");
                g.ColunaMoeda("Saldo", "Saldo", 130);
            },
            (_, _) => RelatorioService.TitulosEmAberto(tipo),
            l =>
            {
                var vencidos = l.Where(x => x.DiasAtraso > 0).ToList();
                return N(l.Count, "título", "títulos") + Sep + "Total em aberto: " + Formato.Moeda(l.Sum(x => x.Saldo)) + Sep +
                       "Vencido: " + Formato.Moeda(vencidos.Sum(x => x.Saldo)) + $" ({N(vencidos.Count, "título", "títulos")})";
            },
            (g, e, item) =>
            {
                if (item.DiasAtraso > 0 && g.Columns[e.ColumnIndex].Name is "DiasAtraso" or "Vencimento")
                {
                    e.CellStyle!.ForeColor = Tema.Perigo;
                    e.CellStyle.SelectionForeColor = Tema.Perigo;
                }
                else if (item.DiasAtraso == 0 && g.Columns[e.ColumnIndex].Name == "DiasAtraso")
                {
                    e.Value = "—";
                    e.FormattingApplied = true;
                }
            });

    /// <summary>Descreve um relatório: nome, se usa período, colunas, consulta e resumo.</summary>
    private abstract class DefinicaoRelatorio
    {
        public string Nome { get; }
        public string Grupo { get; }
        public string Descricao { get; }
        public bool UsaPeriodo { get; }

        protected DefinicaoRelatorio(string nome, string grupo, string descricao, bool usaPeriodo)
        {
            Nome = nome; Grupo = grupo; Descricao = descricao; UsaPeriodo = usaPeriodo;
        }

        public abstract void ConfigurarColunas(GridModerno grid);
        /// <summary>Consulta, carrega o grid e devolve o texto de resumo/totais.</summary>
        public abstract string Gerar(GridModerno grid, DateTime de, DateTime ate);
        public abstract void Formatar(GridModerno grid, DataGridViewCellFormattingEventArgs e);
        public override string ToString() => Nome;
    }

    /// <summary>Relatório tipado: o grid é carregado com o tipo concreto <typeparamref name="T"/> (necessário para ordenar).</summary>
    private sealed class Relatorio<T> : DefinicaoRelatorio where T : class
    {
        private readonly Action<GridModerno> _colunas;
        private readonly Func<DateTime, DateTime, List<T>> _consulta;
        private readonly Func<List<T>, string> _resumo;
        private readonly Action<GridModerno, DataGridViewCellFormattingEventArgs, T>? _formatar;

        public Relatorio(string nome, string grupo, string descricao, bool usaPeriodo, Action<GridModerno> colunas,
            Func<DateTime, DateTime, List<T>> consulta, Func<List<T>, string> resumo,
            Action<GridModerno, DataGridViewCellFormattingEventArgs, T>? formatar = null)
            : base(nome, grupo, descricao, usaPeriodo)
        {
            _colunas = colunas; _consulta = consulta; _resumo = resumo; _formatar = formatar;
        }

        public override void ConfigurarColunas(GridModerno grid) => _colunas(grid);

        public override string Gerar(GridModerno grid, DateTime de, DateTime ate)
        {
            var dados = _consulta(de, ate);
            grid.Carregar(dados);
            return dados.Count == 0 ? "Nenhum registro encontrado." : _resumo(dados);
        }

        public override void Formatar(GridModerno grid, DataGridViewCellFormattingEventArgs e)
        {
            if (_formatar != null && grid.Rows[e.RowIndex].DataBoundItem is T item) _formatar(grid, e, item);
        }
    }
}
