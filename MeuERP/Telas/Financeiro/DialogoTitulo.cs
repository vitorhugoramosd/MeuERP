using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Financeiro;

/// <summary>Inclusão/edição de título avulso (sem venda/compra de origem).</summary>
public class DialogoTitulo : DialogoBase
{
    private readonly TipoTitulo _tipo;
    private readonly Titulo _titulo;
    private readonly bool _novo;
    private int? _pessoaId;

    private readonly TextBox _txtDescricao, _txtObs;
    private readonly Label _lblPessoa;
    private readonly CampoDecimal _txtValor;
    private readonly DateTimePicker _dtVenc;
    private readonly ComboBox _cbForma;
    private readonly NumericUpDown? _numRepetir;
    private readonly Label? _lblRepeticao;

    private bool Receber => _tipo == TipoTitulo.Receber;

    public DialogoTitulo(TipoTitulo tipo, Titulo? titulo)
        : base(TituloDialogo(tipo, titulo), 640, titulo == null ? 450 : 400)
    {
        _tipo = tipo;
        _novo = titulo == null;
        _titulo = titulo ?? new Titulo();
        _pessoaId = _titulo.PessoaId;

        var form = new PainelFormulario(4);
        _txtDescricao = form.Texto("Descrição", 4, 200);

        _lblPessoa = form.Rotulo(Receber ? "Cliente (opcional)" : "Fornecedor (opcional)", 2);
        _lblPessoa.BorderStyle = BorderStyle.FixedSingle;
        _lblPessoa.Padding = new Padding(4, 0, 0, 0);
        form.Adicionar("", new BotaoModerno("Selecionar", EstiloBotao.Secundario, SelecionarPessoa));
        form.Adicionar("", new BotaoModerno("Limpar", EstiloBotao.Secundario, () => { _pessoaId = null; MostrarPessoa(""); }));

        _txtValor = form.Decimal("Valor", 1);
        _dtVenc = form.Data(_novo ? "Vencimento (1ª parcela)" : "Vencimento", 1, _titulo.DataVencimento);
        _cbForma = form.Combo("Forma de pagamento prevista", 2);
        _cbForma.CarregarTextos(new[] { "" }.Concat(FormasPagamento.Todas), _titulo.FormaPagamento);

        if (_novo)
        {
            _numRepetir = form.Inteiro("Repetir por (meses)", 1, 1, 60, 1);
            _lblRepeticao = form.Adicionar("", new Label
            {
                ForeColor = Tema.TextoSuave, Font = Tema.FontePequena, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
            }, 3);
            _numRepetir.ValueChanged += (_, _) => AtualizarRepeticao();
            _dtVenc.ValueChanged += (_, _) => AtualizarRepeticao();
            _txtValor.TextChanged += (_, _) => AtualizarRepeticao();
        }

        _txtObs = form.Multilinha("Observações", 4, 90);
        Corpo.Controls.Add(form);

        _txtDescricao.Text = _titulo.Descricao;
        _txtValor.Valor = _titulo.Valor;
        _txtObs.Text = _titulo.Observacoes;
        MostrarPessoa(_titulo.PessoaNome);
        AtualizarRepeticao();
    }

    private static string TituloDialogo(TipoTitulo tipo, Titulo? t) =>
        (t == null ? "Novo título" : "Editar título") + (tipo == TipoTitulo.Receber ? " a receber" : " a pagar");

    private void MostrarPessoa(string nome)
    {
        _lblPessoa.Text = string.IsNullOrEmpty(nome) ? "(nenhum)" : nome;
        _lblPessoa.ForeColor = string.IsNullOrEmpty(nome) ? Tema.TextoSuave : Tema.Texto;
    }

    private void SelecionarPessoa()
    {
        if (Receber)
        {
            var c = Seletor.Cliente();
            if (c != null) { _pessoaId = c.Id; MostrarPessoa(c.Nome); }
        }
        else
        {
            var f = Seletor.Fornecedor();
            if (f != null) { _pessoaId = f.Id; MostrarPessoa(f.Nome); }
        }
    }

    private void AtualizarRepeticao()
    {
        if (_numRepetir == null || _lblRepeticao == null) return;
        int n = (int)_numRepetir.Value;
        _lblRepeticao.Text = n <= 1
            ? "Título único."
            : $"Serão criados {n} títulos mensais de {Formato.Moeda(_txtValor.Valor)}, " +
              $"de {Formato.Data(_dtVenc.Value)} a {Formato.Data(_dtVenc.Value.Date.AddMonths(n - 1))}.";
    }

    protected override void AoSalvar()
    {
        string desc = _txtDescricao.Text.Trim();
        if (desc.Length == 0) { _txtDescricao.Focus(); throw new RegraException("Informe a descrição."); }
        if (!_txtValor.Valido) { _txtValor.Focus(); throw new RegraException("Valor inválido."); }
        decimal valor = Formato.Arredondar(_txtValor.Valor);
        if (valor <= 0) { _txtValor.Focus(); throw new RegraException("O valor deve ser maior que zero."); }
        string forma = _cbForma.SelectedItem as string ?? "";

        if (!_novo)
        {
            _titulo.Descricao = desc;
            _titulo.PessoaId = _pessoaId;
            _titulo.Valor = valor;
            _titulo.DataVencimento = _dtVenc.Value.Date;
            _titulo.FormaPagamento = forma;
            _titulo.Observacoes = _txtObs.Text.Trim();
            FinanceiroService.SalvarManual(_tipo, _titulo);
            return;
        }

        int n = (int)(_numRepetir?.Value ?? 1);
        for (int i = 1; i <= n; i++)
        {
            var t = new Titulo
            {
                Descricao = n > 1 ? $"{desc} ({i}/{n})" : desc,
                PessoaId = _pessoaId,
                Valor = valor,
                DataEmissao = DateTime.Today,
                DataVencimento = _dtVenc.Value.Date.AddMonths(i - 1),
                FormaPagamento = forma,
                Observacoes = _txtObs.Text.Trim()
            };
            FinanceiroService.SalvarManual(_tipo, t);
        }
    }
}
