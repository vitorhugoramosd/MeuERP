using Dapper;
using MeuERP.Models;
using MeuERP.Nucleo;

namespace MeuERP.Dados;

public static class UsuarioRepository
{
    public static Usuario? Autenticar(string login, string senha)
    {
        using var c = Database.Abrir();
        var u = c.QueryFirstOrDefault<Usuario>("SELECT * FROM usuarios WHERE login = @login AND ativo = 1", new { login = login.Trim() });
        return u != null && Seguranca.Verificar(senha, u.SenhaHash) ? u : null;
    }

    public static List<Usuario> Listar(string? busca = null)
    {
        using var c = Database.Abrir();
        return c.Query<Usuario>(
            "SELECT * FROM usuarios WHERE (@b = '' OR nome LIKE @l OR login LIKE @l) ORDER BY nome",
            new { b = busca?.Trim() ?? "", l = $"%{busca?.Trim()}%" }).ToList();
    }

    public static Usuario? Obter(int id)
    {
        using var c = Database.Abrir();
        return c.QueryFirstOrDefault<Usuario>("SELECT * FROM usuarios WHERE id = @id", new { id });
    }

    /// <summary>Insere ou atualiza. <paramref name="novaSenha"/> é obrigatória na inclusão e opcional na edição.</summary>
    public static void Salvar(Usuario u, string? novaSenha)
    {
        u.Nome = u.Nome.Trim();
        u.Login = u.Login.Trim();
        if (u.Nome.Length == 0) throw new RegraException("Informe o nome do usuário.");
        if (u.Login.Length < 3) throw new RegraException("O login deve ter pelo menos 3 caracteres.");
        if (!Perfis.Todos.Contains(u.Perfil)) throw new RegraException("Perfil inválido.");
        if (u.Id == 0 && string.IsNullOrEmpty(novaSenha)) throw new RegraException("Informe a senha do novo usuário.");
        if (!string.IsNullOrEmpty(novaSenha))
        {
            if (novaSenha.Length < 4) throw new RegraException("A senha deve ter pelo menos 4 caracteres.");
            u.SenhaHash = Seguranca.GerarHash(novaSenha);
        }

        using var c = Database.Abrir();
        if (u.Id != 0 && (!u.Ativo || u.Perfil != Perfis.Admin))
        {
            long outrosAdmins = c.ExecuteScalar<long>("SELECT COUNT(*) FROM usuarios WHERE perfil = 'ADMIN' AND ativo = 1 AND id <> @Id", u);
            if (outrosAdmins == 0) throw new RegraException("É preciso manter pelo menos um administrador ativo.");
        }
        try
        {
            if (u.Id == 0)
            {
                u.CriadoEm = DateTime.Now;
                u.Id = c.ExecuteScalar<int>(@"INSERT INTO usuarios (nome, login, senha_hash, perfil, ativo, criado_em)
                    VALUES (@Nome, @Login, @SenhaHash, @Perfil, @Ativo, @CriadoEm); SELECT last_insert_rowid();", u);
            }
            else if (!string.IsNullOrEmpty(novaSenha))
                c.Execute("UPDATE usuarios SET nome=@Nome, login=@Login, senha_hash=@SenhaHash, perfil=@Perfil, ativo=@Ativo WHERE id=@Id", u);
            else
                c.Execute("UPDATE usuarios SET nome=@Nome, login=@Login, perfil=@Perfil, ativo=@Ativo WHERE id=@Id", u);
        }
        catch (Exception ex) when (Database.EhDuplicado(ex))
        {
            throw new RegraException($"Já existe um usuário com o login \"{u.Login}\".");
        }
    }

    public static void AlterarSenha(int id, string senhaAtual, string novaSenha)
    {
        var u = Obter(id) ?? throw new RegraException("Usuário não encontrado.");
        if (!Seguranca.Verificar(senhaAtual, u.SenhaHash)) throw new RegraException("A senha atual não confere.");
        if (novaSenha.Length < 4) throw new RegraException("A nova senha deve ter pelo menos 4 caracteres.");
        using var c = Database.Abrir();
        c.Execute("UPDATE usuarios SET senha_hash = @h WHERE id = @id", new { id, h = Seguranca.GerarHash(novaSenha) });
    }

    public static void Excluir(int id)
    {
        if (id == Sessao.UsuarioId) throw new RegraException("Você não pode excluir o próprio usuário.");
        using var c = Database.Abrir();
        long outrosAdmins = c.ExecuteScalar<long>("SELECT COUNT(*) FROM usuarios WHERE perfil = 'ADMIN' AND ativo = 1 AND id <> @id", new { id });
        if (outrosAdmins == 0) throw new RegraException("É preciso manter pelo menos um administrador ativo.");
        try { c.Execute("DELETE FROM usuarios WHERE id = @id", new { id }); }
        catch (Exception ex) when (Database.EhRegistroEmUso(ex))
        {
            throw new RegraException("Este usuário possui lançamentos no sistema e não pode ser excluído. Desative-o em vez de excluir.");
        }
    }
}

public static class EmpresaRepository
{
    public static Empresa Obter()
    {
        using var c = Database.Abrir();
        return c.QueryFirstOrDefault<Empresa>("SELECT * FROM empresa WHERE id = 1") ?? new Empresa();
    }

