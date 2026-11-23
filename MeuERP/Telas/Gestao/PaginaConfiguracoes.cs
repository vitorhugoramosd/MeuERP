using System.Diagnostics;
using System.Runtime.InteropServices;
using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Gestao;

/// <summary>Dados da empresa, troca de senha, backup, dados de demonstração e informações do sistema.</summary>
public class PaginaConfiguracoes : PaginaBase
{
    private const int LarguraMaxima = 940;

    private readonly FlowLayoutPanel _coluna;
    private readonly bool _admin = Sessao.EhAdmin;

    // empresa
    private TextBox _razao = null!, _fantasia = null!, _cnpj = null!, _ie = null!, _telefone = null!, _email = null!,
        _cep = null!, _endereco = null!, _cidade = null!;
    private ComboBox _uf = null!;
    // senha
    private TextBox _senhaAtual = null!, _senhaNova = null!, _senhaConfirma = null!;

    public PaginaConfiguracoes()
    {
        Titulo = "Configurações";
        Subtitulo = _admin ? "Empresa, segurança, backup e dados do sistema" : "Dados da empresa e sua senha de acesso";

        _coluna = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Tema.Fundo, Padding = new Padding(0, 0, 0, 12)
        };
        Conteudo.Controls.Add(_coluna);
        _coluna.BringToFront();

        _coluna.Controls.Add(SecaoEmpresa());
        _coluna.Controls.Add(SecaoSenha());
        if (_admin)
        {
            _coluna.Controls.Add(SecaoBackup());
            _coluna.Controls.Add(SecaoDemo());
        }
        _coluna.Controls.Add(SecaoSobre());

