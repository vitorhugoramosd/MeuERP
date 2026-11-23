using MeuERP.Models;

namespace MeuERP.Telas.Estoque;

/// <summary>Linha do grid "Posição de estoque".</summary>
public class LinhaPosicao
{
    public Produto Produto { get; }
    public string Codigo => Produto.Codigo;
    public string Descricao => Produto.Descricao;
    public string CategoriaNome => Produto.CategoriaNome;
    public string Unidade => Produto.Unidade;
    public decimal Saldo => Produto.Estoque;
    public decimal EstoqueMinimo => Produto.EstoqueMinimo;
    public decimal PrecoCusto => Produto.PrecoCusto;
    public decimal ValorEmEstoque => Produto.ValorEmEstoque;
    /// <summary>"Zerado", "Baixo" ou "Normal".</summary>
    public string Situacao => Produto.Estoque <= 0 ? "Zerado" : Produto.EstoqueBaixo ? "Baixo" : "Normal";

    public LinhaPosicao(Produto p) { Produto = p; }
}

/// <summary>Linha do grid "Movimentações".</summary>
public class LinhaMovimentacao
{
    public MovimentacaoEstoque Mov { get; }
    public DateTime DataMovimento => Mov.DataMovimento;
    public string Produto => $"{Mov.ProdutoCodigo} - {Mov.ProdutoDescricao}";
    public string Tipo => Mov.Tipo;
    public decimal Quantidade => Mov.Quantidade;
    public decimal SaldoAnterior => Mov.SaldoAnterior;
    public decimal SaldoPosterior => Mov.SaldoPosterior;
    public string Origem => TextoOrigem(Mov.Origem, Mov.OrigemId);
    public string Observacao => Mov.Observacao;
    public string UsuarioNome => Mov.UsuarioNome;

    public LinhaMovimentacao(MovimentacaoEstoque m) { Mov = m; }

    public static string TextoOrigem(string origem, int? id)
    {
        string n = id.HasValue ? $" nº {id}" : "";
        return origem switch
        {
            "MANUAL" => "Manual",
            "VENDA" => "Venda" + n,
            "COMPRA" => "Compra" + n,
            "CANCELAMENTO_VENDA" => "Canc. venda" + n,
            "CANCELAMENTO_COMPRA" => "Canc. compra" + n,
            _ => origem
        };
    }
}
