using MeuERP.UI;

namespace MeuERP.Telas.Financeiro;

/// <summary>
/// Linha de indicadores (CartaoKpi) com Dock = Top. Os cartões dividem a largura disponível
/// igualmente, para que todas as páginas tenham a mesma altura e espaçamento de KPIs.
/// </summary>
public class PainelKpis : FlowLayoutPanel
{
    private const int Espaco = 12;
    private const int LarguraMinima = 150;
    private readonly List<CartaoKpi> _cartoes = new();

    public PainelKpis()
    {
        Dock = DockStyle.Top;
        Height = 116; // 104 do cartão + 12 de espaço até o bloco seguinte
        WrapContents = false;
        FlowDirection = FlowDirection.LeftToRight;
        BackColor = Tema.Fundo;
        Padding = Padding.Empty;
        Margin = Padding.Empty;
    }

    public CartaoKpi Adicionar(string rotulo, Color cor)
    {
        var k = new CartaoKpi(rotulo, cor);
        _cartoes.Add(k);
        Controls.Add(k);
        Redistribuir();
        return k;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        Redistribuir();
    }

    private void Redistribuir()
    {
        int n = _cartoes.Count;
        if (n == 0 || ClientSize.Width <= 0) return;
        int largura = Math.Max(LarguraMinima, (ClientSize.Width - Espaco * (n - 1)) / n);
        SuspendLayout();
        for (int i = 0; i < n; i++)
        {
            _cartoes[i].Width = largura;
            _cartoes[i].Margin = new Padding(0, 0, i == n - 1 ? 0 : Espaco, Espaco);
        }
        ResumeLayout();
    }
}
