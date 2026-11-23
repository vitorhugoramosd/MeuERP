using MeuERP.Dados;
using MeuERP.Nucleo;
using MeuERP.Telas.Cadastros;
using MeuERP.Telas.Comercial;
using MeuERP.Telas.Financeiro;
using MeuERP.Telas.Gestao;
using MeuERP.UI;

namespace MeuERP.Telas;

/// <summary>Janela principal: menu lateral + área de páginas.</summary>
public class TelaPrincipal : Form
{
    private readonly Panel _area;
    private readonly FlowLayoutPanel _menu;
    private readonly List<ItemMenu> _itens = new();
    private PaginaBase? _pagina;

    /// <summary>True quando o usuário escolheu "Sair" (volta para o login) em vez de fechar o programa.</summary>
    public bool TrocarUsuario { get; private set; }

    public TelaPrincipal()
    {
        var empresa = EmpresaRepository.Obter();
        Text = "MeuERP" + (string.IsNullOrWhiteSpace(empresa.NomeFantasia) ? "" : " - " + empresa.NomeFantasia);
        Size = new Size(1320, 800);
        MinimumSize = new Size(1100, 660);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = Tema.Fundo;
        Font = Tema.Fonte;

        var lateral = new Panel { Dock = DockStyle.Left, Width = 228, BackColor = Tema.Sidebar };
        var logo = new Label
        {
            Text = "MeuERP", Dock = DockStyle.Top, Height = 62, Font = new Font("Segoe UI Semibold", 17f),
            ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 6, 0, 0)
        };

        var rodape = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = Tema.Sidebar };
        rodape.Controls.Add(new Label
        {
            Text = Sessao.Usuario?.Nome ?? "", Location = new Point(18, 10), Size = new Size(140, 20), AutoEllipsis = true,
            ForeColor = Color.White, Font = Tema.FonteNegrito
        });
        rodape.Controls.Add(new Label
        {
            Text = Tema.TextoStatus(Sessao.Usuario?.Perfil), Location = new Point(18, 32), Size = new Size(140, 18),
            ForeColor = Tema.SidebarSecao, Font = Tema.FontePequena
        });
        var sair = new LinkLabel
        {
            Text = "Sair", Location = new Point(170, 20), AutoSize = true, LinkColor = Tema.SidebarTexto,
            ActiveLinkColor = Color.White, LinkBehavior = LinkBehavior.HoverUnderline, Font = Tema.FonteNegrito
        };
        sair.LinkClicked += (_, _) =>
        {
            if (Mensagem.Confirmar("Deseja sair e trocar de usuário?")) { TrocarUsuario = true; Close(); }
        };
        rodape.Controls.Add(sair);

        _menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Tema.Sidebar
        };

        lateral.Controls.Add(_menu);
        lateral.Controls.Add(logo);
        lateral.Controls.Add(rodape);
        _menu.BringToFront();

        _area = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo };
        Controls.Add(_area);
        Controls.Add(lateral);
        _area.BringToFront();

        MontarMenu();
        Shown += (_, _) => { if (_itens.Count > 0) Navegar(_itens[0]); };
    }

    private void MontarMenu()
    {
        bool gerente = Sessao.PodeGerenciar, admin = Sessao.EhAdmin;

        Item("", "Dashboard", () => new PaginaDashboard());

        Secao("COMERCIAL");
        Item("", "Vendas", () => new PaginaVendas());
        if (gerente) Item("", "Compras", () => new PaginaCompras());

        Secao("CADASTROS");
        Item("", "Produtos", () => new PaginaProdutos());
        Item("", "Categorias", () => new PaginaCategorias());
        Item("", "Clientes", () => new PaginaClientes());
        if (gerente) Item("", "Fornecedores", () => new PaginaFornecedores());

        Secao("ESTOQUE");
        Item("", "Movimentações", () => new Estoque.PaginaEstoque());

        if (gerente)
        {
            Secao("FINANCEIRO");
            Item("", "Contas a Receber", () => new PaginaContasReceber());
            Item("", "Contas a Pagar", () => new PaginaContasPagar());
            Item("", "Caixa", () => new PaginaCaixa());
        }

        Secao("GESTÃO");
        if (gerente) Item("", "Relatórios", () => new PaginaRelatorios());
        if (admin) Item("", "Usuários", () => new PaginaUsuarios());
        Item("", "Configurações", () => new PaginaConfiguracoes());
    }

    private void Secao(string texto)
    {
        _menu.Controls.Add(new Label
        {
            Text = texto, Size = new Size(228, 24), Margin = Padding.Empty, Font = new Font("Segoe UI Semibold", 7.5f),
            ForeColor = Tema.SidebarSecao, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(18, 0, 0, 4)
        });
    }

    private void Item(string icone, string texto, Func<PaginaBase> fabrica)
    {
        var item = new ItemMenu(icone, texto, fabrica);
        item.Click += (_, _) => Navegar(item);
        _itens.Add(item);
        _menu.Controls.Add(item);
    }

    private void Navegar(ItemMenu item)
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            PaginaBase nova;
            try { nova = item.Fabrica(); }
            catch (Exception ex)
            {
                Mensagem.Erro($"Não foi possível abrir \"{item.Texto}\":\n\n{ex.Message}");
                return;
            }

            foreach (var i in _itens) i.Ativo = i == item;
            _area.SuspendLayout();
            var antiga = _pagina;
            _pagina = nova;
            nova.Dock = DockStyle.Fill;
            _area.Controls.Add(nova);
            nova.BringToFront();
            if (antiga != null) { _area.Controls.Remove(antiga); antiga.Dispose(); }
            _area.ResumeLayout();
            try { nova.AoExibir(); }
            catch (Exception ex) { Mensagem.Erro("Erro ao carregar a página:\n\n" + ex.Message); }
        }
        finally { Cursor = Cursors.Default; }
    }

    /// <summary>Navega para a página pelo texto do menu (ex.: "Vendas"). Usado por atalhos do dashboard.</summary>
    public void IrPara(string textoMenu)
    {
        var item = _itens.FirstOrDefault(i => i.Texto == textoMenu);
        if (item != null) Navegar(item);
    }

    private class ItemMenu : Control
    {
        private bool _hover, _ativo;
        private readonly string _icone;
        public string Texto { get; }
        public Func<PaginaBase> Fabrica { get; }

        public bool Ativo { get => _ativo; set { if (_ativo != value) { _ativo = value; Invalidate(); } } }

        public ItemMenu(string icone, string texto, Func<PaginaBase> fabrica)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _icone = icone;
            Texto = texto;
            Fabrica = fabrica;
            Size = new Size(228, 32);
            Margin = Padding.Empty;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_ativo || _hover ? Tema.SidebarHover : Tema.Sidebar);
            if (_ativo)
            {
                using var b = new SolidBrush(Tema.Primaria);
                g.FillRectangle(b, 0, 0, 3, Height);
            }
            Color cor = _ativo ? Color.White : Tema.SidebarTexto;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, _icone, Tema.FonteIcone, new Rectangle(18, 0, 24, Height), cor, flags);
            TextRenderer.DrawText(g, Texto, _ativo ? Tema.FonteNegrito : Tema.Fonte, new Rectangle(48, 0, Width - 52, Height), cor, flags);
        }
    }
}
