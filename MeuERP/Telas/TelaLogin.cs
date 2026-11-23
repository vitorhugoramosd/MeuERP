using MeuERP.Dados;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas;

public class TelaLogin : Form
{
    private readonly TextBox _txtLogin, _txtSenha;
    private readonly Label _lblErro;

    public TelaLogin()
    {
        Text = "MeuERP - Entrar";
        ClientSize = new Size(400, 470);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = Tema.Fonte;

        var faixa = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Tema.Sidebar };
        faixa.Controls.Add(new Label
        {
            Text = "MeuERP", Font = new Font("Segoe UI Semibold", 24f), ForeColor = Color.White,
            AutoSize = true, Location = new Point(36, 30), BackColor = Color.Transparent
        });
        faixa.Controls.Add(new Label
        {
            Text = "Gestão empresarial", Font = Tema.Fonte, ForeColor = Tema.SidebarTexto,
            AutoSize = true, Location = new Point(40, 80), BackColor = Color.Transparent
        });
        Controls.Add(faixa);

        Controls.Add(new Label { Text = "Usuário", Location = new Point(40, 160), AutoSize = true, ForeColor = Tema.TextoSuave });
        _txtLogin = new TextBox { Location = new Point(40, 182), Width = 320, Font = new Font("Segoe UI", 11f) };
        Controls.Add(_txtLogin);

        Controls.Add(new Label { Text = "Senha", Location = new Point(40, 228), AutoSize = true, ForeColor = Tema.TextoSuave });
        _txtSenha = new TextBox { Location = new Point(40, 250), Width = 320, Font = new Font("Segoe UI", 11f), UseSystemPasswordChar = true };
        Controls.Add(_txtSenha);

        _lblErro = new Label { Location = new Point(40, 288), Size = new Size(320, 22), ForeColor = Tema.Perigo };
        Controls.Add(_lblErro);

        var btn = new BotaoModerno("Entrar", EstiloBotao.Primario, Entrar) { Location = new Point(40, 318), Margin = Padding.Empty };
        btn.Size = new Size(320, 42);
        Controls.Add(btn);
        AcceptButton = btn;

        var dica = new Label
        {
            Location = new Point(40, 380), Size = new Size(320, 60), ForeColor = Tema.TextoSuave, Font = Tema.FontePequena,
            TextAlign = ContentAlignment.TopCenter
        };
        Controls.Add(dica);

        // Enquanto a senha padrão não for trocada, mostra a dica de primeiro acesso.
        var admin = UsuarioRepository.Listar().FirstOrDefault(u => u.Login.Equals("admin", StringComparison.OrdinalIgnoreCase));
        if (admin != null && Seguranca.Verificar("admin", admin.SenhaHash))
        {
            dica.Text = "Primeiro acesso: usuário admin, senha admin.\nTroque a senha em Configurações.";
            _txtLogin.Text = "admin";
        }

        Shown += (_, _) => (_txtLogin.TextLength > 0 ? _txtSenha : _txtLogin).Focus();
    }

    private void Entrar()
    {
        _lblErro.Text = "";
        if (_txtLogin.Text.Trim().Length == 0 || _txtSenha.TextLength == 0)
        {
            _lblErro.Text = "Informe usuário e senha.";
            return;
        }
        try
        {
            var u = UsuarioRepository.Autenticar(_txtLogin.Text, _txtSenha.Text);
            if (u == null)
            {
                _lblErro.Text = "Usuário ou senha inválidos.";
                _txtSenha.SelectAll();
                _txtSenha.Focus();
                return;
            }
            Sessao.Usuario = u;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Mensagem.Erro("Não foi possível acessar o banco de dados:\n\n" + ex.Message);
        }
    }
}
