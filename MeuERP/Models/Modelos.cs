namespace MeuERP.Models;

// ---------- Constantes de domínio ----------

public static class Perfis
{
    public const string Admin = "ADMIN";
    public const string Gerente = "GERENTE";
    public const string Operador = "OPERADOR";
    public static readonly string[] Todos = { Admin, Gerente, Operador };
}

public static class TipoMov
{
    public const string Entrada = "ENTRADA";
    public const string Saida = "SAIDA";
    /// <summary>Ajuste de inventário: a quantidade informada passa a ser o novo saldo.</summary>
    public const string Ajuste = "AJUSTE";
}

public static class StatusVenda
{
    public const string Orcamento = "ORCAMENTO";
    public const string Finalizada = "FINALIZADA";
    public const string Cancelada = "CANCELADA";
}

public static class StatusCompra
{
    public const string Pedido = "PEDIDO";
    public const string Recebida = "RECEBIDA";
    public const string Cancelada = "CANCELADA";
}

public static class StatusTitulo
{
    public const string Aberto = "ABERTO";
    public const string Pago = "PAGO";
    public const string Cancelado = "CANCELADO";
}

public static class TipoCaixa
{
    public const string Entrada = "ENTRADA";
    public const string Saida = "SAIDA";
}

public static class FormasPagamento
{
    public const string Dinheiro = "Dinheiro";
    public const string Pix = "PIX";
    public const string Debito = "Cartão de Débito";
    public const string Credito = "Cartão de Crédito";
    public const string Boleto = "Boleto";
    public const string Transferencia = "Transferência";
    public const string Crediario = "Crediário";
    public static readonly string[] Todas = { Dinheiro, Pix, Debito, Credito, Boleto, Transferencia, Crediario };
    /// <summary>Formas em que o dinheiro entra na hora (título já nasce quitado).</summary>
    public static bool EhAVista(string forma) => forma is Dinheiro or Pix or Debito or Transferencia;
}

public static class Unidades
{
    public static readonly string[] Todas = { "UN", "PC", "CX", "KG", "G", "L", "ML", "M", "M2", "PAR", "DZ", "HR" };
}

public static class Ufs
{
    public static readonly string[] Todas =
    {
        "AC","AL","AP","AM","BA","CE","DF","ES","GO","MA","MT","MS","MG","PA","PB","PR","PE","PI",
        "RJ","RN","RS","RO","RR","SC","SP","SE","TO"
    };
}

// ---------- Entidades ----------

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Login { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public string Perfil { get; set; } = Perfis.Operador;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}

