using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Comercial;

/// <summary>Utilitários compartilhados pelas telas de vendas e compras.</summary>
internal static class ComercialUi
{
    public static readonly Font FonteTotal = new("Segoe UI Semibold", 22f);

    /// <summary>Busca exata por código/código de barras; se não achar, abre a pesquisa com o texto digitado.</summary>
    public static Produto? ResolverProduto(string texto)
    {
        texto = texto.Trim();
        if (texto.Length == 0) return null;
        Produto? p = null;
        if (!Mensagem.Tentar(() => p = ProdutoRepository.ObterPorCodigo(texto))) return null;
        return p ?? Seletor.Produto(texto);
    }

    /// <summary>Posiciona um rótulo pequeno e, logo abaixo, o controle (layout absoluto).</summary>
    public static T Campo<T>(Control pai, string rotulo, T controle, int x, int largura, int y = 0) where T : Control
    {
        pai.Controls.Add(new Label
        {
            Text = rotulo, Location = new Point(x, y), Size = new Size(largura, 18),
            Font = Tema.FontePequena, ForeColor = Tema.TextoSuave
        });
        controle.Font = Tema.Fonte;
        controle.Location = new Point(x, y + 19);
        controle.Width = largura;
        pai.Controls.Add(controle);
        return controle;
    }

    /// <summary>Pergunta Sim/Não com "Sim" como botão padrão (Enter confirma).</summary>
    public static bool Perguntar(string texto) =>
        MessageBox.Show(texto, "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes;

    public static Label RotuloValor(string texto, Point local, Size tamanho, Font fonte, Color? cor = null) => new()
    {
        Text = texto, Location = local, Size = tamanho, Font = fonte, ForeColor = cor ?? Tema.Texto,
        TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, BackColor = Color.Transparent
    };

    public static Label RotuloTexto(string texto, Point local, Font? fonte = null, Color? cor = null) => new()
    {
        Text = texto, Location = local, AutoSize = true, Font = fonte ?? Tema.Fonte, ForeColor = cor ?? Tema.TextoSuave,
        BackColor = Color.Transparent
    };

    /// <summary>Converte o índice do combo de status (0 = Todos) para o código gravado no banco.</summary>
    public static string? StatusDoCombo(ComboBox combo, params string[] codigos) =>
        combo.SelectedIndex > 0 && combo.SelectedIndex <= codigos.Length ? codigos[combo.SelectedIndex - 1] : null;

    public static string TextoPagamento(string forma, int parcelas, DateTime? primeiroVencimento = null)
    {
        if (FormasPagamento.EhAVista(forma)) return forma + " (à vista)";
        string s = parcelas > 1 ? $"{forma} em {parcelas}x" : forma;
        return primeiroVencimento.HasValue ? $"{s} - 1º vencimento em {Formato.Data(primeiroVencimento.Value)}" : s;
    }
}

/// <summary>Pergunta a data do primeiro vencimento.</summary>
internal class DialogoVencimento : DialogoBase
{
    private readonly DateTimePicker _data;

    public DateTime Data => _data.Value.Date;

    public DialogoVencimento(DateTime padrao, string detalhe) : base("Primeiro vencimento", 380, 260)
    {
        var form = new PainelFormulario(1);
        _data = form.Data("Data do primeiro vencimento", 1, padrao);
        var info = form.Rotulo("Condição de pagamento", 1, detalhe);
        info.ForeColor = Tema.TextoSuave;
        Corpo.Controls.Add(form);
        BtnSalvar.Text = "Confirmar";
        Shown += (_, _) => _data.Focus();
    }
}

/// <summary>Valor recebido em dinheiro e cálculo do troco.</summary>
internal class DialogoValorRecebido : DialogoBase
{
    private readonly decimal _total;
    private readonly CampoDecimal _recebido;
    private readonly Label _troco;

    public decimal Recebido { get; private set; }
    public decimal Troco { get; private set; }

    public DialogoValorRecebido(decimal total) : base("Pagamento em dinheiro", 380, 320)
    {
        _total = total;
        var form = new PainelFormulario(1);
        var lblTotal = form.Rotulo("Total da venda", 1, Formato.Moeda(total));
        lblTotal.Font = Tema.FonteSubtitulo;
        _recebido = form.Decimal("Valor recebido (opcional)", 1);
        _recebido.Valor = total;
        _troco = form.Rotulo("Troco", 1, Formato.Moeda(0));
        _troco.Font = new Font("Segoe UI Semibold", 16f);
        _troco.ForeColor = Tema.Sucesso;
        Corpo.Controls.Add(form);

        BtnSalvar.Text = "Confirmar";
        _recebido.TextChanged += (_, _) => AtualizarTroco();
        Shown += (_, _) => { _recebido.Focus(); _recebido.SelectAll(); };
    }

    private void AtualizarTroco()
    {
        if (!_recebido.Valido) { _troco.Text = "-"; return; }
        decimal r = _recebido.Valor;
        if (r > 0 && r < _total)
        {
            _troco.Text = "Falta " + Formato.Moeda(_total - r);
            _troco.ForeColor = Tema.Perigo;
        }
        else
        {
            _troco.Text = Formato.Moeda(r > 0 ? r - _total : 0);
            _troco.ForeColor = Tema.Sucesso;
        }
    }

    protected override void AoSalvar()
    {
        if (!_recebido.Valido) throw new RegraException("Valor recebido inválido.");
        decimal r = Formato.Arredondar(_recebido.Valor);
        if (r < 0) throw new RegraException("O valor recebido não pode ser negativo.");
        if (r > 0 && r < _total) throw new RegraException("O valor recebido é menor que o total da venda.");
        Recebido = r;
        Troco = r > 0 ? r - _total : 0;
    }
}
