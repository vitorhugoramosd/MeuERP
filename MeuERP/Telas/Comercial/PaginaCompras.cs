using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Comercial;

/// <summary>Pedidos de compra, recebimento de mercadorias e cancelamento (acesso de gerente/administrador).</summary>
public class PaginaCompras : PaginaLista<Compra>
{
    private readonly DateTimePicker _dtDe, _dtAte;
    private readonly ComboBox _cbStatus;

    public PaginaCompras()
    {
        Titulo = "Compras";
        Subtitulo = "Pedidos de compra e recebimento de mercadorias";

        BarraAcoes.Width = 720;
        BtnNovo.Text = "+ Nova compra";
        BtnEditar.Text = "Abrir";
        BtnExcluir.Text = "Cancelar compra";
        var btnReceber = AdicionarAcao("Receber mercadoria", EstiloBotao.Secundario, ReceberSelecionada);
        BarraAcoes.Controls.SetChildIndex(btnReceber, 2); // entre "Abrir" e "Cancelar compra"

        TxtBusca.PlaceholderText = "Buscar por fornecedor, NF ou nº...";
        _dtDe = Ui.Data(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
        _dtAte = Ui.Data(DateTime.Today);
        _cbStatus = Ui.Combo(130);
        _cbStatus.CarregarTextos(new[] { "Todos", "Pedido", "Recebida", "Cancelada" });
        Filtros.Controls.Add(Ui.Rotulo("Período"));
        Filtros.Controls.Add(_dtDe);
        Filtros.Controls.Add(Ui.Rotulo("até"));
        Filtros.Controls.Add(_dtAte);
        Filtros.Controls.Add(Ui.Rotulo("Status"));
        Filtros.Controls.Add(_cbStatus);
        RecarregarAoMudar(_dtDe, _dtAte, _cbStatus);

        if (!Sessao.PodeGerenciar)
            BtnNovo.Visible = BtnExcluir.Visible = btnReceber.Visible = false;
    }

    protected override string NomeExportacao => "compras";

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.Coluna(nameof(Compra.Id), "Nº", 70, alinhamento: DataGridViewContentAlignment.MiddleRight);
        grid.ColunaData(nameof(Compra.DataCompra), "Data");
        grid.Coluna(nameof(Compra.FornecedorNome), "Fornecedor");
        grid.Coluna(nameof(Compra.NumeroNf), "NF", 100);
        grid.Coluna(nameof(Compra.FormaPagamento), "Pagamento", 140);
        grid.ColunaMoeda(nameof(Compra.Total), "Total", 120);
        grid.ColunaStatus(nameof(Compra.Status), "Status", 100);
        grid.ColunaData(nameof(Compra.DataRecebimento), "Recebida em");
    }

    protected override IEnumerable<Compra> Carregar(string busca) =>
        CompraService.Listar(_dtDe.Value, _dtAte.Value,
            ComercialUi.StatusDoCombo(_cbStatus, StatusCompra.Pedido, StatusCompra.Recebida, StatusCompra.Cancelada), busca);

    protected override string Rodape(IReadOnlyList<Compra> itens)
    {
        var recebidas = itens.Where(c => c.Status == StatusCompra.Recebida).ToList();
        var pedidos = itens.Where(c => c.Status == StatusCompra.Pedido).ToList();
        string qtd = itens.Count == 1 ? "1 registro" : $"{itens.Count} registros";
        return $"{qtd}   •   Recebidas: {recebidas.Count} ({Formato.Moeda(recebidas.Sum(c => c.Total))})" +
               $"   •   Pedidos em aberto: {pedidos.Count} ({Formato.Moeda(pedidos.Sum(c => c.Total))})";
    }

    protected override bool Novo()
    {
        using var d = new DialogoCompra();
        bool ok = d.Abrir();
        return ok || d.Gravou;
    }

    protected override bool Editar(Compra item)
    {
        var compra = CompraService.Obter(item.Id) ?? throw new RegraException("Compra não encontrada.");
        using var d = new DialogoCompra(compra);
        bool ok = d.Abrir();
        return ok || d.Gravou;
    }

    protected override string ConfirmacaoExclusao(Compra item) => item.Status switch
    {
        StatusCompra.Pedido => $"Deseja cancelar o pedido de compra nº {item.Id}?",
        StatusCompra.Cancelada => $"A compra nº {item.Id} já está cancelada. Deseja continuar?",
        _ => $"Deseja cancelar a compra nº {item.Id}? Os itens serão retirados do estoque e os títulos a pagar cancelados."
    };

    protected override void Excluir(Compra item)
    {
        CompraService.Cancelar(item.Id);
        Mensagem.Info($"Compra nº {item.Id} cancelada.");
    }

    private void ReceberSelecionada()
    {
        var c = Selecionado;
        if (c == null) { Mensagem.Info("Selecione um pedido de compra."); return; }
        if (c.Status != StatusCompra.Pedido)
        {
            Mensagem.Info(c.Status == StatusCompra.Recebida ? $"A compra nº {c.Id} já foi recebida." : "Somente pedidos podem ser recebidos.");
            return;
        }
        if (!ComercialUi.Perguntar($"Confirmar o recebimento da compra nº {c.Id}?\n\nFornecedor: {c.FornecedorNome}\n" +
                                    $"Total: {Formato.Moeda(c.Total)}\n\nOs itens entrarão no estoque e os títulos a pagar serão gerados."))
            return;

        DateTime? venc = null;
        if (!FormasPagamento.EhAVista(c.FormaPagamento))
        {
            using var d = new DialogoVencimento(DateTime.Today.AddDays(30), ComercialUi.TextoPagamento(c.FormaPagamento, c.Parcelas));
            if (!d.Abrir()) return;
            venc = d.Data;
        }

        if (Mensagem.Tentar(() => CompraService.Receber(c.Id, venc)))
        {
            Mensagem.Info($"Compra nº {c.Id} recebida com sucesso.");
            Recarregar();
        }
    }
}
