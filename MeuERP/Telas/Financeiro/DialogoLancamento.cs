using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Financeiro;

/// <summary>Lançamento avulso de caixa (suprimento, sangria, despesa miúda...).</summary>
public class DialogoLancamento : DialogoBase
{
    private readonly ComboBox _cbTipo, _cbForma;
    private readonly TextBox _txtDescricao;
    private readonly CampoDecimal _txtValor;
    private readonly DateTimePicker _dtData;

    public DialogoLancamento() : base("Novo lançamento de caixa", 560, 260)
    {
        var form = new PainelFormulario(4);
        _cbTipo = form.Combo("Tipo", 1);
        _cbTipo.CarregarTextos(new[] { "Entrada", "Saída" });
        _txtDescricao = form.Texto("Descrição", 3, 200);
        _txtValor = form.Decimal("Valor", 1);
        _cbForma = form.Combo("Forma de pagamento", 2);
        _cbForma.CarregarTextos(FormasPagamento.Todas, FormasPagamento.Dinheiro);
        _dtData = form.Data("Data", 1);
        Corpo.Controls.Add(form);
        Shown += (_, _) => _txtDescricao.Focus();
    }

    protected override void AoSalvar()
    {
        if (_txtDescricao.Text.Trim().Length == 0) { _txtDescricao.Focus(); throw new RegraException("Informe a descrição do lançamento."); }
        if (!_txtValor.Valido) { _txtValor.Focus(); throw new RegraException("Valor inválido."); }
        decimal valor = Formato.Arredondar(_txtValor.Valor);
        if (valor <= 0) { _txtValor.Focus(); throw new RegraException("O valor deve ser maior que zero."); }
        string tipo = _cbTipo.SelectedIndex == 1 ? TipoCaixa.Saida : TipoCaixa.Entrada;
        var agora = DateTime.Now;
        var data = _dtData.Value.Date.AddHours(agora.Hour).AddMinutes(agora.Minute).AddSeconds(agora.Second);
        FinanceiroService.LancarCaixaManual(tipo, _txtDescricao.Text, valor, _cbForma.SelectedItem as string ?? FormasPagamento.Dinheiro, data);
    }
}
