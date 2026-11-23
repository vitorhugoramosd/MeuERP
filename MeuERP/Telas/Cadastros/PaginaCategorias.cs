using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Cadastros;

public class PaginaCategorias : PaginaLista<Categoria>
{
    public PaginaCategorias()
    {
        Titulo = "Categorias";
        Subtitulo = "Agrupamento dos produtos por categoria";
        TxtBusca.PlaceholderText = "Buscar categoria...";
        BtnNovo.Visible = BtnEditar.Visible = BtnExcluir.Visible = Sessao.PodeGerenciar;
    }

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.Coluna("Nome", "Nome");
        grid.Coluna("Ativo", "Ativo", 80);
    }

    protected override IEnumerable<Categoria> Carregar(string busca) => CategoriaRepository.Listar(busca);

    protected override bool Novo()
    {
        using var dlg = new DialogoCategoria(null);
        return dlg.Abrir(FindForm());
    }

    protected override bool Editar(Categoria item)
    {
        using var dlg = new DialogoCategoria(item);
        return dlg.Abrir(FindForm());
    }

    protected override string ConfirmacaoExclusao(Categoria item) => $"Deseja realmente excluir a categoria \"{item.Nome}\"?";

    protected override void Excluir(Categoria item) => CategoriaRepository.Excluir(item.Id);
}

public class DialogoCategoria : DialogoBase
{
    private readonly Categoria _cat;
    private readonly TextBox _txtNome;
    private readonly CheckBox _chkAtivo;

    // 1 linha de 56px + cabeçalho 52 + rodapé 58 + margens 30
    public DialogoCategoria(Categoria? categoria)
        : base(categoria == null ? "Nova categoria" : "Editar categoria", 440, 56 + 52 + 58 + 30)
    {
        // Trabalha sobre uma cópia para não alterar a linha do grid se o usuário cancelar
        _cat = categoria == null ? new Categoria() : new Categoria { Id = categoria.Id, Nome = categoria.Nome, Ativo = categoria.Ativo };

        var f = new PainelFormulario(3);
        _txtNome = f.Texto("Nome", 2, 80);
        _chkAtivo = f.Check("Ativo", 1, true);
        Corpo.Controls.Add(f);

        _txtNome.Text = _cat.Nome;
        _chkAtivo.Checked = _cat.Ativo;
        Shown += (_, _) => { _txtNome.Focus(); _txtNome.SelectAll(); };
    }

    protected override void AoSalvar()
    {
        _cat.Nome = _txtNome.Text;
        _cat.Ativo = _chkAtivo.Checked;
        CategoriaRepository.Salvar(_cat);
    }
}
