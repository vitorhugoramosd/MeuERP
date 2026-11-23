namespace MeuERP.Dados;

internal static class Esquema
{
    public const string Sql = @"
CREATE TABLE IF NOT EXISTS usuarios (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nome TEXT NOT NULL,
    login TEXT NOT NULL UNIQUE COLLATE NOCASE,
    senha_hash TEXT NOT NULL,
    perfil TEXT NOT NULL DEFAULT 'OPERADOR',
    ativo INTEGER NOT NULL DEFAULT 1,
    criado_em TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS empresa (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    razao_social TEXT NOT NULL DEFAULT '',
    nome_fantasia TEXT NOT NULL DEFAULT '',
    cnpj TEXT NOT NULL DEFAULT '',
    inscricao_estadual TEXT NOT NULL DEFAULT '',
    telefone TEXT NOT NULL DEFAULT '',
    email TEXT NOT NULL DEFAULT '',
    cep TEXT NOT NULL DEFAULT '',
    endereco TEXT NOT NULL DEFAULT '',
    cidade TEXT NOT NULL DEFAULT '',
    uf TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS categorias (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nome TEXT NOT NULL UNIQUE COLLATE NOCASE,
    ativo INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS produtos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    codigo TEXT NOT NULL UNIQUE COLLATE NOCASE,
    codigo_barras TEXT NOT NULL DEFAULT '',
    descricao TEXT NOT NULL,
    categoria_id INTEGER REFERENCES categorias(id),
    unidade TEXT NOT NULL DEFAULT 'UN',
    preco_custo REAL NOT NULL DEFAULT 0,
    preco_venda REAL NOT NULL DEFAULT 0,
    estoque REAL NOT NULL DEFAULT 0,
    estoque_minimo REAL NOT NULL DEFAULT 0,
    ativo INTEGER NOT NULL DEFAULT 1,
    criado_em TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_produtos_descricao ON produtos(descricao);
CREATE INDEX IF NOT EXISTS idx_produtos_barras ON produtos(codigo_barras);

CREATE TABLE IF NOT EXISTS clientes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tipo_pessoa TEXT NOT NULL DEFAULT 'F',
    nome TEXT NOT NULL,
    nome_fantasia TEXT NOT NULL DEFAULT '',
    documento TEXT NOT NULL DEFAULT '',
    inscricao_rg TEXT NOT NULL DEFAULT '',
    telefone TEXT NOT NULL DEFAULT '',
    email TEXT NOT NULL DEFAULT '',
    cep TEXT NOT NULL DEFAULT '',
    endereco TEXT NOT NULL DEFAULT '',
    numero TEXT NOT NULL DEFAULT '',
    bairro TEXT NOT NULL DEFAULT '',
    cidade TEXT NOT NULL DEFAULT '',
    uf TEXT NOT NULL DEFAULT '',
    observacoes TEXT NOT NULL DEFAULT '',
    limite_credito REAL NOT NULL DEFAULT 0,
    ativo INTEGER NOT NULL DEFAULT 1,
    criado_em TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_clientes_nome ON clientes(nome);

CREATE TABLE IF NOT EXISTS fornecedores (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tipo_pessoa TEXT NOT NULL DEFAULT 'J',
    nome TEXT NOT NULL,
    nome_fantasia TEXT NOT NULL DEFAULT '',
    documento TEXT NOT NULL DEFAULT '',
    inscricao_rg TEXT NOT NULL DEFAULT '',
    telefone TEXT NOT NULL DEFAULT '',
    email TEXT NOT NULL DEFAULT '',
    cep TEXT NOT NULL DEFAULT '',
    endereco TEXT NOT NULL DEFAULT '',
    numero TEXT NOT NULL DEFAULT '',
    bairro TEXT NOT NULL DEFAULT '',
    cidade TEXT NOT NULL DEFAULT '',
    uf TEXT NOT NULL DEFAULT '',
    observacoes TEXT NOT NULL DEFAULT '',
    contato TEXT NOT NULL DEFAULT '',
    ativo INTEGER NOT NULL DEFAULT 1,
    criado_em TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);
CREATE INDEX IF NOT EXISTS idx_fornecedores_nome ON fornecedores(nome);

CREATE TABLE IF NOT EXISTS movimentacoes_estoque (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    produto_id INTEGER NOT NULL REFERENCES produtos(id),
    tipo TEXT NOT NULL,
    quantidade REAL NOT NULL,
    saldo_anterior REAL NOT NULL DEFAULT 0,
    saldo_posterior REAL NOT NULL DEFAULT 0,
    custo_unitario REAL NOT NULL DEFAULT 0,
    origem TEXT NOT NULL DEFAULT 'MANUAL',
    origem_id INTEGER,
    observacao TEXT NOT NULL DEFAULT '',
    usuario_id INTEGER REFERENCES usuarios(id),
    data_movimento TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_mov_produto ON movimentacoes_estoque(produto_id, data_movimento);
CREATE INDEX IF NOT EXISTS idx_mov_data ON movimentacoes_estoque(data_movimento);

CREATE TABLE IF NOT EXISTS vendas (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    cliente_id INTEGER REFERENCES clientes(id),
    usuario_id INTEGER REFERENCES usuarios(id),
    data_venda TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'ORCAMENTO',
    subtotal REAL NOT NULL DEFAULT 0,
    desconto REAL NOT NULL DEFAULT 0,
    total REAL NOT NULL DEFAULT 0,
    forma_pagamento TEXT NOT NULL DEFAULT '',
    parcelas INTEGER NOT NULL DEFAULT 1,
    observacoes TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_vendas_data ON vendas(data_venda);

CREATE TABLE IF NOT EXISTS venda_itens (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    venda_id INTEGER NOT NULL REFERENCES vendas(id) ON DELETE CASCADE,
    produto_id INTEGER NOT NULL REFERENCES produtos(id),
    quantidade REAL NOT NULL,
    preco_unitario REAL NOT NULL,
    desconto REAL NOT NULL DEFAULT 0,
    total REAL NOT NULL,
    custo_unitario REAL NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_venda_itens_venda ON venda_itens(venda_id);
CREATE INDEX IF NOT EXISTS idx_venda_itens_produto ON venda_itens(produto_id);

CREATE TABLE IF NOT EXISTS compras (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    fornecedor_id INTEGER NOT NULL REFERENCES fornecedores(id),
    usuario_id INTEGER REFERENCES usuarios(id),
    data_compra TEXT NOT NULL,
    data_recebimento TEXT,
    status TEXT NOT NULL DEFAULT 'PEDIDO',
    numero_nf TEXT NOT NULL DEFAULT '',
    subtotal REAL NOT NULL DEFAULT 0,
    desconto REAL NOT NULL DEFAULT 0,
    frete REAL NOT NULL DEFAULT 0,
    total REAL NOT NULL DEFAULT 0,
    forma_pagamento TEXT NOT NULL DEFAULT '',
    parcelas INTEGER NOT NULL DEFAULT 1,
    observacoes TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_compras_data ON compras(data_compra);

CREATE TABLE IF NOT EXISTS compra_itens (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    compra_id INTEGER NOT NULL REFERENCES compras(id) ON DELETE CASCADE,
    produto_id INTEGER NOT NULL REFERENCES produtos(id),
    quantidade REAL NOT NULL,
    custo_unitario REAL NOT NULL,
    total REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_compra_itens_compra ON compra_itens(compra_id);

CREATE TABLE IF NOT EXISTS contas_receber (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    descricao TEXT NOT NULL,
    cliente_id INTEGER REFERENCES clientes(id),
    venda_id INTEGER REFERENCES vendas(id),
    parcela INTEGER NOT NULL DEFAULT 1,
    total_parcelas INTEGER NOT NULL DEFAULT 1,
    valor REAL NOT NULL,
    valor_pago REAL NOT NULL DEFAULT 0,
    data_emissao TEXT NOT NULL,
    data_vencimento TEXT NOT NULL,
    data_pagamento TEXT,
    forma_pagamento TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL DEFAULT 'ABERTO',
    observacoes TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_receber_venc ON contas_receber(status, data_vencimento);
CREATE INDEX IF NOT EXISTS idx_receber_venda ON contas_receber(venda_id);

CREATE TABLE IF NOT EXISTS contas_pagar (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    descricao TEXT NOT NULL,
    fornecedor_id INTEGER REFERENCES fornecedores(id),
    compra_id INTEGER REFERENCES compras(id),
    parcela INTEGER NOT NULL DEFAULT 1,
    total_parcelas INTEGER NOT NULL DEFAULT 1,
    valor REAL NOT NULL,
    valor_pago REAL NOT NULL DEFAULT 0,
    data_emissao TEXT NOT NULL,
    data_vencimento TEXT NOT NULL,
    data_pagamento TEXT,
    forma_pagamento TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL DEFAULT 'ABERTO',
    observacoes TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_pagar_venc ON contas_pagar(status, data_vencimento);
CREATE INDEX IF NOT EXISTS idx_pagar_compra ON contas_pagar(compra_id);

CREATE TABLE IF NOT EXISTS caixa_lancamentos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    data TEXT NOT NULL,
    tipo TEXT NOT NULL,
    descricao TEXT NOT NULL,
    valor REAL NOT NULL,
    forma_pagamento TEXT NOT NULL DEFAULT '',
    origem TEXT NOT NULL DEFAULT 'MANUAL',
    origem_id INTEGER,
    usuario_id INTEGER REFERENCES usuarios(id)
);
CREATE INDEX IF NOT EXISTS idx_caixa_data ON caixa_lancamentos(data);
";
}