    public static void Salvar(Empresa e)
    {
        if (string.IsNullOrWhiteSpace(e.RazaoSocial)) throw new RegraException("Informe a razão social.");
        if (Formato.SoDigitos(e.Cnpj).Length > 0 && !Formato.CnpjValido(e.Cnpj)) throw new RegraException("CNPJ inválido.");
        using var c = Database.Abrir();
        c.Execute(@"UPDATE empresa SET razao_social=@RazaoSocial, nome_fantasia=@NomeFantasia, cnpj=@Cnpj,
            inscricao_estadual=@InscricaoEstadual, telefone=@Telefone, email=@Email, cep=@Cep, endereco=@Endereco,
            cidade=@Cidade, uf=@Uf WHERE id = 1", e);
    }
}

public static class CategoriaRepository
{
    public static List<Categoria> Listar(string? busca = null, bool somenteAtivos = false)
    {
        using var c = Database.Abrir();
        return c.Query<Categoria>(
            "SELECT * FROM categorias WHERE (@b = '' OR nome LIKE @l) AND (@at = 0 OR ativo = 1) ORDER BY nome",
            new { b = busca?.Trim() ?? "", l = $"%{busca?.Trim()}%", at = somenteAtivos }).ToList();
    }

    public static void Salvar(Categoria cat)
    {
        cat.Nome = cat.Nome.Trim();
        if (cat.Nome.Length == 0) throw new RegraException("Informe o nome da categoria.");
        using var c = Database.Abrir();
        try
        {
            if (cat.Id == 0)
                cat.Id = c.ExecuteScalar<int>("INSERT INTO categorias (nome, ativo) VALUES (@Nome, @Ativo); SELECT last_insert_rowid();", cat);
            else
                c.Execute("UPDATE categorias SET nome=@Nome, ativo=@Ativo WHERE id=@Id", cat);
        }
        catch (Exception ex) when (Database.EhDuplicado(ex))
        {
            throw new RegraException($"Já existe uma categoria chamada \"{cat.Nome}\".");
        }
    }

    public static void Excluir(int id)
    {
        using var c = Database.Abrir();
        try { c.Execute("DELETE FROM categorias WHERE id = @id", new { id }); }
        catch (Exception ex) when (Database.EhRegistroEmUso(ex))
        {
            throw new RegraException("Há produtos nesta categoria. Mova os produtos ou desative a categoria.");
        }
    }
}

public static class ProdutoRepository
{
    private const string Select = @"SELECT p.*, COALESCE(c.nome, '') AS categoria_nome
        FROM produtos p LEFT JOIN categorias c ON c.id = p.categoria_id ";

    public static List<Produto> Listar(string? busca = null, bool somenteAtivos = false, int? categoriaId = null, bool somenteEstoqueBaixo = false)
    {
        using var c = Database.Abrir();
        return c.Query<Produto>(Select + @"
            WHERE (@b = '' OR p.codigo LIKE @l OR p.descricao LIKE @l OR p.codigo_barras = @b)
              AND (@at = 0 OR p.ativo = 1)
              AND (@cat IS NULL OR p.categoria_id = @cat)
              AND (@baixo = 0 OR (p.estoque_minimo > 0 AND p.estoque <= p.estoque_minimo))
            ORDER BY p.descricao",
            new { b = busca?.Trim() ?? "", l = $"%{busca?.Trim()}%", at = somenteAtivos, cat = categoriaId, baixo = somenteEstoqueBaixo }).ToList();
    }

    public static Produto? Obter(int id)
    {
        using var c = Database.Abrir();
        return c.QueryFirstOrDefault<Produto>(Select + "WHERE p.id = @id", new { id });
    }

