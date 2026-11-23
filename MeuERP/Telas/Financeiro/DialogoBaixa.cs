using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Financeiro;

/// <summary>Baixa (total ou parcial) de um título: recebimento ou pagamento.</summary>
public class DialogoBaixa : DialogoBase
{
    private readonly TipoTitulo _tipo;
    private readonly Titulo _titulo;
    private readonly CampoDecimal _txtValor;
    private readonly DateTimePicker _dtData;
    private readonly ComboBox _cbForma;
    private readonly Label _lblRestante;

    public DialogoBaixa(TipoTitulo tipo, Titulo titulo)
        : base(tipo == TipoTitulo.Receber ? "Receber título" : "Pagar título", 600, 420)
    {
        _tipo = tipo;
        _titulo = titulo;
        bool receber = tipo == TipoTitulo.Receber;

        // ---- Dados do título (somente leitura)
        var info = new PainelFormulario(4);
        Destaque(info.Rotulo("Descrição", 4, titulo.Descricao + (titulo.TotalParcelas > 1 ? $"  (parcela {titulo.ParcelaTexto})" : "")));
        Destaque(info.Rotulo(receber ? "Cliente" : "Fornecedor", 3, string.IsNullOrEmpty(titulo.PessoaNome) ? "(não informado)" : titulo.PessoaNome));
        var lblVenc = info.Rotulo("Vencimento", 1, Formato.Data(titulo.DataVencimento));
        if (titulo.Vencido) lblVenc.ForeColor = Tema.Perigo;
        info.Rotulo("Valor original", 1, Formato.Moeda(titulo.Valor));
        info.Rotulo(receber ? "Já recebido" : "Já pago", 1, Formato.Moeda(titulo.ValorPago));
        var lblSaldo = info.Rotulo("Saldo", 2, Formato.Moeda(titulo.Saldo));
        lblSaldo.Font = Tema.FonteSubtitulo;
        lblSaldo.ForeColor = titulo.Vencido ? Tema.Perigo : Tema.Texto;

        var separador = new Panel { Dock = DockStyle.Top, Height = 13, BackColor = Color.White };
        separador.Paint += (_, e) => { using var p = new Pen(Tema.Borda); e.Graphics.DrawLine(p, 0, 4, separador.Width - 12, 4); };

        // ---- Campos da baixa
        var form = new PainelFormulario(4);
        _txtValor = form.Decimal(receber ? "Valor recebido" : "Valor pago", 1);
        _dtData = form.Data(receber ? "Data do recebimento" : "Data do pagamento", 1);
        _cbForma = form.Combo("Forma de pagamento", 2);
        string forma = string.IsNullOrEmpty(titulo.FormaPagamento) ? FormasPagamento.Dinheiro : titulo.FormaPagamento;
        _cbForma.CarregarTextos(FormasPagamento.Todas, forma);
        _lblRestante = form.Adicionar("", new Label { ForeColor = Tema.TextoSuave, Font = Tema.FontePequena, TextAlign = ContentAlignment.MiddleLeft }, 4, 32);

        // Dock Top: o último adicionado fica no topo.
        Corpo.Controls.Add(form);
        Corpo.Controls.Add(separador);
        Corpo.Controls.Add(info);

        _txtValor.Valor = titulo.Saldo;
        _txtValor.TextChanged += (_, _) => AtualizarRestante();
        AtualizarRestante();

        BtnSalvar.Text = receber ? "Confirmar recebimento" : "Confirmar pagamento";
        BtnSalvar.Estilo = EstiloBotao.Sucesso;
        Shown += (_, _) => _txtValor.Focus();
    }

    private static void Destaque(Label l) => l.Font = Tema.FonteNegrito;

    private void AtualizarRestante()
    {
        if (!_txtValor.Valido) { _lblRestante.Text = "Valor inválido."; _lblRestante.ForeColor = Tema.Perigo; return; }
        decimal restante = Formato.Arredondar(_titulo.Saldo - _txtValor.Valor);
        if (restante < 0)
        {
            _lblRestante.Text = $"O valor é maior que o saldo do título ({Formato.Moeda(_titulo.Saldo)}).";
            _lblRestante.ForeColor = Tema.Perigo;
        }
        else if (restante == 0)
        {
            _lblRestante.Text = "O título será quitado.";
            _lblRestante.ForeColor = Tema.Sucesso;
        }
        else
        {
            _lblRestante.Text = $"Baixa parcial: restará {Formato.Moeda(restante)} em aberto.";
            _lblRestante.ForeColor = Tema.Aviso;
        }
    }

    protected override void AoSalvar()
    {
        if (!_txtValor.Valido) { _txtValor.Focus(); throw new RegraException("Valor inválido."); }
        string forma = _cbForma.SelectedItem as string ?? FormasPagamento.Dinheiro;
        DateTime data = _dtData.Value.Date + DateTime.Now.TimeOfDay;
        data = new DateTime(data.Year, data.Month, data.Day, data.Hour, data.Minute, data.Second);
        FinanceiroService.Baixar(_tipo, _titulo.Id, _txtValor.Valor, data, forma);
    }
}