        _coluna.Resize += (_, _) => AjustarLarguras();
    }

    public override void AoExibir()
    {
        AjustarLarguras();
        try { CarregarEmpresa(); }
        catch (Exception ex) { Mensagem.Erro("Não foi possível carregar as configurações:\n\n" + ex.Message); }
        // O foco inicial cai num campo do meio da página e o painel rola até ele: volta ao topo.
        BeginInvoke(() => { ActiveControl = null; _coluna.AutoScrollPosition = Point.Empty; });
    }

    private void AjustarLarguras()
    {
        int largura = Math.Min(LarguraMaxima, _coluna.ClientSize.Width - _coluna.Padding.Horizontal - 2);
        if (largura < 300) return;
        _coluna.SuspendLayout();
        foreach (Control c in _coluna.Controls) c.Width = largura;
        _coluna.ResumeLayout();
    }

    // ---------- montagem ----------

    /// <summary>Cartão de seção: título, texto de ajuda opcional e os blocos empilhados (de cima para baixo).</summary>
    private static Cartao Secao(string titulo, string? ajuda, params Control[] blocos)
    {
        var cartao = new Cartao(titulo) { Margin = new Padding(0, 0, 0, 16), Width = LarguraMaxima, Padding = new Padding(16, 44, 16, 14) };
        var todos = new List<Control>();
        if (!string.IsNullOrEmpty(ajuda))
            todos.Add(new Label { Text = ajuda, Height = 26, ForeColor = Tema.TextoSuave, Font = Tema.Fonte, AutoEllipsis = true });
        todos.AddRange(blocos);
        // Dock=Top empilha na ordem inversa da inclusão: adiciona de baixo para cima
        for (int i = todos.Count - 1; i >= 0; i--)
        {
            todos[i].Dock = DockStyle.Top;
            cartao.Controls.Add(todos[i]);
        }
        cartao.Height = cartao.Padding.Vertical + todos.Sum(c => c.Height);
        return cartao;
    }

    private static FlowLayoutPanel BarraBotoes(params Control[] controles)
    {
        var barra = new FlowLayoutPanel { Height = 46, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
        foreach (var c in controles)
        {
            c.Margin = c is BotaoModerno ? new Padding(0, 0, 8, 0) : new Padding(4, 8, 8, 0);
            barra.Controls.Add(c);
        }
        return barra;
    }

    private Cartao SecaoEmpresa()
    {
        var f = new PainelFormulario(4);
        _razao = f.Texto("Razão social", 2, 120);
        _fantasia = f.Texto("Nome fantasia", 2, 120);
        _cnpj = f.Texto("CNPJ", 1, 18);
        _ie = f.Texto("Inscrição estadual", 1, 20);
        _telefone = f.Texto("Telefone", 1, 20);
        _email = f.Texto("E-mail", 1, 120);
        _cep = f.Texto("CEP", 1, 9);
        _endereco = f.Texto("Endereço", 3, 160);
        _cidade = f.Texto("Cidade", 2, 80);
        _uf = f.Combo("UF");
        _uf.CarregarTextos(new[] { "" }.Concat(Ufs.Todas));

        _cnpj.Leave += (_, _) =>
        {
            string d = Formato.SoDigitos(_cnpj.Text);
            if (d.Length == 14) _cnpj.Text = Formato.Documento(d);
        };
        _cep.Leave += (_, _) =>
        {
            string d = Formato.SoDigitos(_cep.Text);
            if (d.Length == 8) _cep.Text = d[..5] + "-" + d[5..];
        };

        if (!_admin)
        {
            foreach (var t in new[] { _razao, _fantasia, _cnpj, _ie, _telefone, _email, _cep, _endereco, _cidade })
            {
                t.ReadOnly = true;
                t.BackColor = Tema.Cabecalho;
            }
            _uf.Enabled = false;
            return Secao("Dados da empresa", "Somente o administrador pode alterar os dados da empresa.", f);
        }

        var salvar = new BotaoModerno("Salvar dados da empresa", EstiloBotao.Primario, SalvarEmpresa);
        return Secao("Dados da empresa", "Aparecem no título da janela e no cabeçalho dos relatórios impressos.", f, BarraBotoes(salvar));
    }

    private Cartao SecaoSenha()
    {
        var f = new PainelFormulario(3);
        _senhaAtual = f.Senha("Senha atual");
        _senhaNova = f.Senha("Nova senha");
        _senhaConfirma = f.Senha("Confirme a nova senha");
        _senhaConfirma.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; AlterarSenha(); } };
        var botao = new BotaoModerno("Alterar senha", EstiloBotao.Primario, AlterarSenha);
        return Secao("Minha senha", $"Usuário: {Sessao.Usuario?.Nome} ({Sessao.Usuario?.Login}). A nova senha deve ter pelo menos 4 caracteres.",
            f, BarraBotoes(botao));
    }

    private Cartao SecaoBackup()
    {
        var caminho = new Label
        {
            Text = "Banco de dados: " + Database.Caminho, Height = 24, ForeColor = Tema.Texto, Font = Tema.Fonte, AutoEllipsis = true
        };
        var backup = new BotaoModerno("Fazer backup...", EstiloBotao.Primario, FazerBackup);
        var restaurar = new BotaoModerno("Restaurar backup...", EstiloBotao.Perigo, RestaurarBackup);
        var abrir = new BotaoModerno("Abrir pasta", EstiloBotao.Secundario, AbrirPasta);
        return Secao("Backup", "Faça cópias de segurança com frequência e guarde-as fora deste computador (pen drive, nuvem).",
            caminho, BarraBotoes(backup, restaurar, abrir));
    }

    private Cartao SecaoDemo()
    {
        var botao = new BotaoModerno("Gerar dados de demonstração", EstiloBotao.Secundario, GerarDemo);
        return Secao("Dados de demonstração",
            "Cria produtos, clientes, fornecedores e cerca de 6 meses de vendas, compras e financeiro para conhecer o sistema.",
            BarraBotoes(botao));
    }

    private Cartao SecaoSobre()
    {
        var linhas = new (string, string)[]
        {
            ("Sistema", "MeuERP — gestão comercial para pequenas empresas"),
            ("Versão", Application.ProductVersion.Split('+')[0]),
            (".NET", RuntimeInformation.FrameworkDescription),
            ("Sistema operacional", RuntimeInformation.OSDescription),
            ("Banco de dados", Database.Caminho),
        };
        var tabela = new TableLayoutPanel { ColumnCount = 2, RowCount = linhas.Length, Height = linhas.Length * 24, BackColor = Color.Transparent };
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < linhas.Length; i++)
        {
            tabela.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            tabela.Controls.Add(new Label { Text = linhas[i].Item1, Dock = DockStyle.Fill, ForeColor = Tema.TextoSuave, Font = Tema.Fonte, TextAlign = ContentAlignment.MiddleLeft }, 0, i);
            tabela.Controls.Add(new Label { Text = linhas[i].Item2, Dock = DockStyle.Fill, ForeColor = Tema.Texto, Font = Tema.Fonte, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 1, i);
        }
        return Secao("Sobre", null, tabela);
    }

    // ---------- ações ----------

    private void CarregarEmpresa()
    {
        var e = EmpresaRepository.Obter();
        _razao.Text = e.RazaoSocial;
        _fantasia.Text = e.NomeFantasia;
        _cnpj.Text = Formato.SoDigitos(e.Cnpj).Length == 14 ? Formato.Documento(e.Cnpj) : e.Cnpj;
        _ie.Text = e.InscricaoEstadual;
        _telefone.Text = e.Telefone;
        _email.Text = e.Email;
        _cep.Text = e.Cep;
        _endereco.Text = e.Endereco;
        _cidade.Text = e.Cidade;
        _uf.SelectedItem = Ufs.Todas.Contains(e.Uf) ? e.Uf : "";
    }

    private void SalvarEmpresa()
    {
        if (!_admin) return;
        var e = new Empresa
        {
            RazaoSocial = _razao.Text.Trim(), NomeFantasia = _fantasia.Text.Trim(),
            Cnpj = Formato.SoDigitos(_cnpj.Text).Length == 14 ? Formato.Documento(_cnpj.Text) : _cnpj.Text.Trim(),
            InscricaoEstadual = _ie.Text.Trim(), Telefone = _telefone.Text.Trim(), Email = _email.Text.Trim(),
            Cep = _cep.Text.Trim(), Endereco = _endereco.Text.Trim(), Cidade = _cidade.Text.Trim(), Uf = _uf.SelectedItem as string ?? ""
        };
        if (Mensagem.Tentar(() =>
            {
                if (e.Email.Length > 0 && !e.Email.Contains('@')) throw new RegraException("E-mail inválido.");
                EmpresaRepository.Salvar(e);
            }))
            Mensagem.Info("Dados da empresa salvos. O título da janela é atualizado no próximo acesso.");
    }

    private void AlterarSenha()
    {
        bool ok = Mensagem.Tentar(() =>
        {
            if (Sessao.UsuarioId is not int id) throw new RegraException("Nenhum usuário conectado.");
            if (_senhaAtual.Text.Length == 0) throw new RegraException("Informe a senha atual.");
            if (_senhaNova.Text.Length == 0) throw new RegraException("Informe a nova senha.");
            if (_senhaNova.Text != _senhaConfirma.Text) throw new RegraException("A confirmação não confere com a nova senha.");
            if (_senhaNova.Text == _senhaAtual.Text) throw new RegraException("A nova senha deve ser diferente da atual.");
            UsuarioRepository.AlterarSenha(id, _senhaAtual.Text, _senhaNova.Text);
        });
        if (!ok) return;
        _senhaAtual.Clear();
        _senhaNova.Clear();
        _senhaConfirma.Clear();
        Mensagem.Info("Senha alterada com sucesso.");
    }

    private void FazerBackup()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Salvar backup", Filter = "Backup do MeuERP (*.db)|*.db|Todos os arquivos (*.*)|*.*",
            FileName = $"meuerp-backup-{DateTime.Now:yyyyMMdd-HHmm}.db", OverwritePrompt = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        if (string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(Database.Caminho), StringComparison.OrdinalIgnoreCase))
        {
            Mensagem.Aviso("Escolha um arquivo diferente do banco de dados em uso.");
            return;
        }
        Cursor = Cursors.WaitCursor;
        bool ok;
        try { ok = Mensagem.Tentar(() => Database.Backup(dlg.FileName)); }
        finally { Cursor = Cursors.Default; }
        if (ok) Mensagem.Info("Backup gerado com sucesso:\n\n" + dlg.FileName);
    }

    private void RestaurarBackup()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Restaurar backup", Filter = "Backup do MeuERP (*.db)|*.db|Todos os arquivos (*.*)|*.*", CheckFileExists = true
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
        if (string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(Database.Caminho), StringComparison.OrdinalIgnoreCase))
        {
            Mensagem.Aviso("Este é o próprio banco de dados em uso. Escolha um arquivo de backup.");
            return;
        }

        var info = new FileInfo(dlg.FileName);
        using (var confirma = new DialogoConfirmacaoForte(
                   "Restaurar backup",
                   $"Todos os dados atuais serão SUBSTITUÍDOS pelo conteúdo do backup:\n\n{info.Name}\n" +
                   $"de {Formato.DataHora(info.LastWriteTime)} ({info.Length / 1024.0:N0} KB).\n\n" +
                   "Uma cópia do banco atual é guardada ao lado dele (\".antes-da-restauracao\"). " +
                   "O sistema será reiniciado em seguida.",
                   "RESTAURAR"))
        {
            if (!confirma.Abrir(FindForm())) return;
        }

        Cursor = Cursors.WaitCursor;
        bool ok;
        try { ok = Mensagem.Tentar(() => Database.Restaurar(dlg.FileName)); }
        finally { Cursor = Cursors.Default; }
        if (!ok) return;
        Mensagem.Info("Backup restaurado com sucesso.\n\nO sistema precisa ser reiniciado e será aberto novamente agora.");
        Application.Restart();
    }

    private void AbrirPasta()
    {
        Mensagem.Tentar(() =>
        {
            if (File.Exists(Database.Caminho))
                Process.Start("explorer.exe", $"/select,\"{Database.Caminho}\"");
            else
                Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(Database.Caminho)}\"");
        });
    }

    private void GerarDemo()
    {
        long vendas, produtos;
        using (var c = Database.Abrir())
        {
            vendas = c.ExecuteScalar<long>("SELECT COUNT(*) FROM vendas");
            produtos = c.ExecuteScalar<long>("SELECT COUNT(*) FROM produtos");
        }
        if (!Mensagem.Confirmar("Gerar dados de demonstração?\n\nSerão criados produtos, clientes, fornecedores, os usuários " +
                                "\"gerente\" e \"vendedor\" e cerca de 6 meses de vendas, compras e lançamentos financeiros.\n\n" +
                                "Recomendado apenas em um banco novo, para conhecer o sistema."))
            return;
        if ((vendas > 0 || produtos > 1) &&
            !Mensagem.Confirmar($"ATENÇÃO: este banco já possui dados ({produtos} produtos e {vendas} vendas).\n\n" +
                                "Os dados de demonstração serão MISTURADOS aos dados reais e não há como desfazer " +
                                "automaticamente (só restaurando um backup).\n\nDeseja continuar mesmo assim?"))
            return;

        string resumo = "";
        Cursor = Cursors.WaitCursor;
        bool ok;
        try { ok = Mensagem.Tentar(() => resumo = DadosDemo.Gerar()); }
        finally { Cursor = Cursors.Default; }
        if (ok) Mensagem.Info("Dados de demonstração gerados com sucesso!\n\n" + resumo + "\n\nAbra o Dashboard para ver os indicadores.");
    }

    /// <summary>Confirmação de ação irreversível: o usuário precisa digitar uma palavra.</summary>
    private sealed class DialogoConfirmacaoForte : DialogoBase
    {
        private readonly TextBox _txt;
        private readonly string _palavra;

        public DialogoConfirmacaoForte(string titulo, string texto, string palavra) : base(titulo, 520, 340)
        {
            _palavra = palavra;
            BtnSalvar.Text = "Confirmar";
            BtnSalvar.Estilo = EstiloBotao.Perigo;
            var f = new PainelFormulario(1);
            _txt = f.Texto($"Para confirmar, digite {palavra}:");
            var lbl = new Label
            {
                Text = texto, Dock = DockStyle.Top, Height = 150, ForeColor = Tema.Texto, Font = Tema.Fonte,
                Padding = new Padding(0, 0, 10, 0)
            };
            Corpo.Controls.Add(f);
            Corpo.Controls.Add(lbl);
            Shown += (_, _) => _txt.Focus();
        }

        protected override void AoSalvar()
        {
            if (!string.Equals(_txt.Text.Trim(), _palavra, StringComparison.Ordinal))
                throw new RegraException($"Digite {_palavra} (em maiúsculas) para confirmar.");
        }
    }
}
