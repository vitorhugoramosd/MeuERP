using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Estoque;

/// <summary>Movimentação manual de estoque: entrada, saída ou ajuste de inventário.</summary>
public class DialogoMovimentacao : DialogoBase
{
    private static readonly (string Texto, string Codigo)[] Tipos =
    {
        ("Entrada", TipoMov.Entrada), ("Saída", TipoMov.Saida), ("Ajuste de inventário", TipoMov.Ajuste)
    };

    private Produto? _produto;
    private readonly Label _lblProduto, _lblSaldo, _lblAjuda, _lblApos;
    private readonly ComboBox _cbTipo;
    private readonly CampoDecimal _txtQtd, _txtCusto;
    private readonly Label _rotQtd, _rotObs;
    private readonly Control _celulaCusto;
    private readonly TextBox _txtObs;

    private string TipoSelecionado => Tipos[Math.Max(0, _cbTipo.SelectedIndex)].Codigo;

    public DialogoMovimentacao(Produto? produto = null) : base("Nova movimentação de estoque", 620, 490)
    {
        // ---- Produto e tipo
        var form1 = new PainelFormulario(4);
        _lblProduto = form1.Rotulo("Produto", 3);
        _lblProduto.BorderStyle = BorderStyle.FixedSingle;
        _lblProduto.Padding = new Padding(4, 0, 0, 0);
        form1.Adicionar("", new BotaoModerno("Selecionar", EstiloBotao.Secundario, SelecionarProduto));
        _lblSaldo = form1.Rotulo("Saldo atual", 4);
        _lblSaldo.Font = Tema.FonteNegrito;

        _cbTipo = form1.Combo("Tipo de movimentação", 2);
        _cbTipo.CarregarTextos(Tipos.Select(t => t.Texto));
        _txtQtd = form1.Decimal("Quantidade", 1, 3);
        _txtCusto = form1.Decimal("Custo unitário (opcional)", 1, 2);
        _rotQtd = RotuloDe(_txtQtd);
        _celulaCusto = _txtCusto.Parent!;

        // ---- Linha de ajuda
        _lblAjuda = new Label
        {
            Dock = DockStyle.Top, Height = 34, ForeColor = Tema.TextoSuave, Font = Tema.FontePequena,
            TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 0, 10, 0)
        };

        // ---- Observação e resultado
        var form2 = new PainelFormulario(4);
        _txtObs = form2.Multilinha("Observação", 4, 86);
        _rotObs = RotuloDe(_txtObs);
        _lblApos = form2.Rotulo("", 4);
        _lblApos.Font = Tema.FonteSubtitulo;

        // Dock Top: o último adicionado fica no topo.
        Corpo.Controls.Add(form2);
        Corpo.Controls.Add(_lblAjuda);
        Corpo.Controls.Add(form1);

        _cbTipo.SelectedIndexChanged += (_, _) => AoMudarTipo();
        _txtQtd.TextChanged += (_, _) => AtualizarSaldoApos();

        _produto = produto;
        MostrarProduto();
        AoMudarTipo();

