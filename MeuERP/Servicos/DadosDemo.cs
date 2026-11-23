using System.Data;
using System.Globalization;
using System.Text;
using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;

namespace MeuERP.Servicos;

/// <summary>
/// Gera uma base de demonstração realista (loja de informática e papelaria) com cerca de 6 meses de histórico,
/// numa única transação e com sorteio de semente fixa. Os lançamentos são feitos por SQL direto na transação,
/// em ordem cronológica, e as invariantes de estoque, vendas e financeiro são conferidas antes do commit.
/// </summary>
public static class DadosDemo
{
    /// <summary>Gera os dados e devolve um resumo do que foi criado.</summary>
    public static string Gerar() => Database.EmTransacao((c, tx) => new Gerador(c, tx).Executar());

    private sealed class Prod
    {
        public int Id;
        public string Codigo = "", Descricao = "", Categoria = "", Unidade = "UN";
        public decimal CustoBase, Custo, Preco, Estoque, Minimo;
        public int Alvo, Peso, QtdMax, Fornecedor;
    }

    private sealed record Pessoa(int Id, string Nome, int Peso);

    private sealed class Gerador
    {
        private readonly IDbConnection _c;
        private readonly IDbTransaction _tx;
        private readonly Random _rnd = new(20251123);
        private readonly DateTime _agora = DateTime.Now, _hoje = DateTime.Today, _inicio;
        private DateTime _diaAtual;

        private readonly List<Prod> _produtos = new();
        private readonly List<Pessoa> _clientes = new();
        private readonly List<(int Id, string Nome)> _fornecedores = new();
        private readonly List<(int Id, int Peso)> _vendedores = new();
        private int? _adminId, _gerenteId;
        private readonly List<(DateTime Quando, Action Acao)> _agenda = new();
        private readonly HashSet<string> _documentos = new();
        private HashSet<Prod> _escassos = new();

        private long _antesVenda, _antesReceber, _antesPagar, _antesCaixa, _antesMov;
        private int _qtdVendas, _qtdOrcamentos, _qtdCompras, _qtdTitulos, _usuariosCriados, _categoriasCriadas;
        private decimal _faturamento;

        public Gerador(IDbConnection c, IDbTransaction tx)
        {
            _c = c;
            _tx = tx;
            _inicio = _hoje.AddDays(-182);
        }

        // ---------- utilidades ----------

        private T Escalar<T>(string sql, object? p = null) => _c.ExecuteScalar<T>(sql, p, _tx)!;
        private void Exec(string sql, object? p = null) => _c.Execute(sql, p, _tx);
        private int Inserir(string sql, object p) => _c.ExecuteScalar<int>(sql + "; SELECT last_insert_rowid();", p, _tx);
        private static decimal R(decimal v) => Formato.Arredondar(v);
        private double Sorteio() => _rnd.NextDouble();
        private T Um<T>(IReadOnlyList<T> lista) => lista[_rnd.Next(lista.Count)];

        private T Ponderado<T>(IReadOnlyList<T> itens, Func<T, int> peso)
        {
            int total = itens.Sum(peso);
            int x = _rnd.Next(total);
            foreach (var i in itens)
            {
                x -= peso(i);
                if (x < 0) return i;
            }
            return itens[^1];
        }

        private int Poisson(double lambda)
        {
            double l = Math.Exp(-lambda), p = 1;
            int k = 0;
            do { k++; p *= Sorteio(); } while (p > l);
            return k - 1;
        }

        private static string SemAcento(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }

        private bool DocumentoExiste(string doc) =>
            !_documentos.Add(doc) ||
            Escalar<long>("SELECT (SELECT COUNT(*) FROM clientes WHERE documento = @doc) + (SELECT COUNT(*) FROM fornecedores WHERE documento = @doc)", new { doc }) > 0;

        private string GerarCpf()
        {
            while (true)
            {
                var d = new int[11];
                for (int i = 0; i < 9; i++) d[i] = _rnd.Next(10);
                for (int j = 9; j < 11; j++)
                {
                    int soma = 0;
                    for (int i = 0; i < j; i++) soma += d[i] * (j + 1 - i);
                    d[j] = soma * 10 % 11 % 10;
                }
                string s = string.Concat(d);
                if (Formato.CpfValido(s) && !DocumentoExiste(s)) return s;
            }
        }

        private string GerarCnpj()
        {
            int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            while (true)
            {
                var d = new int[14];
                for (int i = 0; i < 8; i++) d[i] = _rnd.Next(10);
                d[8] = 0; d[9] = 0; d[10] = 0; d[11] = 1;
                int Dig(int[] pesos)
                {
                    int soma = 0;
                    for (int i = 0; i < pesos.Length; i++) soma += d[i] * pesos[i];
                    int r = soma % 11;
                    return r < 2 ? 0 : 11 - r;
                }
                d[12] = Dig(p1);
                d[13] = Dig(p2);
                string s = string.Concat(d);
                if (Formato.CnpjValido(s) && !DocumentoExiste(s)) return s;
            }
        }

        private string GerarEan13()
        {
            var d = new int[13];
            d[0] = 7; d[1] = 8; d[2] = 9;
            for (int i = 3; i < 12; i++) d[i] = _rnd.Next(10);
            int soma = 0;
            for (int i = 0; i < 12; i++) soma += d[i] * (i % 2 == 0 ? 1 : 3);
            d[12] = (10 - soma % 10) % 10;
            return string.Concat(d);
        }

        private static readonly (string Cidade, string Ddd, string Cep, int Peso)[] Cidades =
        {
            ("Joinville", "47", "892", 9), ("Blumenau", "47", "890", 3), ("Jaraguá do Sul", "47", "892", 3),
            ("Florianópolis", "48", "880", 3), ("São José", "48", "881", 2), ("Itajaí", "47", "883", 2),
            ("Balneário Camboriú", "47", "883", 2), ("Brusque", "47", "883", 1), ("Criciúma", "48", "888", 1),
            ("Chapecó", "49", "898", 1), ("Lages", "49", "885", 1), ("São Bento do Sul", "47", "892", 2),
        };

