using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Cadastros;

/// <summary>Cadastro de usuários (acessível apenas ao administrador).</summary>
public class PaginaUsuarios : PaginaLista<Usuario>
{
    public PaginaUsuarios()
    {
        Titulo = "Usuários";
        Subtitulo = "Contas de acesso ao sistema e perfis de permissão";
        TxtBusca.PlaceholderText = "Buscar por nome ou login...";
        BtnNovo.Visible = BtnEditar.Visible = BtnExcluir.Visible = Sessao.EhAdmin;
    }

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.Coluna("Nome", "Nome");
        grid.Coluna("Login", "Login", 150);
        grid.ColunaStatus("Perfil", "Perfil", 130);
        grid.Coluna("Ativo", "Ativo", 70);
        grid.ColunaData("CriadoEm", "Criado em", true);
    }

    protected override IEnumerable<Usuario> Carregar(string busca) => UsuarioRepository.Listar(busca);

    protected override bool Novo()
    {
        using var dlg = new DialogoUsuario(null);
        return dlg.Abrir(FindForm());
    }

    protected override bool Editar(Usuario item)
    {
        var u = UsuarioRepository.Obter(item.Id) ?? throw new RegraException("Usuário não encontrado. Ele pode ter sido excluído.");
        using var dlg = new DialogoUsuario(u);
        return dlg.Abrir(FindForm());
    }

    protected override string ConfirmacaoExclusao(Usuario item) => $"Deseja realmente excluir o usuário \"{item.Nome}\" ({item.Login})?";

    protected override void Excluir(Usuario item) => UsuarioRepository.Excluir(item.Id);
}

public class DialogoUsuario : DialogoBase
{
    private readonly Usuario _u;
    private readonly TextBox _txtNome, _txtLogin, _txtSenha, _txtConfirmar;
    private readonly ComboBox _cmbPerfil;
    private readonly CheckBox _chkAtivo;

    // 3 linhas de 56px + cabeçalho 52 + rodapé 58 + margens 30
    public DialogoUsuario(Usuario? usuario)
        : base(usuario == null ? "Novo usuário" : "Editar usuário", 600, 3 * 56 + 52 + 58 + 30)
    {
        _u = usuario ?? new Usuario();
        bool novo = _u.Id == 0;

        var f = new PainelFormulario(4);
        _txtNome = f.Texto("Nome", 2, 100);
        _txtLogin = f.Texto("Login", 1, 40);
        _cmbPerfil = f.Combo("Perfil", 1);
        _txtSenha = f.Senha(novo ? "Senha" : "Nova senha (deixe em branco para manter)", 2);
        _txtConfirmar = f.Senha("Confirmar senha", 2);
        _chkAtivo = f.Check("Ativo", 1, true);
        Corpo.Controls.Add(f);

        _cmbPerfil.CarregarTextos(Perfis.Todos.Select(Tema.TextoStatus), Tema.TextoStatus(_u.Perfil));
        _txtNome.Text = _u.Nome;
        _txtLogin.Text = _u.Login;
        _chkAtivo.Checked = _u.Ativo;

        Shown += (_, _) => { _txtNome.Focus(); _txtNome.SelectAll(); };
    }

    protected override void AoSalvar()
    {
        string senha = _txtSenha.Text;
        if (senha != _txtConfirmar.Text)
        {
            _txtConfirmar.Focus();
            _txtConfirmar.SelectAll();
            throw new RegraException("A confirmação não confere com a senha informada.");
        }

        _u.Nome = _txtNome.Text;
        _u.Login = _txtLogin.Text;
        _u.Perfil = _cmbPerfil.SelectedIndex >= 0 ? Perfis.Todos[_cmbPerfil.SelectedIndex] : Perfis.Operador;
        _u.Ativo = _chkAtivo.Checked;
        UsuarioRepository.Salvar(_u, senha.Length > 0 ? senha : null);

        // Mantém a sessão coerente se o admin editou o próprio cadastro
        if (Sessao.Usuario != null && Sessao.Usuario.Id == _u.Id)
        {
            Sessao.Usuario.Nome = _u.Nome;
            Sessao.Usuario.Login = _u.Login;
        }
    }
}
