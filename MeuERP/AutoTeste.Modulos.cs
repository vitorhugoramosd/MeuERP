using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;

namespace MeuERP;

internal static partial class AutoTeste
{
    static partial void TestarModulosImpl()
    {
        TestarVendas();
        TestarCompras();
        TestarDemonstracao();
    }

    private static decimal Saldo(int produtoId) => ProdutoRepository.Obter(produtoId)!.Estoque;

    private static int Contar(string tabela)
    {
        using var c = Database.Abrir();
        return c.ExecuteScalar<int>($"SELECT COUNT(*) FROM {tabela}");
    }

    private static void TestarVendas()
    {
        var p = new Produto { Codigo = "VND-1", Descricao = "Mouse sem fio", Unidade = "UN", PrecoCusto = 50m, PrecoVenda = 100m };
        ProdutoRepository.Salvar(p);
        EstoqueService.MovimentarManual(p.Id, TipoMov.Entrada, 10, "Estoque inicial");
        decimal caixa0 = FinanceiroService.SaldoCaixa();

        // À vista, sem cliente, com desconto geral
        int v1 = VendaService.Finalizar(new Venda
        {
            FormaPagamento = FormasPagamento.Dinheiro, Desconto = 10m,
            Itens = { new VendaItem { ProdutoId = p.Id, Quantidade = 2, PrecoUnitario = 100m } }
        });
        var venda1 = VendaService.Obter(v1)!;
        Verificar("venda à vista finalizada", venda1.Status == StatusVenda.Finalizada && venda1.Itens.Count == 1);
        Igual("total = subtotal - desconto", 190m, venda1.Total);
        Igual("custo do item registrado", 50m, venda1.Itens[0].CustoUnitario);
        Igual("estoque baixado pela venda", 8, Saldo(p.Id));
        Igual("venda à vista entra no caixa", caixa0 + 190m, FinanceiroService.SaldoCaixa());
        var tit1 = FinanceiroService.Listar(TipoTitulo.Receber).Where(t => t.OrigemId == v1).ToList();
        Verificar("título da venda à vista nasce pago", tit1.Count == 1 && tit1[0].Status == StatusTitulo.Pago);
        Verificar("venda aparece na listagem", VendaService.Listar(DateTime.Today, DateTime.Today).Any(v => v.Id == v1));

        // A prazo
        Verificar("crediário sem cliente é recusado", Lanca(() => VendaService.Finalizar(new Venda
        {
            FormaPagamento = FormasPagamento.Crediario, Parcelas = 3,
            Itens = { new VendaItem { ProdutoId = p.Id, Quantidade = 1, PrecoUnitario = 100m } }
        })));
        Igual("venda recusada não mexe no estoque", 8, Saldo(p.Id));

        int v2 = VendaService.Finalizar(new Venda
        {
            ClienteId = _clienteId, FormaPagamento = FormasPagamento.Crediario, Parcelas = 3,
            Itens = { new VendaItem { ProdutoId = p.Id, Quantidade = 3, PrecoUnitario = 100m } }
        }, DateTime.Today.AddDays(15));
        var tit2 = FinanceiroService.Listar(TipoTitulo.Receber).Where(t => t.OrigemId == v2).OrderBy(t => t.Parcela).ToList();
        Verificar("venda a prazo gera 3 parcelas em aberto", tit2.Count == 3 && tit2.All(t => t.Status == StatusTitulo.Aberto));
        Igual("parcelas somam o total", 300m, tit2.Sum(t => t.Valor));
        Verificar("primeiro vencimento respeitado", tit2[0].DataVencimento == DateTime.Today.AddDays(15));
        Igual("venda a prazo não entra no caixa", caixa0 + 190m, FinanceiroService.SaldoCaixa());
        Igual("estoque após segunda venda", 5, Saldo(p.Id));

        int vendasAntes = Contar("vendas");
        Verificar("limite de crédito é respeitado", Lanca(() => VendaService.Finalizar(new Venda
        {
            ClienteId = _clienteId, FormaPagamento = FormasPagamento.Crediario, Parcelas = 1,
            Itens = { new VendaItem { ProdutoId = p.Id, Quantidade = 3, PrecoUnitario = 300m } }
        })));
        Verificar("estoque insuficiente bloqueia a venda", Lanca(() => VendaService.Finalizar(new Venda
        {
            FormaPagamento = FormasPagamento.Pix,
            Itens = { new VendaItem { ProdutoId = p.Id, Quantidade = 100, PrecoUnitario = 100m } }
        })));
        Verificar("venda sem itens é recusada", Lanca(() => VendaService.Finalizar(new Venda { FormaPagamento = FormasPagamento.Pix })));
        Verificar("vendas recusadas não ficam gravadas", Contar("vendas") == vendasAntes);
        Igual("estoque intacto após recusas", 5, Saldo(p.Id));

        // Orçamento -> finalização
        int orc = VendaService.SalvarOrcamento(new Venda
        {
            FormaPagamento = FormasPagamento.Pix,
            Itens = { new VendaItem { ProdutoId = p.Id, Quantidade = 1, PrecoUnitario = 100m, Desconto = 5m } }
        });
        var orcamento = VendaService.Obter(orc)!;
        Verificar("orçamento não baixa estoque", orcamento.Status == StatusVenda.Orcamento && Saldo(p.Id) == 5);
        Igual("total do orçamento com desconto no item", 95m, orcamento.Total);
        int fin = VendaService.Finalizar(orcamento);
        Verificar("orçamento finalizado mantém o número", fin == orc && VendaService.Obter(orc)!.Status == StatusVenda.Finalizada);
        Igual("estoque após finalizar orçamento", 4, Saldo(p.Id));

        // Cancelamentos
        VendaService.Cancelar(v1);
        Verificar("venda cancelada", VendaService.Obter(v1)!.Status == StatusVenda.Cancelada);
        Igual("cancelamento devolve o estoque", 6, Saldo(p.Id));
        Igual("cancelamento estorna o caixa", caixa0 + 95m, FinanceiroService.SaldoCaixa());
        Verificar("título da venda cancelada fica cancelado",
            FinanceiroService.Listar(TipoTitulo.Receber).Where(t => t.OrigemId == v1).All(t => t.Status == StatusTitulo.Cancelado));
        Verificar("cancelar duas vezes é recusado", Lanca(() => VendaService.Cancelar(v1)));
        Igual("custo não muda no cancelamento", 50m, ProdutoRepository.Obter(p.Id)!.PrecoCusto);
    }