        private static readonly string[] Ruas =
        {
            "Rua XV de Novembro", "Rua das Palmeiras", "Av. Getúlio Vargas", "Rua Blumenau", "Rua Dona Francisca",
            "Rua São Paulo", "Rua Sete de Setembro", "Av. Beira-Mar", "Rua Marechal Deodoro", "Rua Princesa Isabel",
            "Rua Itajaí", "Rua Max Colin", "Rua Otto Boehm", "Av. Santos Dumont"
        };

        private static readonly string[] Bairros =
        {
            "Centro", "América", "Glória", "Atiradores", "Bom Retiro", "Itoupava", "Velha", "Trindade",
            "Costeira", "Fazenda", "Vila Nova", "Saguaçu", "Anita Garibaldi", "Boa Vista"
        };

        private (string cidade, string ddd, string cep, string endereco, string numero, string bairro) Endereco()
        {
            var cid = Ponderado(Cidades, x => x.Peso);
            string cep = $"{cid.Cep}{_rnd.Next(10, 99)}-{_rnd.Next(0, 999):000}";
            return (cid.Cidade, cid.Ddd, cep, Um(Ruas), _rnd.Next(12, 2900).ToString(), Um(Bairros));
        }

        // ---------- execução ----------

        public string Executar()
        {
            _antesVenda = Escalar<long>("SELECT COALESCE(MAX(id), 0) FROM vendas");
            _antesReceber = Escalar<long>("SELECT COALESCE(MAX(id), 0) FROM contas_receber");
            _antesPagar = Escalar<long>("SELECT COALESCE(MAX(id), 0) FROM contas_pagar");
            _antesCaixa = Escalar<long>("SELECT COALESCE(MAX(id), 0) FROM caixa_lancamentos");
            _antesMov = Escalar<long>("SELECT COALESCE(MAX(id), 0) FROM movimentacoes_estoque");

            CriarUsuarios();
            CriarFornecedores();
            CriarProdutos();
            CriarClientes();
            SimularHistorico();
            AjustarEstoqueMinimo();
            foreach (var p in _produtos)
                Exec("UPDATE produtos SET estoque = @Estoque, preco_custo = @Custo, estoque_minimo = @Minimo WHERE id = @Id",
                    new { p.Estoque, Custo = Math.Round(p.Custo, 4), p.Minimo, p.Id });
            Verificar();

            int baixo = _produtos.Count(p => p.Minimo > 0 && p.Estoque <= p.Minimo);
            var sb = new StringBuilder();
            sb.Append($"{_produtos.Count} produtos, {_clientes.Count} clientes, {_fornecedores.Count} fornecedores, ");
            sb.Append($"{_qtdVendas} vendas, {_qtdCompras} compras, {_qtdOrcamentos} orçamentos e {_qtdTitulos} títulos financeiros");
            if (_categoriasCriadas > 0) sb.Append($", {_categoriasCriadas} categorias");
            if (_usuariosCriados > 0) sb.Append($" e {_usuariosCriados} usuários (gerente/gerente123 e vendedor/vendedor123)");
            sb.Append(".\n\n");
            sb.Append($"Faturamento dos últimos 6 meses: {Formato.Moeda(_faturamento)}. {baixo} produtos ficaram abaixo do estoque mínimo.");
            return sb.ToString();
        }

        private void CriarUsuarios()
        {
            _adminId = _c.QueryFirstOrDefault<int?>("SELECT id FROM usuarios WHERE perfil = 'ADMIN' AND ativo = 1 ORDER BY id LIMIT 1", null, _tx)
                       ?? _c.QueryFirstOrDefault<int?>("SELECT id FROM usuarios ORDER BY id LIMIT 1", null, _tx);
            int? Garantir(string nome, string login, string senha, string perfil)
            {
                var id = _c.QueryFirstOrDefault<int?>("SELECT id FROM usuarios WHERE login = @login", new { login }, _tx);
                if (id != null) return id;
                _usuariosCriados++;
                return Inserir("INSERT INTO usuarios (nome, login, senha_hash, perfil, ativo, criado_em) VALUES (@nome, @login, @hash, @perfil, 1, @data)",
                    new { nome, login, hash = Seguranca.GerarHash(senha), perfil, data = _inicio.AddHours(8) });
            }
            _gerenteId = Garantir("Fernanda Lima", "gerente", "gerente123", Perfis.Gerente);
            int? vendedor = Garantir("Rafael Souza", "vendedor", "vendedor123", Perfis.Operador);
            if (vendedor != null) _vendedores.Add((vendedor.Value, 60));
            if (_gerenteId != null) _vendedores.Add((_gerenteId.Value, 25));
            if (_adminId != null) _vendedores.Add((_adminId.Value, 15));
            _gerenteId ??= _adminId;
        }

        private static readonly (string Nome, string Fantasia, string Contato, string[] Categorias)[] DefFornecedores =
        {
            ("TechSul Distribuidora de Informática Ltda", "TechSul", "Marcelo Andrade", new[] { "Informática" }),
            ("Mega Byte Importadora Ltda", "Mega Byte", "Patrícia Nunes", new[] { "Informática", "Armazenamento" }),
            ("Alfa Periféricos Comércio Ltda", "Alfa Periféricos", "Ricardo Souza", new[] { "Periféricos" }),
            ("Data Storage Brasil Ltda", "Data Storage", "Simone Reis", new[] { "Armazenamento" }),
            ("Conecta Cabos e Acessórios Ltda", "Conecta", "André Luiz", new[] { "Cabos e Adaptadores" }),
            ("Papelaria Catarinense Atacado Ltda", "Catarinense Atacado", "Luciana Prates", new[] { "Papelaria" }),
            ("Norte Papéis e Embalagens Ltda", "Norte Papéis", "Jorge Hammes", new[] { "Papelaria", "Escritório" }),
            ("Escritório Total Distribuidora Ltda", "Escritório Total", "Cláudia Meyer", new[] { "Escritório" }),
        };

