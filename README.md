# MeuERP

Sistema de gestão empresarial em desktop para cadastro de produtos e controle de estoque.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Windows Forms](https://img.shields.io/badge/Windows%20Forms-Desktop-0078D4?logo=windows)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-336791?logo=postgresql)
![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp)

---

## Sobre o projeto

**MeuERP** é uma aplicação Windows Forms que permite:

- **Cadastro de produtos** — código, descrição, unidade, preço e controle de estoque
- **Movimentação de estoque** — registro de entradas e saídas por produto
- **Busca de produtos** — por código ou descrição

A aplicação segue uma arquitetura em camadas (Models, Repositories, Services) e utiliza **PostgreSQL** como banco de dados.

---

## Tecnologias

| Tecnologia        | Uso                    |
|-------------------|------------------------|
| **.NET 10**       | Runtime e framework    |
| **Windows Forms** | Interface gráfica     |
| **C# 12**         | Linguagem             |
| **Npgsql**        | Acesso ao PostgreSQL  |
| **PostgreSQL**   | Banco de dados        |

---

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) (15 ou superior)
- Windows (aplicação desktop)

---

## Configuração do banco de dados

1. Crie um banco chamado `meuerp` no PostgreSQL.

2. Execute o script SQL abaixo para criar as tabelas:

```sql
-- Tabela de produtos
CREATE TABLE produtos (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(50) NOT NULL,
    descricao VARCHAR(200) NOT NULL,
    unidade VARCHAR(20) NOT NULL,
    preco DECIMAL(18, 2) NOT NULL DEFAULT 0,
    estoque DECIMAL(18, 4) NOT NULL DEFAULT 0
);

-- Tabela de movimentações de estoque
CREATE TABLE movimentacoes_estoque (
    id SERIAL PRIMARY KEY,
    produto_id INTEGER NOT NULL REFERENCES produtos(id),
    tipo VARCHAR(20) NOT NULL,
    quantidade DECIMAL(18, 4) NOT NULL,
    data_movimento TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Índice para buscas
CREATE INDEX idx_produtos_codigo ON produtos(codigo);
CREATE INDEX idx_produtos_descricao ON produtos(descricao);
```

3. Ajuste a **string de conexão** no arquivo `Conexao.cs` conforme seu ambiente (host, porta, usuário, senha e nome do banco).

> **Dica:** Para não versionar senha no Git, use variáveis de ambiente ou um arquivo de configuração fora do controle de versão.

---

## Como executar

1. Clone o repositório:
   ```bash
   git clone https://github.com/seu-usuario/MeuERP.git
   cd MeuERP/MeuERP
   ```

2. Restaure as dependências:
   ```bash
   dotnet restore
   ```

3. Execute a aplicação:
   ```bash
   dotnet run
   ```

4. Na tela principal, use o menu **Cadastros → Produtos** para abrir a tela de produtos.

---

## Estrutura do projeto

```
MeuERP/
├── Conexao.cs              # Coneexão com PostgreSQL
├── Program.cs              # Ponto de entrada da aplicação
├── TelaPrincipal.cs        # Form principal (menu MDI)
├── TelaProdutos.cs         # Cadastro e listagem de produtos
├── Models/
│   ├── Produto.cs          # Entidade produto
│   └── MovimentacaoEstoque.cs
├── Repositories/
│   ├── ProdutoRepository.cs
│   └── MovimentacaoRepository.cs
├── Services/
│   ├── ProdutoService.cs
│   └── MovimentacaoService.cs
└── MeuERP.csproj
```

---

## Funcionalidades da tela de produtos

- **Novo** — limpa os campos para cadastrar um novo produto  
- **Salvar** — insere ou atualiza o produto (validação de código, descrição, unidade e preço)  
- **Excluir** — remove o produto selecionado (com confirmação)  
- **Buscar** — filtra por código ou descrição (Enter no campo de busca)  
- **Grid** — clique em uma linha para carregar o produto nos campos e editar  

---

## Licença

Este projeto está sob a licença MIT. Sinta-se à vontade para usar e modificar.
