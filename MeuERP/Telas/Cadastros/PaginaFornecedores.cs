using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Cadastros;

public class PaginaFornecedores : PaginaLista<Fornecedor>
{
    private readonly CheckBox _chkAtivos;

    public PaginaFornecedores()
    {
        Titulo = "Fornecedores";
        Subtitulo = "Empresas e pessoas de quem a empresa compra";
        TxtBusca.PlaceholderText = "Buscar por nome, CPF/CNPJ, e-mail ou cidade...";
        TxtBusca.Width = 320;

        _chkAtivos = Ui.Check("Somente ativos");
        _chkAtivos.Checked = true;
        Filtros.Controls.Add(_chkAtivos);
        RecarregarAoMudar(_chkAtivos);

        BtnNovo.Visible = BtnEditar.Visible = BtnExcluir.Visible = Sessao.PodeGerenciar;
    }

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.Coluna("Nome", "Razão social / Nome");
        grid.Coluna("DocumentoFormatado", "CNPJ/CPF", 145);
        grid.Coluna("Telefone", "Telefone", 120);
        grid.Coluna("Email", "E-mail", 190);
        grid.Coluna("CidadeUf", "Cidade", 150);
        grid.Coluna("Ativo", "Ativo", 60);
    }

    protected override IEnumerable<Fornecedor> Carregar(string busca) => FornecedorRepository.Listar(busca, _chkAtivos.Checked);

    protected override bool Novo()
    {
        using var dlg = new DialogoPessoa(new Fornecedor { TipoPessoa = "J" });
        return dlg.Abrir(FindForm());
    }

    protected override bool Editar(Fornecedor item)
    {
        var f = FornecedorRepository.Obter(item.Id) ?? throw new RegraException("Fornecedor não encontrado. Ele pode ter sido excluído.");
        using var dlg = new DialogoPessoa(f);
        return dlg.Abrir(FindForm());
    }

    protected override string ConfirmacaoExclusao(Fornecedor item) => $"Deseja realmente excluir o fornecedor \"{item.Nome}\"?";

    protected override void Excluir(Fornecedor item) => FornecedorRepository.Excluir(item.Id);
}