        private void CriarFornecedores()
        {
            foreach (var f in DefFornecedores)
            {
                var e = Endereco();
                string dominio = SemAcento(f.Fantasia).ToLowerInvariant().Replace(" ", "");
                int id = Inserir(@"INSERT INTO fornecedores (tipo_pessoa, nome, nome_fantasia, documento, inscricao_rg, telefone, email, cep,
                        endereco, numero, bairro, cidade, uf, observacoes, contato, ativo, criado_em)
                    VALUES ('J', @nome, @fantasia, @doc, @ie, @tel, @email, @cep, @end, @num, @bairro, @cidade, 'SC', '', @contato, 1, @data)",
                    new
                    {
                        nome = f.Nome, fantasia = f.Fantasia, doc = GerarCnpj(), ie = _rnd.Next(250000000, 260000000).ToString(),
                        tel = $"({e.ddd}) 3{_rnd.Next(100, 999)}-{_rnd.Next(1000, 9999)}", email = $"vendas@{dominio}.com.br",
                        cep = e.cep, end = e.endereco, num = e.numero, bairro = e.bairro, cidade = e.cidade, contato = f.Contato,
                        data = _inicio.AddHours(8)
                    });
                _fornecedores.Add((id, f.Nome));
            }
        }

        // (categoria, descrição, unidade, custo, estoque-alvo, estoque mínimo, popularidade, qtd. máx. por venda)
        private static readonly (string Cat, string Desc, string Un, decimal Custo, int Alvo, int Min, int Peso, int QtdMax)[] DefProdutos =
        {
            ("Informática", "Notebook Lenovo IdeaPad 15\" i5 8GB SSD 512GB", "UN", 2890m, 6, 2, 2, 1),
            ("Informática", "Notebook Dell Inspiron 14\" i7 16GB SSD 512GB", "UN", 4150m, 4, 1, 1, 1),
            ("Informática", "Monitor LG 24\" Full HD IPS", "UN", 640m, 8, 3, 3, 1),
            ("Informática", "Monitor Samsung 27\" Curvo", "UN", 1080m, 5, 2, 2, 1),
            ("Informática", "Impressora Multifuncional HP Ink Tank", "UN", 720m, 5, 2, 2, 1),
            ("Informática", "Roteador Wi-Fi TP-Link AC1200", "UN", 165m, 10, 4, 4, 1),
            ("Periféricos", "Mouse óptico USB Logitech M90", "UN", 24.90m, 40, 10, 10, 3),
            ("Periféricos", "Mouse sem fio Logitech M170", "UN", 52m, 30, 8, 8, 2),
            ("Periféricos", "Teclado USB ABNT2 Multilaser", "UN", 32m, 30, 8, 7, 2),
            ("Periféricos", "Teclado mecânico Redragon Kumara", "UN", 189m, 10, 3, 3, 1),
            ("Periféricos", "Kit teclado e mouse sem fio Logitech MK270", "UN", 125m, 15, 5, 4, 1),
            ("Periféricos", "Headset gamer HyperX Cloud Stinger", "UN", 210m, 8, 3, 3, 1),
            ("Periféricos", "Webcam Full HD Logitech C920", "UN", 330m, 6, 2, 2, 1),
            ("Periféricos", "Caixa de som USB 2.0 6W", "UN", 45m, 15, 5, 3, 2),
            ("Periféricos", "Mousepad gamer grande 70x30 cm", "UN", 28m, 25, 6, 4, 2),
            ("Armazenamento", "Pen drive 32GB SanDisk", "UN", 22.50m, 50, 12, 9, 3),
            ("Armazenamento", "Pen drive 64GB Kingston", "UN", 34m, 40, 10, 7, 3),
            ("Armazenamento", "SSD 480GB Kingston A400", "UN", 175m, 12, 4, 4, 1),
            ("Armazenamento", "HD externo 1TB Seagate", "UN", 310m, 6, 2, 2, 1),
            ("Armazenamento", "Cartão de memória microSD 64GB", "UN", 38m, 25, 8, 5, 2),
            ("Cabos e Adaptadores", "Cabo HDMI 2.0 1,8 m", "UN", 15.90m, 50, 12, 8, 3),
            ("Cabos e Adaptadores", "Cabo USB-C para USB-C 1 m", "UN", 18m, 40, 10, 6, 3),
            ("Cabos e Adaptadores", "Carregador USB-C 20W", "UN", 42m, 25, 8, 5, 2),
            ("Cabos e Adaptadores", "Adaptador HDMI para VGA", "UN", 24m, 20, 6, 3, 2),
            ("Cabos e Adaptadores", "Filtro de linha 5 tomadas", "UN", 39m, 20, 6, 4, 2),
            ("Papelaria", "Caneta esferográfica azul BIC (caixa c/ 50)", "CX", 38m, 20, 5, 4, 2),
            ("Papelaria", "Caderno universitário 10 matérias 200 folhas", "UN", 17.50m, 60, 15, 9, 4),
            ("Papelaria", "Lápis preto HB nº 2 (caixa c/ 12)", "CX", 9.80m, 40, 10, 5, 3),
            ("Papelaria", "Borracha branca Mercur", "UN", 1.20m, 100, 25, 5, 6),
            ("Papelaria", "Marca-texto amarelo Stabilo", "UN", 4.10m, 80, 20, 6, 5),
            ("Papelaria", "Papel sulfite A4 75g (resma 500 folhas)", "PC", 24.90m, 60, 15, 9, 5),
            ("Papelaria", "Cola branca 90 g Tenaz", "UN", 3.20m, 60, 15, 4, 4),
            ("Papelaria", "Tesoura escolar sem ponta", "UN", 4.50m, 40, 10, 3, 3),
            ("Papelaria", "Fita adesiva transparente 45 mm x 45 m", "UN", 5.80m, 60, 15, 4, 5),
            ("Escritório", "Grampeador de mesa 26/6", "UN", 26m, 15, 4, 2, 1),
            ("Escritório", "Grampos 26/6 (caixa c/ 5000)", "CX", 6.20m, 40, 10, 3, 3),
            ("Escritório", "Pasta com elástico ofício", "UN", 3.10m, 60, 15, 3, 6),
            ("Escritório", "Envelope pardo A4 (pacote c/ 50)", "PC", 29m, 20, 5, 2, 2),
            ("Escritório", "Calculadora de mesa 12 dígitos", "UN", 32m, 15, 4, 2, 1),
            ("Escritório", "Cartucho de tinta HP 664 preto", "UN", 62m, 20, 6, 5, 2),
            ("Escritório", "Toner compatível HP 85A", "UN", 78m, 12, 4, 3, 2),
        };

