using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;

namespace MeuERP;

/// <summary>
/// Autoteste das regras de negócio: roda com <c>MeuERP.exe --autoteste</c> em um banco temporário
/// e grava o resultado em %TEMP%\meuerp-autoteste.log. Código de saída 0 = tudo certo.
/// </summary>
internal static partial class AutoTeste
{
    private static readonly List<string> Log = new();
    private static int _falhas;

    public static int Executar()
    {
        string pasta = Path.Combine(Path.GetTempPath(), "meuerp-autoteste-" + Guid.NewGuid().ToString("N")[..8]);
        string arquivoLog = Path.Combine(Path.GetTempPath(), "meuerp-autoteste.log");
        try
        {
            Database.Inicializar(Path.Combine(pasta, "teste.db"));
            Sessao.Usuario = UsuarioRepository.Autenticar("admin", "admin");
            Verificar("login admin/admin", Sessao.Usuario != null);
            Verificar("senha errada é recusada", UsuarioRepository.Autenticar("admin", "errada") == null);

            Rodar("Cadastros", TestarCadastros);
            Rodar("Estoque", TestarEstoque);
            Rodar("Financeiro", TestarFinanceiro);
            Rodar("Módulos", TestarModulos);
            Rodar("Backup", () => TestarBackup(pasta));
        }
        catch (Exception ex)
        {
            _falhas++;
            Log.Add("ERRO FATAL: " + ex);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(pasta, true); } catch { /* arquivos temporários */ }
        }

