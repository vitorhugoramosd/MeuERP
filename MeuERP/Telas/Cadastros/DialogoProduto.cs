using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Cadastros;

/// <summary>Inclusão (produto = null) ou edição de produto.</summary>
public class DialogoProduto : DialogoBase
{
    private readonly Produto _p;
    private readonly bool _novo;
    private readonly TextBox _txtCodigo, _txtBarras, _txtDescricao;
    private readonly ComboBox _cmbCategoria, _cmbUnidade;
    private readonly CampoDecimal _txtCusto, _txtVenda, _txtMinimo;
    private readonly CampoDecimal? _txtEstoqueInicial;
    private readonly Label _lblMargem;
    private readonly CheckBox _chkAtivo;

    // 4 linhas de 56px + cabeçalho 52 + rodapé 58 + margens 30
    public DialogoProduto(Produto? produto)
        : base(produto == null ? "Novo produto" : "Editar produto", 700, 4 * 56 + 52 + 58 + 30)
    {
        _novo = produto == null;
        _p = produto ?? new Produto { Codigo = ProdutoRepository.ProximoCodigo() };

        var f = new PainelFormulario(4);
        _txtCodigo = f.Texto("Código", 1, 30);
        _txtBarras = f.Texto("Código de barras", 1, 50);
        _cmbCategoria = f.Combo("Categoria", 2);
        _txtDescricao = f.Texto("Descrição", 3, 150);
        _cmbUnidade = f.Combo("Unidade", 1);
        _txtCusto = f.Decimal("Preço de custo (R$)");
        _txtVenda = f.Decimal("Preço de venda (R$)");
        _lblMargem = f.Rotulo("Margem");
        _lblMargem.Font = Tema.FonteNegrito;
        _txtMinimo = f.Decimal("Estoque mínimo", 1, 3);
        if (_novo)
            _txtEstoqueInicial = f.Decimal("Estoque inicial", 1, 3);
        else
        {
            var lbl = f.Rotulo("Estoque atual (alterado em Estoque > Movimentações)", 2, $"{Formato.Qtd(_p.Estoque)} {_p.Unidade}");
            lbl.Font = Tema.FonteNegrito;
            if (_p.EstoqueBaixo) lbl.ForeColor = Tema.Perigo;
        }
        _chkAtivo = f.Check("Ativo", 1, true);
        Corpo.Controls.Add(f);

        // Categorias ativas + a categoria atual do produto (mesmo se inativa)
        var cats = CategoriaRepository.Listar().Where(c => c.Ativo || c.Id == _p.CategoriaId).Select(c => (c.Id, c.Nome));
        _cmbCategoria.Carregar(cats, "(sem categoria)");
        _cmbCategoria.SelecionarId(_p.CategoriaId);
        _cmbUnidade.CarregarTextos(Unidades.Todas.Contains(_p.Unidade) ? Unidades.Todas : Unidades.Todas.Append(_p.Unidade), _p.Unidade);

        _txtCodigo.Text = _p.Codigo;
        _txtBarras.Text = _p.CodigoBarras;
        _txtDescricao.Text = _p.Descricao;
        _txtCusto.Valor = _p.PrecoCusto;
        _txtVenda.Valor = _p.PrecoVenda;
        _txtMinimo.Valor = _p.EstoqueMinimo;
        _chkAtivo.Checked = _p.Ativo;

        _txtCusto.TextChanged += (_, _) => AtualizarMargem();
        _txtVenda.TextChanged += (_, _) => AtualizarMargem();
        AtualizarMargem();

        Shown += (_, _) => { _txtCodigo.Focus(); _txtCodigo.SelectAll(); };
    }

    private void AtualizarMargem()
    {
        if (!_txtCusto.Valido || !_txtVenda.Valido) { _lblMargem.Text = "—"; _lblMargem.ForeColor = Tema.TextoSuave; return; }
        decimal custo = _txtCusto.Valor, venda = _txtVenda.Valor;
        decimal margem = venda > 0 ? Math.Round((venda - custo) / venda * 100, 1) : 0;
        _lblMargem.Text = Formato.Percentual(margem);
        _lblMargem.ForeColor = margem < 0 ? Tema.Perigo : margem > 0 ? Tema.Sucesso : Tema.Texto;
    }

    protected override void AoSalvar()
    {
        if (!_txtCusto.Valido) { _txtCusto.Focus(); throw new RegraException("Preço de custo inválido."); }
        if (!_txtVenda.Valido) { _txtVenda.Focus(); throw new RegraException("Preço de venda inválido."); }
        if (!_txtMinimo.Valido) { _txtMinimo.Focus(); throw new RegraException("Estoque mínimo inválido."); }
        decimal inicial = 0;
        if (_txtEstoqueInicial != null)
        {
            if (!_txtEstoqueInicial.Valido || _txtEstoqueInicial.Valor < 0)
            {
                _txtEstoqueInicial.Focus();
                throw new RegraException("Estoque inicial inválido.");
            }
            inicial = _txtEstoqueInicial.Valor;
        }

        _p.Codigo = _txtCodigo.Text;
        _p.CodigoBarras = _txtBarras.Text;
        _p.Descricao = _txtDescricao.Text;
        _p.CategoriaId = _cmbCategoria.IdSelecionado();
        _p.Unidade = _cmbUnidade.SelectedItem as string ?? "UN";
        _p.PrecoCusto = _txtCusto.Valor;
        _p.PrecoVenda = _txtVenda.Valor;
        _p.EstoqueMinimo = _txtMinimo.Valor;
        _p.Ativo = _chkAtivo.Checked;

        ProdutoRepository.Salvar(_p);
        // Estoque inicial lançado uma única vez (se falhar, nova tentativa de salvar lança de novo)
        if (_novo && !_estoqueLancado && inicial > 0)
        {
            EstoqueService.MovimentarManual(_p.Id, TipoMov.Entrada, inicial, "Estoque inicial");
            _estoqueLancado = true;
        }
    }

    private bool _estoqueLancado;
}
