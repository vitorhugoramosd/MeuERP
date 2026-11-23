using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Cadastros;

public class PaginaClientes : PaginaLista<Cliente>
{
    private readonly CheckBox _chkAtivos;

    public PaginaClientes()
    {
        Titulo = "Clientes";
        Subtitulo = "Pessoas físicas e jurídicas que compram da empresa";
        TxtBusca.PlaceholderText = "Buscar por nome, CPF/CNPJ, e-mail ou cidade...";
        TxtBusca.Width = 320;

        _chkAtivos = Ui.Check("Somente ativos");
        _chkAtivos.Checked = true;
        Filtros.Controls.Add(_chkAtivos);
        RecarregarAoMudar(_chkAtivos);

        // Qualquer perfil inclui e edita; excluir só gerente/admin
        BtnExcluir.Visible = Sessao.PodeGerenciar;
    }

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.Coluna("Nome", "Nome / Razão social");
        grid.Coluna("DocumentoFormatado", "CPF/CNPJ", 145);
        grid.Coluna("Telefone", "Telefone", 120);
        grid.Coluna("Email", "E-mail", 190);
        grid.Coluna("CidadeUf", "Cidade", 150);
        grid.ColunaMoeda("LimiteCredito", "Limite de crédito", 125);
        grid.Coluna("Ativo", "Ativo", 60);
    }

    protected override IEnumerable<Cliente> Carregar(string busca) => ClienteRepository.Listar(busca, _chkAtivos.Checked);

    protected override bool Novo()
    {
        using var dlg = new DialogoPessoa(new Cliente { TipoPessoa = "F" });
        return dlg.Abrir(FindForm());
    }

    protected override bool Editar(Cliente item)
    {
        var c = ClienteRepository.Obter(item.Id) ?? throw new RegraException("Cliente não encontrado. Ele pode ter sido excluído.");
        using var dlg = new DialogoPessoa(c);
        return dlg.Abrir(FindForm());
    }

    protected override string ConfirmacaoExclusao(Cliente item) => $"Deseja realmente excluir o cliente \"{item.Nome}\"?";

    protected override void Excluir(Cliente item) => ClienteRepository.Excluir(item.Id);
}
