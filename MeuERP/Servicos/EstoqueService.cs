using System.Data;
using Dapper;
using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;

namespace MeuERP.Servicos;

/// <summary>Única porta de entrada para alterar saldo de estoque: toda alteração gera um registro no histórico.</summary>
public static class EstoqueService
{
    /// <summary>
    /// Movimenta o estoque dentro de uma transação existente.
    /// ENTRADA soma, SAIDA subtrai (bloqueia saldo negativo), AJUSTE define o saldo como <paramref name="quantidade"/>.
    /// Se <paramref name="custoUnitario"/> for informado em uma ENTRADA, o custo do produto é atualizado (custo médio ponderado).
    /// </summary>
    public static void Movimentar(IDbConnection c, IDbTransaction? tx, int produtoId, string tipo, decimal quantidade,
        string origem, int? origemId = null, string? observacao = null, decimal? custoUnitario = null)
    {
        var p = c.QueryFirstOrDefault<Produto>("SELECT * FROM produtos WHERE id = @produtoId", new { produtoId }, tx)
                ?? throw new RegraException("Produto não encontrado.");

        decimal anterior = p.Estoque, posterior;
        switch (tipo)
        {
            case TipoMov.Entrada:
                if (quantidade <= 0) throw new RegraException("A quantidade deve ser maior que zero.");
                posterior = anterior + quantidade;
                break;
            case TipoMov.Saida:
                if (quantidade <= 0) throw new RegraException("A quantidade deve ser maior que zero.");
                if (quantidade > anterior)
                    throw new RegraException($"Estoque insuficiente para \"{p.Descricao}\". Saldo atual: {Formato.Qtd(anterior)} {p.Unidade}.");
                posterior = anterior - quantidade;
                break;
            case TipoMov.Ajuste:
                if (quantidade < 0) throw new RegraException("O novo saldo não pode ser negativo.");
                posterior = quantidade;
                quantidade = Math.Abs(posterior - anterior);
                break;
            default:
                throw new RegraException("Tipo de movimentação inválido.");
        }

        decimal custo = p.PrecoCusto;
        if (tipo == TipoMov.Entrada && custoUnitario is > 0)
        {
            custo = posterior > 0 && anterior > 0
                ? Math.Round((anterior * p.PrecoCusto + quantidade * custoUnitario.Value) / posterior, 4)
                : custoUnitario.Value;
            c.Execute("UPDATE produtos SET preco_custo = @custo WHERE id = @produtoId", new { custo, produtoId }, tx);
        }

        c.Execute("UPDATE produtos SET estoque = @posterior WHERE id = @produtoId", new { posterior, produtoId }, tx);
        c.Execute(@"INSERT INTO movimentacoes_estoque
            (produto_id, tipo, quantidade, saldo_anterior, saldo_posterior, custo_unitario, origem, origem_id, observacao, usuario_id, data_movimento)
            VALUES (@produtoId, @tipo, @quantidade, @anterior, @posterior, @custoMov, @origem, @origemId, @obs, @usuario, @data)",
            new
            {
                produtoId, tipo, quantidade, anterior, posterior,
                custoMov = custoUnitario ?? p.PrecoCusto,
                origem, origemId, obs = observacao ?? "", usuario = Sessao.UsuarioId, data = DateTime.Now
            }, tx);
    }

    /// <summary>Movimentação manual (entrada, saída ou ajuste de inventário) em transação própria.</summary>
    public static void MovimentarManual(int produtoId, string tipo, decimal quantidade, string? observacao, decimal? custoUnitario = null) =>
        Database.EmTransacao((c, tx) => Movimentar(c, tx, produtoId, tipo, quantidade, "MANUAL", null, observacao, custoUnitario));

    public static List<MovimentacaoEstoque> Historico(DateTime de, DateTime ate, int? produtoId = null, string? tipo = null, string? busca = null)
    {
        using var c = Database.Abrir();
        string b = busca?.Trim() ?? "";
        return c.Query<MovimentacaoEstoque>(@"
            SELECT m.*, p.codigo AS produto_codigo, p.descricao AS produto_descricao, COALESCE(u.nome, '') AS usuario_nome
            FROM movimentacoes_estoque m
            JOIN produtos p ON p.id = m.produto_id
            LEFT JOIN usuarios u ON u.id = m.usuario_id
            WHERE m.data_movimento >= @de AND m.data_movimento < @ate
              AND (@produtoId IS NULL OR m.produto_id = @produtoId)
              AND (@tipo = '' OR m.tipo = @tipo)
              AND (@b = '' OR p.codigo LIKE @l OR p.descricao LIKE @l)
            ORDER BY m.id DESC",
            new { de = de.Date, ate = ate.Date.AddDays(1), produtoId, tipo = tipo ?? "", b, l = $"%{b}%" }).ToList();
    }
}