    /// <summary>Busca exata por código interno ou código de barras (somente ativos).</summary>
    public static Produto? ObterPorCodigo(string codigo)
    {
        codigo = codigo.Trim();
        if (codigo.Length == 0) return null;
        using var c = Database.Abrir();
        return c.QueryFirstOrDefault<Produto>(Select + "WHERE p.ativo = 1 AND (p.codigo = @codigo OR p.codigo_barras = @codigo)", new { codigo });
    }

    public static string ProximoCodigo()
    {
        using var c = Database.Abrir();
        long n = c.ExecuteScalar<long>("SELECT COALESCE(MAX(id), 0) + 1 FROM produtos");
        string cod;
        do { cod = n.ToString("D4"); n++; }
        while (c.ExecuteScalar<long>("SELECT COUNT(*) FROM produtos WHERE codigo = @cod", new { cod }) > 0);
        return cod;
    }

    /// <summary>Insere ou atualiza o cadastro. O saldo de estoque NÃO é alterado aqui (use EstoqueService).</summary>
    public static void Salvar(Produto p)
    {
        p.Codigo = p.Codigo.Trim();
        p.Descricao = p.Descricao.Trim();
        p.CodigoBarras = p.CodigoBarras.Trim();
        if (p.Codigo.Length == 0) throw new RegraException("O código do produto é obrigatório.");
        if (p.Descricao.Length == 0) throw new RegraException("A descrição do produto é obrigatória.");
        if (string.IsNullOrWhiteSpace(p.Unidade)) throw new RegraException("A unidade do produto é obrigatória.");
        if (p.PrecoVenda < 0 || p.PrecoCusto < 0) throw new RegraException("Preços não podem ser negativos.");
        if (p.EstoqueMinimo < 0) throw new RegraException("O estoque mínimo não pode ser negativo.");

        using var c = Database.Abrir();
        try
        {
            if (p.Id == 0)
            {
                p.CriadoEm = DateTime.Now;
                p.Id = c.ExecuteScalar<int>(@"INSERT INTO produtos
                    (codigo, codigo_barras, descricao, categoria_id, unidade, preco_custo, preco_venda, estoque_minimo, ativo, criado_em)
                    VALUES (@Codigo, @CodigoBarras, @Descricao, @CategoriaId, @Unidade, @PrecoCusto, @PrecoVenda, @EstoqueMinimo, @Ativo, @CriadoEm);
                    SELECT last_insert_rowid();", p);
            }
            else
            {
                c.Execute(@"UPDATE produtos SET codigo=@Codigo, codigo_barras=@CodigoBarras, descricao=@Descricao,
                    categoria_id=@CategoriaId, unidade=@Unidade, preco_custo=@PrecoCusto, preco_venda=@PrecoVenda,
                    estoque_minimo=@EstoqueMinimo, ativo=@Ativo WHERE id=@Id", p);
            }
        }
        catch (Exception ex) when (Database.EhDuplicado(ex))
        {
            throw new RegraException($"Já existe um produto com o código \"{p.Codigo}\".");
        }
    }

    public static void Excluir(int id)
    {
        using var c = Database.Abrir();
        try { c.Execute("DELETE FROM produtos WHERE id = @id", new { id }); }
        catch (Exception ex) when (Database.EhRegistroEmUso(ex))
        {
            throw new RegraException("Este produto possui movimentações, vendas ou compras e não pode ser excluído. Desative-o em vez de excluir.");
        }
    }
}

internal static class PessoaDados
{
    public static List<T> Listar<T>(string tabela, string? busca, bool somenteAtivos) where T : Pessoa
    {
        using var c = Database.Abrir();
        string b = busca?.Trim() ?? "";
        string dig = Formato.SoDigitos(b);
        return c.Query<T>($@"SELECT * FROM {tabela}
            WHERE (@b = '' OR nome LIKE @l OR nome_fantasia LIKE @l OR email LIKE @l OR cidade LIKE @l OR (@dig <> '' AND documento LIKE @ld))
              AND (@at = 0 OR ativo = 1)
            ORDER BY nome",
            new { b, l = $"%{b}%", dig, ld = $"%{dig}%", at = somenteAtivos }).ToList();
    }

    public static T? Obter<T>(string tabela, int id) where T : Pessoa
    {
        using var c = Database.Abrir();
        return c.QueryFirstOrDefault<T>($"SELECT * FROM {tabela} WHERE id = @id", new { id });
    }

    public static void Salvar<T>(string tabela, T p, string colunaExtra, string propExtra) where T : Pessoa
    {
        p.Nome = p.Nome.Trim();
        p.Documento = Formato.SoDigitos(p.Documento);
        if (p.Nome.Length == 0) throw new RegraException("Informe o nome / razão social.");
        if (p.TipoPessoa is not ("F" or "J")) throw new RegraException("Tipo de pessoa inválido.");
        if (p.Documento.Length > 0)
        {
            if (p.TipoPessoa == "F" && !Formato.CpfValido(p.Documento)) throw new RegraException("CPF inválido.");
            if (p.TipoPessoa == "J" && !Formato.CnpjValido(p.Documento)) throw new RegraException("CNPJ inválido.");
        }
        if (p.Email.Trim().Length > 0 && !p.Email.Contains('@')) throw new RegraException("E-mail inválido.");

        using var c = Database.Abrir();
        if (p.Documento.Length > 0 &&
            c.ExecuteScalar<long>($"SELECT COUNT(*) FROM {tabela} WHERE documento = @Documento AND id <> @Id", p) > 0)
            throw new RegraException("Já existe um cadastro com este CPF/CNPJ.");

        if (p.Id == 0)
        {
            p.CriadoEm = DateTime.Now;
            p.Id = c.ExecuteScalar<int>($@"INSERT INTO {tabela}
                (tipo_pessoa, nome, nome_fantasia, documento, inscricao_rg, telefone, email, cep, endereco, numero, bairro, cidade, uf, observacoes, {colunaExtra}, ativo, criado_em)
                VALUES (@TipoPessoa, @Nome, @NomeFantasia, @Documento, @InscricaoRg, @Telefone, @Email, @Cep, @Endereco, @Numero, @Bairro, @Cidade, @Uf, @Observacoes, @{propExtra}, @Ativo, @CriadoEm);
                SELECT last_insert_rowid();", p);
        }
        else
        {
            c.Execute($@"UPDATE {tabela} SET tipo_pessoa=@TipoPessoa, nome=@Nome, nome_fantasia=@NomeFantasia, documento=@Documento,
                inscricao_rg=@InscricaoRg, telefone=@Telefone, email=@Email, cep=@Cep, endereco=@Endereco, numero=@Numero,
                bairro=@Bairro, cidade=@Cidade, uf=@Uf, observacoes=@Observacoes, {colunaExtra}=@{propExtra}, ativo=@Ativo WHERE id=@Id", p);
        }
    }

    public static void Excluir(string tabela, int id, string mensagemEmUso)
    {
        using var c = Database.Abrir();
        try { c.Execute($"DELETE FROM {tabela} WHERE id = @id", new { id }); }
        catch (Exception ex) when (Database.EhRegistroEmUso(ex)) { throw new RegraException(mensagemEmUso); }
    }
}

public static class ClienteRepository
{
    public static List<Cliente> Listar(string? busca = null, bool somenteAtivos = false) => PessoaDados.Listar<Cliente>("clientes", busca, somenteAtivos);
    public static Cliente? Obter(int id) => PessoaDados.Obter<Cliente>("clientes", id);
    public static void Salvar(Cliente c)
    {
        if (c.LimiteCredito < 0) throw new RegraException("O limite de crédito não pode ser negativo.");
        PessoaDados.Salvar("clientes", c, "limite_credito", nameof(Cliente.LimiteCredito));
    }
    public static void Excluir(int id) => PessoaDados.Excluir("clientes", id,
        "Este cliente possui vendas ou títulos e não pode ser excluído. Desative-o em vez de excluir.");

    /// <summary>Total em aberto (a receber) do cliente.</summary>
    public static decimal SaldoDevedor(int clienteId)
    {
        using var c = Database.Abrir();
        return c.ExecuteScalar<decimal>("SELECT COALESCE(SUM(valor - valor_pago), 0) FROM contas_receber WHERE cliente_id = @clienteId AND status = 'ABERTO'", new { clienteId });
    }
}

public static class FornecedorRepository
{
    public static List<Fornecedor> Listar(string? busca = null, bool somenteAtivos = false) => PessoaDados.Listar<Fornecedor>("fornecedores", busca, somenteAtivos);
    public static Fornecedor? Obter(int id) => PessoaDados.Obter<Fornecedor>("fornecedores", id);
    public static void Salvar(Fornecedor f) => PessoaDados.Salvar("fornecedores", f, "contato", nameof(Fornecedor.Contato));
    public static void Excluir(int id) => PessoaDados.Excluir("fornecedores", id,
        "Este fornecedor possui compras ou títulos e não pode ser excluído. Desative-o em vez de excluir.");
}
