using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Comercial;

/// <summary>Lista de vendas e orçamentos, com PDV (nova venda), cancelamento e impressão do comprovante.</summary>
public class PaginaVendas : PaginaLista<Venda>
{
    private readonly DateTimePicker _dtDe, _dtAte;
    private readonly ComboBox _cbStatus;

    public PaginaVendas()
    {
        Titulo = "Vendas";
        Subtitulo = "Vendas, orçamentos e frente de caixa";

        BtnNovo.Text = "+ Nova venda";
        BtnEditar.Text = "Abrir";
        BtnExcluir.Text = "Cancelar venda";
        var btnImprimir = AdicionarAcao("Imprimir", EstiloBotao.Secundario, Imprimir);
        BarraAcoes.Controls.SetChildIndex(btnImprimir, 1); // fica entre "Cancelar venda" e "Exportar"

        TxtBusca.PlaceholderText = "Buscar por cliente ou nº da venda...";
        _dtDe = Ui.Data(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
        _dtAte = Ui.Data(DateTime.Today);
        _cbStatus = Ui.Combo(130);
        _cbStatus.CarregarTextos(new[] { "Todos", "Orçamento", "Finalizada", "Cancelada" });
        Filtros.Controls.Add(Ui.Rotulo("Período"));
        Filtros.Controls.Add(_dtDe);
        Filtros.Controls.Add(Ui.Rotulo("até"));
        Filtros.Controls.Add(_dtAte);
        Filtros.Controls.Add(Ui.Rotulo("Status"));
        Filtros.Controls.Add(_cbStatus);
        RecarregarAoMudar(_dtDe, _dtAte, _cbStatus);
    }

    protected override string NomeExportacao => "vendas";

    protected override void ConfigurarGrid(GridModerno grid)
    {
        grid.Coluna(nameof(Venda.Id), "Nº", 70, alinhamento: DataGridViewContentAlignment.MiddleRight);
        grid.ColunaData(nameof(Venda.DataVenda), "Data", comHora: true);
        grid.Coluna(nameof(Venda.ClienteNome), "Cliente");
        grid.Coluna(nameof(Venda.UsuarioNome), "Vendedor", 140);
        grid.Coluna(nameof(Venda.FormaPagamento), "Pagamento", 140);
        grid.ColunaNumero(nameof(Venda.Parcelas), "Parcelas", 75, "0");
        grid.ColunaMoeda(nameof(Venda.Total), "Total", 120);
        grid.ColunaStatus(nameof(Venda.Status), "Status", 105);
    }

    protected override IEnumerable<Venda> Carregar(string busca) =>
        VendaService.Listar(_dtDe.Value, _dtAte.Value,
            ComercialUi.StatusDoCombo(_cbStatus, StatusVenda.Orcamento, StatusVenda.Finalizada, StatusVenda.Cancelada), busca);

    protected override string Rodape(IReadOnlyList<Venda> itens)
    {
        var finalizadas = itens.Where(v => v.Status == StatusVenda.Finalizada).ToList();
        string qtd = itens.Count == 1 ? "1 registro" : $"{itens.Count} registros";
        return $"{qtd}   •   {finalizadas.Count} venda{(finalizadas.Count == 1 ? "" : "s")} finalizada{(finalizadas.Count == 1 ? "" : "s")}: " +
               Formato.Moeda(finalizadas.Sum(v => v.Total));
    }

    protected override bool Novo()
    {
        using var d = new DialogoVenda();
        bool ok = d.Abrir();
        return ok || d.Gravou;
    }

    protected override bool Editar(Venda item)
    {
        var venda = VendaService.Obter(item.Id) ?? throw new RegraException("Venda não encontrada.");
        using var d = new DialogoVenda(venda);
        bool ok = d.Abrir();
        return ok || d.Gravou;
    }

    protected override string ConfirmacaoExclusao(Venda item) => item.Status switch
    {
        StatusVenda.Orcamento => $"Deseja cancelar o orçamento nº {item.Id}?",
        StatusVenda.Cancelada => $"A venda nº {item.Id} já está cancelada. Deseja continuar?",
        _ => $"Deseja cancelar a venda nº {item.Id}? O estoque será devolvido e os títulos cancelados."
    };

    protected override void Excluir(Venda item)
    {
        VendaService.Cancelar(item.Id);
        Mensagem.Info(item.Status == StatusVenda.Orcamento ? $"Orçamento nº {item.Id} cancelado." : $"Venda nº {item.Id} cancelada.");
    }

    private void Imprimir()
    {
        var v = Selecionado;
        if (v == null) { Mensagem.Info("Selecione uma venda."); return; }
        Mensagem.Tentar(() => ComprovanteVenda.Visualizar(v.Id));
    }
}