        private void CriarProdutos()
        {
            var categorias = new Dictionary<string, int>();
            foreach (string nome in DefProdutos.Select(d => d.Cat).Distinct())
            {
                var id = _c.QueryFirstOrDefault<int?>("SELECT id FROM categorias WHERE nome = @nome", new { nome }, _tx);
                if (id == null)
                {
                    id = Inserir("INSERT INTO categorias (nome, ativo) VALUES (@nome, 1)", new { nome });
                    _categoriasCriadas++;
                }
                categorias[nome] = id.Value;
            }

            int seq = 1001;
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in DefProdutos)
            {
                string codigo;
                do { codigo = (seq++).ToString(CultureInfo.InvariantCulture); }
                while (!usados.Add(codigo) || Escalar<long>("SELECT COUNT(*) FROM produtos WHERE codigo = @codigo", new { codigo }) > 0);

                decimal margem = 0.25m + (decimal)Sorteio() * 0.35m;
                decimal preco = Math.Ceiling(d.Custo * (1 + margem)) - 0.10m;
                if (preco < d.Custo * 1.2m) preco = Math.Ceiling(d.Custo * 1.3m * 10) / 10;
                // fornecedor: um dos que atendem a categoria (alterna entre eles)
                var possiveis = DefFornecedores.Select((f, i) => (f, i)).Where(x => x.f.Categorias.Contains(d.Cat)).Select(x => x.i).ToList();
                var p = new Prod
                {
                    Codigo = codigo, Descricao = d.Desc, Categoria = d.Cat, Unidade = d.Un, CustoBase = d.Custo, Custo = d.Custo,
                    Preco = preco, Minimo = d.Min, Alvo = d.Alvo, Peso = d.Peso, QtdMax = d.QtdMax,
                    Fornecedor = possiveis[_produtos.Count % possiveis.Count]
                };
                p.Id = Inserir(@"INSERT INTO produtos (codigo, codigo_barras, descricao, categoria_id, unidade, preco_custo, preco_venda,
                        estoque, estoque_minimo, ativo, criado_em)
                    VALUES (@Codigo, @barras, @Descricao, @cat, @Unidade, @Custo, @Preco, 0, @Minimo, 1, @data)",
                    new { p.Codigo, barras = GerarEan13(), p.Descricao, cat = categorias[d.Cat], p.Unidade, p.Custo, p.Preco, p.Minimo, data = _inicio.AddHours(8) });
                _produtos.Add(p);
            }
            _escassos = _produtos.Where(p => p.Peso >= 4).OrderByDescending(p => p.Peso).ThenBy(p => p.Id).Take(5).ToHashSet();
        }

        private static readonly (string Nome, bool Juridica, int Peso)[] DefClientes =
        {
            ("Ana Paula Souza", false, 3), ("Bruno Henrique Oliveira", false, 2), ("Carla Regina Martins", false, 2),
            ("Diego Fernandes Lima", false, 2), ("Eduarda Costa Ribeiro", false, 1), ("Felipe Augusto Schmitt", false, 3),
            ("Gabriela Hoffmann", false, 2), ("Henrique Kowalski", false, 1), ("Isabela Ramos Pereira", false, 2),
            ("João Pedro Almeida", false, 3), ("Juliana Vieira Krause", false, 2), ("Lucas Gabriel Santos", false, 2),
            ("Mariana Oliveira Becker", false, 2), ("Marcos Antônio da Silva", false, 2), ("Natália Rocha Cardoso", false, 1),
            ("Pedro Henrique Werner", false, 2), ("Rafaela Moreira", false, 1), ("Rodrigo Teixeira Müller", false, 2),
            ("Tatiane Correia", false, 1), ("Vinícius Barbosa Zimmermann", false, 2),
            ("Escola Pequeno Saber Ltda", true, 4), ("Contabilidade Precisão Ltda", true, 4), ("Clínica Bem Estar Ltda", true, 3),
            ("Imobiliária Costa Verde Ltda", true, 3), ("Lemos & Associados Advocacia", true, 3),
        };

        private void CriarClientes()
        {
            foreach (var d in DefClientes)
            {
                var e = Endereco();
                string semAcento = SemAcento(d.Nome).ToLowerInvariant();
                string email;
                if (d.Juridica)
                {
                    string dominio = new string(semAcento.Replace(" ltda", "").Where(char.IsLetter).ToArray());
                    email = $"contato@{dominio}.com.br";
                }
                else
                {
                    var partes = semAcento.Split(' ');
                    email = $"{partes[0]}.{partes[^1]}@email.com.br";
                }
                int id = Inserir(@"INSERT INTO clientes (tipo_pessoa, nome, nome_fantasia, documento, inscricao_rg, telefone, email, cep,
                        endereco, numero, bairro, cidade, uf, observacoes, limite_credito, ativo, criado_em)
                    VALUES (@tipo, @nome, '', @doc, '', @tel, @email, @cep, @end, @num, @bairro, @cidade, 'SC', '', @limite, 1, @data)",
                    new
                    {
                        tipo = d.Juridica ? "J" : "F", nome = d.Nome, doc = d.Juridica ? GerarCnpj() : GerarCpf(),
                        tel = d.Juridica ? $"({e.ddd}) 3{_rnd.Next(100, 999)}-{_rnd.Next(1000, 9999)}" : $"({e.ddd}) 9{_rnd.Next(8000, 9999)}-{_rnd.Next(1000, 9999)}",
                        email, cep = e.cep, end = e.endereco, num = e.numero, bairro = e.bairro, cidade = e.cidade,
                        limite = d.Juridica ? 5000m : _rnd.Next(5, 31) * 100m,
                        data = _inicio.AddHours(9).AddDays(_rnd.Next(0, 20))
                    });
                _clientes.Add(new Pessoa(id, d.Nome, d.Peso));
            }
        }

        // ---------- linha do tempo ----------

        private void SimularHistorico()
        {
            int totalDias = (_hoje - _inicio).Days;
            var eventos = new List<(DateTime Quando, Action Acao)>();
            for (var dia = _inicio; dia <= _hoje; dia = dia.AddDays(1))
            {
                var d = dia;
                _diaAtual = d;
                eventos.Clear();
                bool passado = d < _hoje;

                if (d == _inicio) eventos.Add((d.AddHours(8), () => Implantacao(d.AddHours(8))));
                if (passado && (d == _inicio || d.Day == 1)) eventos.Add((d.AddHours(8).AddMinutes(5), () => Despesas(d)));
                if (passado && d > _inicio && d.Day == 5) eventos.Add((d.AddHours(9).AddMinutes(10), () => Reposicao(d.AddHours(9).AddMinutes(10))));

                // pagamentos agendados para hoje
                foreach (var a in _agenda.Where(a => a.Quando.Date == d).ToList())
                {
                    eventos.Add(a);
                    _agenda.Remove(a);
                }

                // vendas do dia: mais movimento nos meses recentes, sábado mais fraco, domingo fechado
                double fracao = totalDias > 0 ? (d - _inicio).Days / (double)totalDias : 1;
                double lambda = 2.05 * (0.55 + 0.9 * fracao);
                if (d.DayOfWeek == DayOfWeek.Saturday) lambda *= 0.6;
                if (d.DayOfWeek == DayOfWeek.Sunday) lambda = 0;
                int qtd = Poisson(lambda);
                if (d == _hoje) qtd = Math.Max(qtd, 4);
                for (int i = 0; i < qtd; i++)
                {
                    DateTime quando = HorarioNoDia(d);
                    eventos.Add((quando, () => Venda(quando, StatusVenda.Finalizada)));
                }
                if (d >= _hoje.AddDays(-1))
                {
                    DateTime quando = HorarioNoDia(d);
                    eventos.Add((quando, () => Venda(quando, StatusVenda.Orcamento)));
                }

                foreach (var ev in eventos.OrderBy(e => e.Quando).ToList()) ev.Acao();
            }
            // pagamentos agendados que ficariam para depois de hoje não devem existir
            if (_agenda.Count > 0) throw new InvalidOperationException("Dados de demonstração: pagamentos agendados para o futuro.");
        }

        /// <summary>Horário comercial no dia; hoje, nunca depois de agora.</summary>
        private DateTime HorarioNoDia(DateTime dia)
        {
            if (dia < _hoje)
                return dia.AddHours(9).AddMinutes(_rnd.Next(0, 10 * 60)).AddSeconds(_rnd.Next(60));
            var inicio = _agora.Hour >= 9 ? _hoje.AddHours(8).AddMinutes(30) : _hoje;
            double janela = Math.Max(1, (_agora.AddMinutes(-2) - inicio).TotalSeconds);
            var t = inicio.AddSeconds(_rnd.NextDouble() * janela);
            return t > _agora ? _agora : t;
        }

        private void Agendar(DateTime quando, Action acao)
        {
            if (quando.Date >= _hoje) throw new InvalidOperationException("Pagamento agendado para hoje ou depois.");
            if (quando.Date <= _diaAtual) acao();
            else _agenda.Add((quando, acao));
        }

        private void Movimentar(Prod p, string tipo, decimal qtd, decimal custo, string origem, int? origemId, string obs, int? usuario, DateTime quando)
        {
            decimal anterior = p.Estoque;
            decimal posterior = tipo == TipoMov.Entrada ? anterior + qtd : anterior - qtd;
            if (qtd <= 0 || posterior < 0) throw new InvalidOperationException($"Dados de demonstração: movimentação inválida em {p.Descricao}.");
            if (tipo == TipoMov.Entrada && custo > 0)
                p.Custo = anterior > 0 && posterior > 0 ? Math.Round((anterior * p.Custo + qtd * custo) / posterior, 4) : custo;
            p.Estoque = posterior;
            Exec(@"INSERT INTO movimentacoes_estoque (produto_id, tipo, quantidade, saldo_anterior, saldo_posterior, custo_unitario,
                    origem, origem_id, observacao, usuario_id, data_movimento)
                VALUES (@id, @tipo, @qtd, @anterior, @posterior, @custo, @origem, @origemId, @obs, @usuario, @quando)",
                new { id = p.Id, tipo, qtd, anterior, posterior, custo = Math.Round(custo, 4), origem, origemId, obs, usuario, quando });
        }

        private void LancarCaixa(string tipo, string descricao, decimal valor, string forma, string origem, int origemId, int? usuario, DateTime quando) =>
            Exec(@"INSERT INTO caixa_lancamentos (data, tipo, descricao, valor, forma_pagamento, origem, origem_id, usuario_id)
                VALUES (@quando, @tipo, @descricao, @valor, @forma, @origem, @origemId, @usuario)",
                new { quando, tipo, descricao, valor, forma, origem, origemId, usuario });

        /// <summary>Insere as parcelas (mesma regra de divisão do FinanceiroService) e devolve (id, valor, vencimento) de cada uma.</summary>
        private List<(int Id, decimal Valor, DateTime Vencimento, int Parcela)> Parcelas(string tabela, string colPessoa, string colOrigem,
            string descricao, int? pessoaId, int? origemId, decimal total, int parcelas, DateTime emissao, Func<int, DateTime> vencimento, string forma)
        {
            var lista = new List<(int, decimal, DateTime, int)>();
            decimal valorParcela = Math.Floor(total / parcelas * 100) / 100;
            decimal resto = total - valorParcela * parcelas;
            for (int i = 1; i <= parcelas; i++)
            {
                decimal valor = i == parcelas ? valorParcela + resto : valorParcela;
                DateTime venc = vencimento(i).Date;
                int id = Inserir($@"INSERT INTO {tabela} (descricao, {colPessoa}, {colOrigem}, parcela, total_parcelas, valor, valor_pago,
                        data_emissao, data_vencimento, data_pagamento, forma_pagamento, status)
                    VALUES (@descricao, @pessoaId, @origemId, @i, @parcelas, @valor, 0, @emissao, @venc, NULL, @forma, 'ABERTO')",
                    new { descricao, pessoaId, origemId, i, parcelas, valor, emissao = emissao.Date, venc, forma });
                _qtdTitulos++;
                lista.Add((id, valor, venc, i));
            }
            return lista;
        }

        /// <summary>Quita o título (valor integral) e lança no caixa.</summary>
        private void Quitar(bool receber, int id, decimal valor, string descricaoCaixa, string forma, int? usuario, DateTime quando)
        {
            string tabela = receber ? "contas_receber" : "contas_pagar";
            Exec($"UPDATE {tabela} SET valor_pago = valor, status = 'PAGO', data_pagamento = @quando, forma_pagamento = @forma WHERE id = @id",
                new { id, quando, forma });
            LancarCaixa(receber ? TipoCaixa.Entrada : TipoCaixa.Saida, descricaoCaixa, valor, forma,
                receber ? "RECEBIMENTO" : "PAGAMENTO", id, usuario, quando);
        }

        private void Implantacao(DateTime quando)
        {
            foreach (var p in _produtos)
                Movimentar(p, TipoMov.Entrada, p.Alvo, p.CustoBase, "MANUAL", null, "Saldo inicial (implantação do sistema)", _adminId, quando);
        }

        private void Despesas(DateTime dia)
        {
            var mes = new DateTime(dia.Year, dia.Month, 1);
            string ref_ = mes.ToString("MMM/yyyy", Formato.PtBr).Replace(".", "");
            var lista = new (string Desc, decimal Valor, int DiaVenc, string Forma)[]
            {
                ("Honorários contábeis", 480m, 7, FormasPagamento.Pix),
                ("Aluguel da loja", 2300m, 10, FormasPagamento.Boleto),
                ("Energia elétrica", R(380m + (decimal)Sorteio() * 180m), 15, FormasPagamento.Boleto),
                ("Internet e telefone", 189.90m, 20, FormasPagamento.Boleto),
            };
            foreach (var d in lista)
            {
                var venc = new DateTime(mes.Year, mes.Month, d.DiaVenc);
                if (venc < dia) continue;
                string desc = $"{d.Desc} - {ref_}";
                var t = Parcelas("contas_pagar", "fornecedor_id", "compra_id", desc, null, null, d.Valor, 1, dia, _ => venc, d.Forma)[0];
                if (venc < _hoje)
                {
                    var quando = venc.AddHours(10).AddMinutes(_rnd.Next(0, 240));
                    Agendar(quando, () => Quitar(false, t.Id, t.Valor, "Pagamento: " + desc, d.Forma, _gerenteId, quando));
                }
            }
        }

        private void Reposicao(DateTime quando)
        {
            // no último ciclo os produtos "escassos" não são repostos (ficam abaixo do mínimo naturalmente)
            bool ultimoCiclo = quando.Date > _hoje.AddDays(-31);
            var precisam = _produtos.Where(p => p.Estoque < p.Alvo * 0.6m && !(ultimoCiclo && _escassos.Contains(p))).GroupBy(p => p.Fornecedor);
            foreach (var grupo in precisam)
            {
                var (fornId, fornNome) = _fornecedores[grupo.Key];
                var itens = grupo.Select(p =>
                {
                    decimal qtd = p.Alvo - p.Estoque;
                    decimal custo = R(p.CustoBase * (0.95m + (decimal)Sorteio() * 0.12m));
                    return (p, qtd, custo, total: R(qtd * custo));
                }).ToList();
                decimal subtotal = itens.Sum(i => i.total);
                decimal frete = subtotal > 1500 && Sorteio() < 0.4 ? _rnd.Next(35, 120) : 0;
                decimal total = subtotal + frete;
                bool pix = total < 400 && Sorteio() < 0.5;
                string forma = pix ? FormasPagamento.Pix : FormasPagamento.Boleto;
                int parcelas = pix ? 1 : total > 6000 ? 3 : total > 1500 ? 2 : 1;
                string nf = _rnd.Next(10000, 99999).ToString(CultureInfo.InvariantCulture);
                DateTime pedido = quando.Date.AddDays(-_rnd.Next(2, 6)).AddHours(14).AddMinutes(_rnd.Next(0, 120));

                int compraId = Inserir(@"INSERT INTO compras (fornecedor_id, usuario_id, data_compra, data_recebimento, status, numero_nf,
                        subtotal, desconto, frete, total, forma_pagamento, parcelas, observacoes)
                    VALUES (@fornId, @usuario, @pedido, @quando, 'RECEBIDA', @nf, @subtotal, 0, @frete, @total, @forma, @parcelas, '')",
                    new { fornId, usuario = _gerenteId, pedido, quando, nf, subtotal, frete, total, forma, parcelas });
                _qtdCompras++;
                foreach (var i in itens)
                {
                    Exec("INSERT INTO compra_itens (compra_id, produto_id, quantidade, custo_unitario, total) VALUES (@compraId, @id, @qtd, @custo, @total)",
                        new { compraId, id = i.p.Id, i.qtd, i.custo, i.total });
                    Movimentar(i.p, TipoMov.Entrada, i.qtd, i.custo, "COMPRA", compraId, $"Compra nº {compraId} - NF {nf}", _gerenteId, quando);
                }

                string desc = $"Compra nº {compraId} - NF {nf}";
                var titulos = Parcelas("contas_pagar", "fornecedor_id", "compra_id", desc, fornId, compraId, total, parcelas, quando,
                    i => pix ? quando : quando.AddMonths(i), forma);
                foreach (var t in titulos)
                {
                    string descCaixa = "Pagamento: " + desc + (parcelas > 1 ? $" ({t.Parcela}/{parcelas})" : "");
                    if (pix) { Quitar(false, t.Id, t.Valor, descCaixa, forma, _gerenteId, quando); continue; }
                    if (t.Vencimento >= _hoje) continue;
                    // as que venceram nos últimos dias às vezes ainda estão em aberto
                    if (t.Vencimento >= _hoje.AddDays(-4) && Sorteio() < 0.5) continue;
                    var q = t.Vencimento.AddHours(11).AddMinutes(_rnd.Next(0, 300));
                    Agendar(q, () => Quitar(false, t.Id, t.Valor, descCaixa, forma, _gerenteId, q));
                }
            }
        }

        private void Venda(DateTime quando, string status)
        {
            // itens
            int nItens = Ponderado(new[] { (1, 40), (2, 30), (3, 20), (4, 10) }, x => x.Item2).Item1;
            var escolhidos = new List<(Prod p, decimal qtd)>();
            for (int tentativa = 0; tentativa < nItens * 3 && escolhidos.Count < nItens; tentativa++)
            {
                var p = Ponderado(_produtos, x => x.Peso);
                if (escolhidos.Any(e => e.p == p)) continue;
                double u = Sorteio();
                decimal qtd = 1 + (int)Math.Floor(u * u * p.QtdMax);
                if (status == StatusVenda.Finalizada) qtd = Math.Min(qtd, p.Estoque);
                if (qtd <= 0) continue;
                escolhidos.Add((p, qtd));
            }
            if (escolhidos.Count == 0) return;

            var itens = escolhidos.Select(e => (e.p, e.qtd, preco: e.p.Preco, total: R(e.qtd * e.p.Preco), custo: Math.Round(e.p.Custo, 4))).ToList();
            decimal subtotal = itens.Sum(i => i.total);
            decimal desconto = subtotal > 200 && Sorteio() < 0.18 ? R(subtotal * Um(new[] { 0.03m, 0.05m, 0.10m })) : 0;
            decimal total = subtotal - desconto;

            Pessoa? cliente = Sorteio() < 0.5 ? Ponderado(_clientes, c => c.Peso) : null;
            string forma;
            int parcelas = 1;
            if (cliente == null)
                forma = Ponderado(new[] { (FormasPagamento.Dinheiro, 30), (FormasPagamento.Pix, 40), (FormasPagamento.Debito, 30) }, x => x.Item2).Item1;
            else
            {
                forma = Ponderado(new[]
                {
                    (FormasPagamento.Pix, 25), (FormasPagamento.Debito, 15), (FormasPagamento.Dinheiro, 10),
                    (FormasPagamento.Credito, 25), (FormasPagamento.Boleto, 10), (FormasPagamento.Crediario, 15)
                }, x => x.Item2).Item1;
                if (!FormasPagamento.EhAVista(forma) && total >= 100)
                    parcelas = forma switch
                    {
                        FormasPagamento.Credito => _rnd.Next(1, 4),
                        FormasPagamento.Boleto => _rnd.Next(1, 3),
                        _ => _rnd.Next(2, 5)
                    };
            }
            int? vendedor = _vendedores.Count > 0 ? Ponderado(_vendedores, v => v.Peso).Id : null;

            int vendaId = Inserir(@"INSERT INTO vendas (cliente_id, usuario_id, data_venda, status, subtotal, desconto, total, forma_pagamento, parcelas, observacoes)
                VALUES (@cliente, @vendedor, @quando, @status, @subtotal, @desconto, @total, @forma, @parcelas, @obs)",
                new
                {
                    cliente = cliente?.Id, vendedor, quando, status, subtotal, desconto, total, forma, parcelas,
                    obs = status == StatusVenda.Orcamento ? "Orçamento enviado por e-mail ao cliente." : ""
                });
            foreach (var i in itens)
                Exec(@"INSERT INTO venda_itens (venda_id, produto_id, quantidade, preco_unitario, desconto, total, custo_unitario)
                    VALUES (@vendaId, @id, @qtd, @preco, 0, @total, @custo)", new { vendaId, id = i.p.Id, i.qtd, i.preco, i.total, i.custo });

            if (status != StatusVenda.Finalizada) { _qtdOrcamentos++; return; }
            _qtdVendas++;
            _faturamento += total;

            foreach (var i in itens)
                Movimentar(i.p, TipoMov.Saida, i.qtd, i.p.Custo, "VENDA", vendaId, $"Venda nº {vendaId}", vendedor, quando);

            if (total <= 0) return;
            string desc = $"Venda nº {vendaId}";
            if (FormasPagamento.EhAVista(forma))
            {
                var t = Parcelas("contas_receber", "cliente_id", "venda_id", desc, cliente?.Id, vendaId, total, 1, quando, _ => quando, forma)[0];
                Quitar(true, t.Id, t.Valor, desc, forma, vendedor, quando);
                return;
            }

            var titulos = Parcelas("contas_receber", "cliente_id", "venda_id", desc, cliente?.Id, vendaId, total, parcelas, quando,
                i => quando.Date.AddMonths(i), forma);
            foreach (var t in titulos)
            {
                if (t.Vencimento >= _hoje) continue;
                DateTime pagamento;
                if (forma == FormasPagamento.Credito)
                    pagamento = t.Vencimento.AddHours(8).AddMinutes(_rnd.Next(0, 60)); // repasse da operadora
                else
                {
                    if (Sorteio() < 0.18) continue; // cliente em atraso
                    pagamento = t.Vencimento.AddDays(_rnd.Next(-3, 4)).AddHours(10).AddMinutes(_rnd.Next(0, 420));
                    if (pagamento.Date < quando.Date) pagamento = quando.Date.AddDays(1).AddHours(10);
                    if (pagamento.Date >= _hoje) continue;
                }
                string descCaixa = "Recebimento: " + desc + (parcelas > 1 ? $" ({t.Parcela}/{parcelas})" : "");
                var q = pagamento;
                Agendar(q, () => Quitar(true, t.Id, t.Valor, descCaixa, forma, _gerenteId, q));
            }
        }

        /// <summary>Deixa cerca de 5 produtos populares abaixo do mínimo e o restante acima dele.</summary>
        private void AjustarEstoqueMinimo()
        {
            foreach (var p in _produtos)
            {
                if (_escassos.Contains(p))
                {
                    if (p.Estoque > p.Minimo) p.Minimo = p.Estoque + _rnd.Next(2, 6);
                }
                else if (p.Estoque <= p.Minimo && p.Estoque >= 2)
                    p.Minimo = Math.Max(1, Math.Floor(p.Estoque / 2));
            }
        }

        // ---------- conferência ----------

        private void Falha(string msg) => throw new InvalidOperationException("Dados de demonstração inconsistentes: " + msg);

        private void Contar0(string descricao, string sql, object? p = null)
        {
            long n = Escalar<long>(sql, p);
            if (n != 0) Falha($"{descricao} ({n} ocorrência(s)).");
        }

        private void Verificar()
        {
            var ids = _produtos.Select(p => p.Id).ToList();
            var a = new { ids, v = _antesVenda, r = _antesReceber, pg = _antesPagar, cx = _antesCaixa, m = _antesMov };

            // estoque: saldo do produto = último saldo_posterior = Σ entradas − Σ saídas, nunca negativo, cadeia de saldos coerente
            Contar0("estoque diferente do histórico", @"
                SELECT COUNT(*) FROM produtos p WHERE p.id IN @ids AND (
                    p.estoque < 0
                    OR ABS(p.estoque - COALESCE((SELECT m.saldo_posterior FROM movimentacoes_estoque m WHERE m.produto_id = p.id ORDER BY m.id DESC LIMIT 1), 0)) > 0.0001
                    OR ABS(p.estoque - COALESCE((SELECT SUM(CASE WHEN m.tipo = 'ENTRADA' THEN m.quantidade WHEN m.tipo = 'SAIDA' THEN -m.quantidade ELSE 0 END)
                                                 FROM movimentacoes_estoque m WHERE m.produto_id = p.id), 0)) > 0.0001)", a);
            Contar0("cadeia de saldos de estoque quebrada", @"
                SELECT COUNT(*) FROM movimentacoes_estoque m WHERE m.produto_id IN @ids AND (
                    m.saldo_posterior < 0
                    OR ABS(m.saldo_posterior - (m.saldo_anterior + CASE WHEN m.tipo = 'ENTRADA' THEN m.quantidade ELSE -m.quantidade END)) > 0.0001
                    OR ABS(m.saldo_anterior - COALESCE((SELECT m2.saldo_posterior FROM movimentacoes_estoque m2
                                                        WHERE m2.produto_id = m.produto_id AND m2.id < m.id ORDER BY m2.id DESC LIMIT 1), 0)) > 0.0001)", a);
            Contar0("movimentações fora de ordem cronológica", @"
                SELECT COUNT(*) FROM movimentacoes_estoque m WHERE m.id > @m AND
                    (SELECT m2.data_movimento FROM movimentacoes_estoque m2 WHERE m2.id > @m AND m2.id < m.id ORDER BY m2.id DESC LIMIT 1) > m.data_movimento", a);

            // vendas
            Contar0("total da venda diferente de subtotal − desconto",
                "SELECT COUNT(*) FROM vendas WHERE id > @v AND ABS(total - (subtotal - desconto)) > 0.005", a);
            Contar0("subtotal da venda diferente da soma dos itens",
                "SELECT COUNT(*) FROM vendas v WHERE v.id > @v AND ABS(v.subtotal - COALESCE((SELECT SUM(i.total) FROM venda_itens i WHERE i.venda_id = v.id), 0)) > 0.005", a);
            Contar0("títulos da venda não fecham o total", @"
                SELECT COUNT(*) FROM vendas v WHERE v.id > @v AND v.status = 'FINALIZADA' AND v.total > 0
                  AND ABS(v.total - COALESCE((SELECT SUM(t.valor) FROM contas_receber t WHERE t.venda_id = v.id AND t.status <> 'CANCELADO'), 0)) > 0.005", a);
            Contar0("orçamento com títulos ou movimentações", @"
                SELECT COUNT(*) FROM vendas v WHERE v.id > @v AND v.status <> 'FINALIZADA'
                  AND (EXISTS (SELECT 1 FROM contas_receber t WHERE t.venda_id = v.id)
                       OR EXISTS (SELECT 1 FROM movimentacoes_estoque m WHERE m.origem = 'VENDA' AND m.origem_id = v.id))", a);

            // títulos
            foreach (var (tabela, antes) in new[] { ("contas_receber", "@r"), ("contas_pagar", "@pg") })
            {
                Contar0($"valor pago maior que o valor ({tabela})", $"SELECT COUNT(*) FROM {tabela} WHERE id > {antes} AND valor_pago > valor + 0.005", a);
                Contar0($"status incoerente ({tabela})", $@"SELECT COUNT(*) FROM {tabela} WHERE id > {antes} AND (
                    (status = 'PAGO' AND (ABS(valor_pago - valor) > 0.005 OR data_pagamento IS NULL))
                    OR (status = 'ABERTO' AND (valor_pago >= valor - 0.005 OR data_pagamento IS NOT NULL))
                    OR status NOT IN ('ABERTO', 'PAGO'))", a);
            }

            // caixa: todo pagamento tem lançamento e não há outros lançamentos
            Contar0("recebimento sem lançamento de caixa (ou duplicado)", @"
                SELECT COUNT(*) FROM contas_receber t WHERE t.id > @r AND t.valor_pago > 0 AND
                    (SELECT COUNT(*) FROM caixa_lancamentos l WHERE l.id > @cx AND l.origem = 'RECEBIMENTO' AND l.origem_id = t.id AND l.tipo = 'ENTRADA'
                       AND ABS(l.valor - t.valor_pago) < 0.005) <> 1", a);
            Contar0("pagamento sem lançamento de caixa (ou duplicado)", @"
                SELECT COUNT(*) FROM contas_pagar t WHERE t.id > @pg AND t.valor_pago > 0 AND
                    (SELECT COUNT(*) FROM caixa_lancamentos l WHERE l.id > @cx AND l.origem = 'PAGAMENTO' AND l.origem_id = t.id AND l.tipo = 'SAIDA'
                       AND ABS(l.valor - t.valor_pago) < 0.005) <> 1", a);
            Contar0("lançamento de caixa sem título correspondente", @"
                SELECT COUNT(*) FROM caixa_lancamentos l WHERE l.id > @cx AND NOT (
                    (l.origem = 'RECEBIMENTO' AND l.tipo = 'ENTRADA' AND EXISTS (SELECT 1 FROM contas_receber t WHERE t.id = l.origem_id AND t.id > @r))
                    OR (l.origem = 'PAGAMENTO' AND l.tipo = 'SAIDA' AND EXISTS (SELECT 1 FROM contas_pagar t WHERE t.id = l.origem_id AND t.id > @pg)))", a);
            decimal caixa = Escalar<decimal>("SELECT COALESCE(SUM(CASE WHEN tipo = 'ENTRADA' THEN valor ELSE -valor END), 0) FROM caixa_lancamentos WHERE id > @cx", a);
            decimal recebido = Escalar<decimal>("SELECT COALESCE(SUM(valor_pago), 0) FROM contas_receber WHERE id > @r", a);
            decimal pago = Escalar<decimal>("SELECT COALESCE(SUM(valor_pago), 0) FROM contas_pagar WHERE id > @pg", a);
            if (Math.Abs(caixa - (recebido - pago)) > 0.01m)
                Falha($"saldo do caixa ({caixa}) diferente de recebido − pago ({recebido - pago}).");
        }
    }
}