    private static void TestarCompras()
    {
        var p = new Produto { Codigo = "CMP-1", Descricao = "Monitor 24", Unidade = "UN", PrecoCusto = 500m, PrecoVenda = 900m };
        ProdutoRepository.Salvar(p);
        EstoqueService.MovimentarManual(p.Id, TipoMov.Entrada, 10, "Estoque inicial");
        decimal caixa0 = FinanceiroService.SaldoCaixa();

        Verificar("compra sem fornecedor é recusada", Lanca(() => CompraService.SalvarPedido(new Compra
        {
            Itens = { new CompraItem { ProdutoId = p.Id, Quantidade = 1, CustoUnitario = 1m } }
        })));

        int id = CompraService.SalvarPedido(new Compra
        {
            FornecedorId = _fornecedorId, NumeroNf = "12345", FormaPagamento = FormasPagamento.Boleto, Parcelas = 2,
            Frete = 50m, Desconto = 20m,
            Itens = { new CompraItem { ProdutoId = p.Id, Quantidade = 10, CustoUnitario = 700m } }
        });
        var compra = CompraService.Obter(id)!;
        Verificar("pedido gravado sem mexer no estoque", compra.Status == StatusCompra.Pedido && Saldo(p.Id) == 10);
        Igual("total = subtotal - desconto + frete", 7030m, compra.Total);

        CompraService.Receber(id);
        compra = CompraService.Obter(id)!;
        Verificar("compra recebida", compra.Status == StatusCompra.Recebida && compra.DataRecebimento != null);
        Igual("recebimento dá entrada no estoque", 20, Saldo(p.Id));
        Igual("custo médio atualizado", 600m, ProdutoRepository.Obter(p.Id)!.PrecoCusto);
        var tit = FinanceiroService.Listar(TipoTitulo.Pagar).Where(t => t.OrigemId == id).ToList();
        Verificar("compra a prazo gera 2 títulos a pagar", tit.Count == 2 && tit.All(t => t.Status == StatusTitulo.Aberto));
        Igual("títulos somam o total da compra", 7030m, tit.Sum(t => t.Valor));
        Igual("compra a prazo não sai do caixa", caixa0, FinanceiroService.SaldoCaixa());
        Verificar("receber duas vezes é recusado", Lanca(() => CompraService.Receber(id)));
        Verificar("compra aparece na listagem", CompraService.Listar(DateTime.Today, DateTime.Today).Any(c => c.Id == id));

        CompraService.Cancelar(id);
        Verificar("compra cancelada", CompraService.Obter(id)!.Status == StatusCompra.Cancelada);
        Igual("cancelamento retira do estoque", 10, Saldo(p.Id));
        Verificar("títulos da compra cancelados",
            FinanceiroService.Listar(TipoTitulo.Pagar).Where(t => t.OrigemId == id).All(t => t.Status == StatusTitulo.Cancelado));
    }