public class Empresa
{
    public int Id { get; set; } = 1;
    public string RazaoSocial { get; set; } = "";
    public string NomeFantasia { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public string InscricaoEstadual { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Cep { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
}

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public class Produto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string CodigoBarras { get; set; } = "";
    public string Descricao { get; set; } = "";
    public int? CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = "";
    public string Unidade { get; set; } = "UN";
    public decimal PrecoCusto { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal Estoque { get; set; }
    public decimal EstoqueMinimo { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }

    public bool EstoqueBaixo => EstoqueMinimo > 0 && Estoque <= EstoqueMinimo;
    /// <summary>Margem sobre o preço de venda, em %.</summary>
    public decimal Margem => PrecoVenda > 0 ? Math.Round((PrecoVenda - PrecoCusto) / PrecoVenda * 100, 1) : 0;
    public decimal ValorEmEstoque => Math.Round(Estoque * PrecoCusto, 2);
}

public abstract class Pessoa
{
    public int Id { get; set; }
    /// <summary>"F" (física) ou "J" (jurídica).</summary>
    public string TipoPessoa { get; set; } = "F";
    public string Nome { get; set; } = "";
    public string NomeFantasia { get; set; } = "";
    /// <summary>CPF ou CNPJ, somente dígitos.</summary>
    public string Documento { get; set; } = "";
    public string InscricaoRg { get; set; } = "";
    public string Telefone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Cep { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Bairro { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Observacoes { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }

    public string DocumentoFormatado => Nucleo.Formato.Documento(Documento);
    public string CidadeUf => string.IsNullOrEmpty(Cidade) ? Uf : (string.IsNullOrEmpty(Uf) ? Cidade : $"{Cidade}/{Uf}");
}

public class Cliente : Pessoa
{
    public decimal LimiteCredito { get; set; }
}

public class Fornecedor : Pessoa
{
    public string Contato { get; set; } = "";
}

public class MovimentacaoEstoque
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoCodigo { get; set; } = "";
    public string ProdutoDescricao { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal Quantidade { get; set; }
    public decimal SaldoAnterior { get; set; }
    public decimal SaldoPosterior { get; set; }
    public decimal CustoUnitario { get; set; }
    /// <summary>MANUAL, VENDA, COMPRA, CANCELAMENTO_VENDA, CANCELAMENTO_COMPRA.</summary>
    public string Origem { get; set; } = "";
    public int? OrigemId { get; set; }
    public string Observacao { get; set; } = "";
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = "";
    public DateTime DataMovimento { get; set; }
}

public class Venda
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string ClienteNome { get; set; } = "";
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = "";
    public DateTime DataVenda { get; set; } = DateTime.Now;
    public string Status { get; set; } = StatusVenda.Orcamento;
    public decimal Subtotal { get; set; }
    public decimal Desconto { get; set; }
    public decimal Total { get; set; }
    public string FormaPagamento { get; set; } = FormasPagamento.Dinheiro;
    public int Parcelas { get; set; } = 1;
    public string Observacoes { get; set; } = "";
    public List<VendaItem> Itens { get; set; } = new();
}

public class VendaItem
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoCodigo { get; set; } = "";
    public string ProdutoDescricao { get; set; } = "";
    public string Unidade { get; set; } = "";
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Desconto { get; set; }
    public decimal Total { get; set; }
    public decimal CustoUnitario { get; set; }
}

public class Compra
{
    public int Id { get; set; }
    public int FornecedorId { get; set; }
    public string FornecedorNome { get; set; } = "";
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = "";
    public DateTime DataCompra { get; set; } = DateTime.Now;
    public DateTime? DataRecebimento { get; set; }
    public string Status { get; set; } = StatusCompra.Pedido;
    public string NumeroNf { get; set; } = "";
    public decimal Subtotal { get; set; }
    public decimal Desconto { get; set; }
    public decimal Frete { get; set; }
    public decimal Total { get; set; }
    public string FormaPagamento { get; set; } = FormasPagamento.Boleto;
    public int Parcelas { get; set; } = 1;
    public string Observacoes { get; set; } = "";
    public List<CompraItem> Itens { get; set; } = new();
}

public class CompraItem
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public int ProdutoId { get; set; }
    public string ProdutoCodigo { get; set; } = "";
    public string ProdutoDescricao { get; set; } = "";
    public string Unidade { get; set; } = "";
    public decimal Quantidade { get; set; }
    public decimal CustoUnitario { get; set; }
    public decimal Total { get; set; }
}

/// <summary>Título financeiro: conta a receber (pessoa = cliente, origem = venda) ou a pagar (pessoa = fornecedor, origem = compra).</summary>
public class Titulo
{
    public int Id { get; set; }
    public string Descricao { get; set; } = "";
    public int? PessoaId { get; set; }
    public string PessoaNome { get; set; } = "";
    public int? OrigemId { get; set; }
    public int Parcela { get; set; } = 1;
    public int TotalParcelas { get; set; } = 1;
    public decimal Valor { get; set; }
    public decimal ValorPago { get; set; }
    public DateTime DataEmissao { get; set; } = DateTime.Today;
    public DateTime DataVencimento { get; set; } = DateTime.Today;
    public DateTime? DataPagamento { get; set; }
    public string FormaPagamento { get; set; } = "";
    public string Status { get; set; } = StatusTitulo.Aberto;
    public string Observacoes { get; set; } = "";

    public decimal Saldo => Status == StatusTitulo.Aberto ? Math.Max(0, Valor - ValorPago) : 0;
    public bool Vencido => Status == StatusTitulo.Aberto && DataVencimento.Date < DateTime.Today;
    public string ParcelaTexto => $"{Parcela}/{TotalParcelas}";
    /// <summary>ABERTO, VENCIDO, PAGO ou CANCELADO.</summary>
    public string Situacao => Vencido ? "VENCIDO" : Status;
}

public class LancamentoCaixa
{
    public int Id { get; set; }
    public DateTime Data { get; set; }
    public string Tipo { get; set; } = TipoCaixa.Entrada;
    public string Descricao { get; set; } = "";
    public decimal Valor { get; set; }
    public string FormaPagamento { get; set; } = "";
    /// <summary>MANUAL, RECEBIMENTO, PAGAMENTO ou ESTORNO.</summary>
    public string Origem { get; set; } = "MANUAL";
    public int? OrigemId { get; set; }
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = "";

    public decimal ValorComSinal => Tipo == TipoCaixa.Entrada ? Valor : -Valor;
}