        Log.Add(_falhas == 0 ? "RESULTADO: OK" : $"RESULTADO: {_falhas} FALHA(S)");
        File.WriteAllLines(arquivoLog, Log);
        return _falhas == 0 ? 0 : 1;
    }

    private static void Rodar(string nome, Action teste)
    {
        Log.Add($"== {nome}");
        try { teste(); }
        catch (Exception ex) { _falhas++; Log.Add($"  [ERRO] exceção em {nome}: {ex}"); }
    }

    private static void Verificar(string descricao, bool condicao)
    {
        if (!condicao) _falhas++;
        Log.Add($"  [{(condicao ? "ok" : "FALHOU")}] {descricao}");
    }

    private static void Igual(string descricao, decimal esperado, decimal obtido) =>
        Verificar($"{descricao} (esperado {esperado}, obtido {obtido})", Math.Abs(esperado - obtido) < 0.005m);

    private static bool Lanca(Action acao)
    {
        try { acao(); return false; }
        catch (RegraException) { return true; }
    }

    private static int _produtoId, _clienteId, _fornecedorId;

    private static void TestarCadastros()
    {
        var cat = new Categoria { Nome = "Informática" };
        CategoriaRepository.Salvar(cat);
        Verificar("categoria duplicada é recusada", Lanca(() => CategoriaRepository.Salvar(new Categoria { Nome = "informática" })));

        var p = new Produto { Codigo = ProdutoRepository.ProximoCodigo(), Descricao = "Teclado mecânico", CategoriaId = cat.Id, Unidade = "UN", PrecoCusto = 100m, PrecoVenda = 199.90m, EstoqueMinimo = 5 };
        ProdutoRepository.Salvar(p);
        _produtoId = p.Id;
        var lido = ProdutoRepository.Obter(p.Id)!;
        Igual("preço de venda gravado", 199.90m, lido.PrecoVenda);
        Verificar("nome da categoria no produto", lido.CategoriaNome == "Informática");
        Verificar("data de criação lida", lido.CriadoEm.Date == DateTime.Today);
        Verificar("código duplicado é recusado", Lanca(() => ProdutoRepository.Salvar(new Produto { Codigo = p.Codigo, Descricao = "Outro" })));
        Verificar("busca por descrição", ProdutoRepository.Listar("teclado").Count == 1);
        Verificar("busca por código exato", ProdutoRepository.ObterPorCodigo(p.Codigo)?.Id == p.Id);

        var cli = new Cliente { Nome = "Maria da Silva", TipoPessoa = "F", Documento = "529.982.247-25", Cidade = "Joinville", Uf = "SC", LimiteCredito = 1000 };
        ClienteRepository.Salvar(cli);
        _clienteId = cli.Id;
        Verificar("CPF inválido é recusado", Lanca(() => ClienteRepository.Salvar(new Cliente { Nome = "X", Documento = "111.111.111-11" })));
        Verificar("CPF duplicado é recusado", Lanca(() => ClienteRepository.Salvar(new Cliente { Nome = "Y", Documento = "52998224725" })));
        Verificar("busca cliente por documento", ClienteRepository.Listar("529.982").Count == 1);

        var forn = new Fornecedor { Nome = "Distribuidora Alfa Ltda", TipoPessoa = "J", Documento = "11.222.333/0001-81", Contato = "João" };
        FornecedorRepository.Salvar(forn);
        _fornecedorId = forn.Id;
        Verificar("fornecedor gravado com contato", FornecedorRepository.Obter(forn.Id)?.Contato == "João");

        var u = new Usuario { Nome = "Operador", Login = "oper", Perfil = Perfis.Operador };
        UsuarioRepository.Salvar(u, "1234");
        Verificar("novo usuário autentica", UsuarioRepository.Autenticar("OPER", "1234") != null);
        Verificar("login duplicado é recusado", Lanca(() => UsuarioRepository.Salvar(new Usuario { Nome = "Z", Login = "oper" }, "1234")));
        Verificar("não remove o último admin", Lanca(() => UsuarioRepository.Salvar(new Usuario { Id = Sessao.UsuarioId!.Value, Nome = "Administrador", Login = "admin", Perfil = Perfis.Operador }, null)));
    }

    private static void TestarEstoque()
    {
        EstoqueService.MovimentarManual(_produtoId, TipoMov.Entrada, 10, "Estoque inicial");
        Igual("saldo após entrada", 10, ProdutoRepository.Obter(_produtoId)!.Estoque);
        EstoqueService.MovimentarManual(_produtoId, TipoMov.Entrada, 10, "Compra avulsa", custoUnitario: 120m);
        Igual("custo médio ponderado", 110, ProdutoRepository.Obter(_produtoId)!.PrecoCusto);
        EstoqueService.MovimentarManual(_produtoId, TipoMov.Saida, 4.5m, "Perda");
        Igual("saldo após saída", 15.5m, ProdutoRepository.Obter(_produtoId)!.Estoque);
        Verificar("saída maior que o saldo é bloqueada", Lanca(() => EstoqueService.MovimentarManual(_produtoId, TipoMov.Saida, 100, null)));
        EstoqueService.MovimentarManual(_produtoId, TipoMov.Ajuste, 20, "Inventário");
        Igual("saldo após ajuste", 20, ProdutoRepository.Obter(_produtoId)!.Estoque);

        var hist = EstoqueService.Historico(DateTime.Today, DateTime.Today, _produtoId);
        Verificar("histórico tem 4 movimentos", hist.Count == 4);
        Verificar("histórico traz saldos e descrição", hist[0].SaldoPosterior == 20 && hist[0].ProdutoDescricao.Length > 0);
        Verificar("produto com movimento não é excluído", Lanca(() => ProdutoRepository.Excluir(_produtoId)));
    }

    private static void TestarFinanceiro()
    {
        Database.EmTransacao((c, tx) =>
            FinanceiroService.GerarParcelas(c, tx, TipoTitulo.Receber, "Teste parcelado", _clienteId, null, 100m, 3, DateTime.Today.AddDays(30), FormasPagamento.Boleto));
        var titulos = FinanceiroService.Listar(TipoTitulo.Receber, new FiltroTitulos { Busca = "Teste parcelado" });
        Verificar("gera 3 parcelas", titulos.Count == 3);
        Igual("soma das parcelas fecha o total", 100m, titulos.Sum(t => t.Valor));
        Verificar("vencimentos mensais", titulos[1].DataVencimento == DateTime.Today.AddDays(30).AddMonths(1));
        Verificar("nome do cliente no título", titulos[0].PessoaNome == "Maria da Silva");
        Igual("saldo devedor do cliente", 100m, ClienteRepository.SaldoDevedor(_clienteId));

        decimal caixaAntes = FinanceiroService.SaldoCaixa();
        FinanceiroService.Baixar(TipoTitulo.Receber, titulos[0].Id, 10m, DateTime.Now, FormasPagamento.Pix);
        var t0 = FinanceiroService.Obter(TipoTitulo.Receber, titulos[0].Id)!;
        Verificar("baixa parcial mantém em aberto", t0.Status == StatusTitulo.Aberto && t0.ValorPago == 10m);
        FinanceiroService.Baixar(TipoTitulo.Receber, t0.Id, t0.Saldo, DateTime.Now, FormasPagamento.Pix);
        t0 = FinanceiroService.Obter(TipoTitulo.Receber, t0.Id)!;
        Verificar("baixa total quita", t0.Status == StatusTitulo.Pago && t0.DataPagamento != null);
        Igual("caixa recebeu o valor", caixaAntes + t0.Valor, FinanceiroService.SaldoCaixa());
        Verificar("baixa acima do saldo é recusada", Lanca(() => FinanceiroService.Baixar(TipoTitulo.Receber, titulos[1].Id, 9999m, DateTime.Now, FormasPagamento.Pix)));

        FinanceiroService.Estornar(TipoTitulo.Receber, t0.Id);
        Verificar("estorno reabre o título", FinanceiroService.Obter(TipoTitulo.Receber, t0.Id)!.Status == StatusTitulo.Aberto);
        Igual("estorno devolve o caixa", caixaAntes, FinanceiroService.SaldoCaixa());

        var avulso = new Titulo { Descricao = "Aluguel", Valor = 1500m, DataVencimento = DateTime.Today.AddDays(-2), PessoaId = _fornecedorId };
        FinanceiroService.SalvarManual(TipoTitulo.Pagar, avulso);
        Verificar("filtro de vencidos", FinanceiroService.Listar(TipoTitulo.Pagar, new FiltroTitulos { Situacao = "VENCIDO" }).Any(t => t.Id == avulso.Id));
        Verificar("filtro por período", FinanceiroService.Listar(TipoTitulo.Pagar, new FiltroTitulos { VencimentoDe = DateTime.Today, VencimentoAte = DateTime.Today.AddDays(5) }).All(t => t.Id != avulso.Id));
        FinanceiroService.Baixar(TipoTitulo.Pagar, avulso.Id, 1500m, DateTime.Now, FormasPagamento.Transferencia);
        Igual("pagamento sai do caixa", caixaAntes - 1500m, FinanceiroService.SaldoCaixa());
        FinanceiroService.LancarCaixaManual(TipoCaixa.Entrada, "Suprimento", 2000m, FormasPagamento.Dinheiro, DateTime.Now);
        Verificar("lista de caixa do dia", FinanceiroService.ListarCaixa(DateTime.Today, DateTime.Today).Count >= 4);
        Verificar("título pago não é cancelável", Lanca(() => FinanceiroService.Cancelar(TipoTitulo.Pagar, avulso.Id)));
        FinanceiroService.Cancelar(TipoTitulo.Receber, titulos[2].Id);
        Verificar("cancelamento de título aberto", FinanceiroService.Obter(TipoTitulo.Receber, titulos[2].Id)!.Status == StatusTitulo.Cancelado);
    }

    private static void TestarBackup(string pasta)
    {
        string destino = Path.Combine(pasta, "copia.db");
        Database.Backup(destino);
        Verificar("backup gera arquivo", new FileInfo(destino).Length > 0);
        int antes;
        using (var c = Database.Abrir()) antes = c.ExecuteScalar<int>("SELECT COUNT(*) FROM produtos");
        ProdutoRepository.Salvar(new Produto { Codigo = "TEMP-BKP", Descricao = "Criado depois do backup" });
        Database.Restaurar(destino);
        using (var c = Database.Abrir())
            Verificar("restauração volta ao estado do backup", c.ExecuteScalar<int>("SELECT COUNT(*) FROM produtos") == antes);
        Verificar("arquivo inválido é recusado", Lanca(() =>
        {
            string lixo = Path.Combine(pasta, "lixo.db");
            File.WriteAllText(lixo, "isto não é um banco");
            Database.Restaurar(lixo);
        }));
    }

    /// <summary>Testes dos módulos de vendas, compras, dashboard e relatórios (preenchido na integração).</summary>
    static partial void TestarModulosImpl();
    private static void TestarModulos() => TestarModulosImpl();
}