    private static void TestarDemonstracao()
    {
        string original = Database.Caminho;
        var usuario = Sessao.Usuario;
        try
        {
            Database.Inicializar(Path.Combine(Path.GetDirectoryName(original)!, "demo.db"));
            Sessao.Usuario = UsuarioRepository.Autenticar("admin", "admin");
            string resumo = DadosDemo.Gerar();
            Log.Add("  demo: " + resumo.Replace("\r", "").Replace("\n", " | "));

            using var c = Database.Abrir();
            Verificar("demo cria produtos, clientes e vendas",
                Contar("produtos") >= 20 && Contar("clientes") >= 10 && Contar("vendas") >= 100 && Contar("compras") >= 3);
            Verificar("demo: nenhum estoque negativo", c.ExecuteScalar<int>("SELECT COUNT(*) FROM produtos WHERE estoque < 0") == 0);
            Verificar("demo: estoque = último saldo do histórico", c.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM produtos p WHERE ABS(p.estoque - COALESCE(
                    (SELECT m.saldo_posterior FROM movimentacoes_estoque m WHERE m.produto_id = p.id ORDER BY m.id DESC LIMIT 1), 0)) > 0.001") == 0);
            Verificar("demo: total da venda = soma dos itens - desconto", c.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM vendas v WHERE ABS(v.total - ((SELECT COALESCE(SUM(i.total), 0) FROM venda_itens i WHERE i.venda_id = v.id) - v.desconto)) > 0.011") == 0);
            Verificar("demo: títulos fecham com o total da venda", c.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM vendas v WHERE v.status = 'FINALIZADA' AND v.total > 0 AND ABS(v.total -
                    (SELECT COALESCE(SUM(r.valor), 0) FROM contas_receber r WHERE r.venda_id = v.id AND r.status <> 'CANCELADO')) > 0.011") == 0);
            Verificar("demo: nenhum título pago além do valor", c.ExecuteScalar<int>(
                "SELECT (SELECT COUNT(*) FROM contas_receber WHERE valor_pago > valor + 0.001) + (SELECT COUNT(*) FROM contas_pagar WHERE valor_pago > valor + 0.001)") == 0);
            Verificar("demo: há produtos com estoque baixo", ProdutoRepository.Listar(somenteEstoqueBaixo: true).Count > 0);
            Verificar("demo: CPFs/CNPJs válidos", ClienteRepository.Listar().All(x => Formato.DocumentoValido(x.Documento)) &&
                                                 FornecedorRepository.Listar().All(x => Formato.DocumentoValido(x.Documento)));
            Verificar("demo: usuário gerente autentica", UsuarioRepository.Autenticar("gerente", "gerente123")?.Perfil == Perfis.Gerente);
            decimal recebido = c.ExecuteScalar<decimal>("SELECT COALESCE(SUM(valor_pago), 0) FROM contas_receber WHERE status <> 'CANCELADO'");
            decimal pago = c.ExecuteScalar<decimal>("SELECT COALESCE(SUM(valor_pago), 0) FROM contas_pagar WHERE status <> 'CANCELADO'");
            Igual("demo: caixa = recebido - pago", Formato.Arredondar(recebido - pago), FinanceiroService.SaldoCaixa());

            TestarRelatorios();
        }
        finally
        {
            Database.Inicializar(original);
            Sessao.Usuario = usuario;
        }
    }

    /// <summary>Consultas de dashboard/relatórios sobre a base de demonstração.</summary>
    static partial void TestarRelatoriosImpl();
    private static void TestarRelatorios() => TestarRelatoriosImpl();
}