        Shown += (_, _) => { if (_produto == null) SelecionarProduto(); _txtQtd.Focus(); };
    }

    /// <summary>Rótulo que o PainelFormulario criou acima do controle.</summary>
    private static Label RotuloDe(Control c) => c.Parent!.Controls.OfType<Label>().First(l => l != c);

    private void SelecionarProduto()
    {
        var p = Seletor.Produto();
        if (p == null) return;
        _produto = ProdutoRepository.Obter(p.Id) ?? p;
        MostrarProduto();
        if (TipoSelecionado == TipoMov.Ajuste) _txtQtd.Valor = _produto.Estoque;
        _txtQtd.Focus();
    }

    private void MostrarProduto()
    {
        if (_produto == null)
        {
            _lblProduto.Text = "(nenhum produto selecionado)";
            _lblProduto.ForeColor = Tema.TextoSuave;
            _lblSaldo.Text = "—";
        }
        else
        {
            _lblProduto.Text = $"{_produto.Codigo} - {_produto.Descricao}";
            _lblProduto.ForeColor = Tema.Texto;
            _lblSaldo.Text = $"{Formato.Qtd(_produto.Estoque)} {_produto.Unidade}" +
                             (_produto.EstoqueMinimo > 0 ? $"   (mínimo: {Formato.Qtd(_produto.EstoqueMinimo)})" : "");
        }
        AtualizarSaldoApos();
    }

    private void AoMudarTipo()
    {
        string tipo = TipoSelecionado;
        bool ajuste = tipo == TipoMov.Ajuste;
        _rotQtd.Text = ajuste ? "Novo saldo" : "Quantidade";
        _celulaCusto.Visible = tipo == TipoMov.Entrada;
        _rotObs.Text = tipo == TipoMov.Entrada ? "Observação" : "Motivo (obrigatório)";
        _lblAjuda.Text = tipo switch
        {
            TipoMov.Entrada => "A quantidade será somada ao saldo. Se informar o custo unitário, o custo médio do produto será atualizado.",
            TipoMov.Saida => "A quantidade será baixada do saldo. Informe o motivo (perda, consumo interno, avaria...).",
            _ => "Ajuste de inventário: o saldo do produto passará a ser exatamente o valor informado em \"Novo saldo\"."
        };
        if (ajuste && _produto != null && _txtQtd.Valor == 0) _txtQtd.Valor = _produto.Estoque;
        AtualizarSaldoApos();
    }

    private decimal? SaldoApos()
    {
        if (_produto == null || !_txtQtd.Valido) return null;
        decimal q = _txtQtd.Valor;
        return TipoSelecionado switch
        {
            TipoMov.Entrada => _produto.Estoque + q,
            TipoMov.Saida => _produto.Estoque - q,
            _ => q
        };
    }

    private void AtualizarSaldoApos()
    {
        var s = SaldoApos();
        string un = _produto?.Unidade ?? "";
        _lblApos.Text = s == null ? "Saldo após a movimentação: —" : $"Saldo após a movimentação: {Formato.Qtd(s.Value)} {un}";
        _lblApos.ForeColor = s < 0 ? Tema.Perigo : Tema.Texto;
    }

    protected override void AoSalvar()
    {
        if (_produto == null) throw new RegraException("Selecione o produto.");
        if (!_txtQtd.Valido) { _txtQtd.Focus(); throw new RegraException("Quantidade inválida."); }
        string tipo = TipoSelecionado;
        decimal qtd = _txtQtd.Valor;
        if (tipo != TipoMov.Ajuste && qtd <= 0) { _txtQtd.Focus(); throw new RegraException("A quantidade deve ser maior que zero."); }
        if (tipo == TipoMov.Ajuste && qtd < 0) { _txtQtd.Focus(); throw new RegraException("O novo saldo não pode ser negativo."); }
        if (tipo == TipoMov.Ajuste && qtd == _produto.Estoque) { _txtQtd.Focus(); throw new RegraException("O novo saldo é igual ao saldo atual."); }

        string obs = _txtObs.Text.Trim();
        if (tipo != TipoMov.Entrada && obs.Length == 0) { _txtObs.Focus(); throw new RegraException("Informe o motivo da movimentação."); }

        decimal? custo = null;
        if (tipo == TipoMov.Entrada)
        {
            if (!_txtCusto.Valido) { _txtCusto.Focus(); throw new RegraException("Custo unitário inválido."); }
            if (_txtCusto.Valor < 0) { _txtCusto.Focus(); throw new RegraException("O custo unitário não pode ser negativo."); }
            if (_txtCusto.Valor > 0) custo = _txtCusto.Valor;
        }

        EstoqueService.MovimentarManual(_produto.Id, tipo, qtd, obs, custo);
    }
}
