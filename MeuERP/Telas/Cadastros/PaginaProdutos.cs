using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Cadastros;

public class PaginaProdutos : PaginaLista<Produto>
{
    private readonly ComboBox _cmbCategoria;
    private readonly CheckBox _chkAtivos, _chkEstoqueBaixo;

    public PaginaProdutos()
    {
        Titulo = "Produtos";
        Subtitulo = "Cadastro de produtos, preços e níveis de estoque";
        TxtBusca.PlaceholderText = "Buscar por código, descrição ou código de barras...";
        TxtBusca.Width = 320;

        _cmbCategoria = Ui.Combo(190);
        _cmbCategoria.Carregar(CategoriaRepository.Listar().Select(c => (c.Id, c.Nome)), "Todas as categorias");
        _chkAtivos = Ui.Check("Somente ativos");
        _chkAtivos.Checked = true;
        _chkEstoqueBaixo = Ui.Check("Estoque baixo");
        Filtros.Controls.Add(_cmbCategoria);
        Filtros.Controls.Add(_chkAtivos);
        Filtros.Controls.Add(_chkEstoqueBaixo);
        RecarregarAoMudar(_cmbCategoria, _chkAtivos, _chkEstoqueBaixo);

        // Operador: somente consulta
        bool pode = Sessao.PodeGerenciar;
        BtnNovo.Visible = BtnEditar.Visible = BtnExcluir.Visible = pode;

        Grid.CellFormatting += Grid_CellFormatting;
    }

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.Coluna("Codigo", "Código", 90);
        grid.Coluna("Descricao", "Descrição");
        grid.Coluna("CategoriaNome", "Categoria", 140);
        grid.Coluna("Unidade", "Un.", 50);
        grid.ColunaMoeda("PrecoCusto", "Custo", 100);
        grid.ColunaMoeda("PrecoVenda", "Preço de venda", 115);
        grid.ColunaNumero("Margem", "Margem %", 85, "N1");
        grid.ColunaNumero("Estoque", "Estoque", 85);
        grid.ColunaNumero("EstoqueMinimo", "Mínimo", 80);
        grid.Coluna("Ativo", "Ativo", 60);
    }

    protected override IEnumerable<Produto> Carregar(string busca) =>
        ProdutoRepository.Listar(busca, _chkAtivos.Checked, _cmbCategoria.IdSelecionado(), _chkEstoqueBaixo.Checked);

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        if (Grid.Rows[e.RowIndex].DataBoundItem is not Produto p) return;
        if (!p.Ativo)
        {
            e.CellStyle.ForeColor = Tema.TextoSuave;
            e.CellStyle.SelectionForeColor = Tema.TextoSuave;
        }
        if (p.EstoqueBaixo && Grid.Columns[e.ColumnIndex].Name == "Estoque")
        {
            e.CellStyle.ForeColor = Tema.Perigo;
            e.CellStyle.SelectionForeColor = Tema.Perigo;
            e.CellStyle.Font = Tema.FonteNegrito;
        }
    }

    protected override string Rodape(IReadOnlyList<Produto> itens)
    {
        string qtd = itens.Count == 1 ? "1 registro" : $"{itens.Count} registros";
        decimal total = Formato.Arredondar(itens.Sum(p => p.ValorEmEstoque));
        return $"{qtd}   •   Valor total em estoque (custo): {Formato.Moeda(total)}";
    }

    protected override bool Novo()
    {
        using var dlg = new DialogoProduto(null);
        return dlg.Abrir(FindForm());
    }

    protected override bool Editar(Produto item)
    {
        var p = ProdutoRepository.Obter(item.Id) ?? throw new RegraException("Produto não encontrado. Ele pode ter sido excluído.");
        using var dlg = new DialogoProduto(p);
        return dlg.Abrir(FindForm());
    }

    protected override string ConfirmacaoExclusao(Produto item) =>
        $"Deseja realmente excluir o produto \"{item.Codigo} - {item.Descricao}\"?";

    protected override void Excluir(Produto item) => ProdutoRepository.Excluir(item.Id);
}
