# MeuERP

Sistema de gestão empresarial (ERP) para desktop Windows: vendas com PDV, compras, estoque,
financeiro, relatórios e controle de usuários, com banco de dados embutido. Não precisa instalar
servidor de banco.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Windows Forms](https://img.shields.io/badge/Windows%20Forms-Desktop-0078D4?logo=windows)
![SQLite](https://img.shields.io/badge/SQLite-embutido-003B57?logo=sqlite)
![C#](https://img.shields.io/badge/C%23-14-239120?logo=csharp)

---

## Módulos

| Módulo | O que faz |
|---|---|
| **Dashboard** | Indicadores do dia e do mês (vendas, ticket médio, lucro bruto, contas a receber/pagar, caixa, estoque baixo), gráfico de vendas dos últimos 30 dias ou 12 meses, produtos mais vendidos, últimas vendas e contas vencendo. |
| **Vendas (PDV)** | Venda pelo teclado com leitor de código de barras, desconto por item e geral, orçamentos, venda à vista ou parcelada (crediário, boleto, cartão), limite de crédito do cliente, troco e comprovante impresso. O cancelamento devolve o estoque e estorna o financeiro. |
| **Compras** | Pedidos a fornecedores com frete e desconto. No recebimento a mercadoria entra no estoque, o custo médio é recalculado e as contas a pagar são geradas. |
| **Cadastros** | Produtos (categoria, código de barras, custo, preço, margem, estoque mínimo), categorias, clientes e fornecedores (pessoa física ou jurídica, com validação de CPF/CNPJ) e usuários. |
| **Estoque** | Posição de estoque com alerta de mínimo, histórico de todas as movimentações com saldo anterior e posterior, entradas, saídas e ajuste de inventário. |
| **Financeiro** | Contas a receber e a pagar com parcelamento, baixa total ou parcial, estorno, títulos recorrentes (aluguel, mensalidades) e controle de caixa com saldo. |
| **Relatórios** | Vendas por dia, produto, cliente, forma de pagamento e vendedor; compras por fornecedor; posição de estoque; contas em aberto; fluxo de caixa e resultado do período (DRE simplificado). Exportação para CSV/Excel e impressão. |
| **Configurações** | Dados da empresa (saem no comprovante), troca de senha, backup e restauração do banco e geração de dados de demonstração. |

### Perfis de acesso

| Perfil | Acesso |
|---|---|
| **Administrador** | Tudo, incluindo usuários, backup e dados da empresa. |
| **Gerente** | Tudo, exceto usuários, backup e dados da empresa. |
| **Operador** | Dashboard resumido, vendas, consulta de produtos e estoque, cadastro de clientes. |

---

## Como executar

Pré-requisitos: Windows e o [SDK do .NET 10](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/vitorhugoramosd/MeuERP.git
cd MeuERP/MeuERP
dotnet run
```

No primeiro acesso entre com o usuário **admin** e a senha **admin**, e troque a senha em
**Configurações → Minha senha**.

Para conhecer o sistema com dados de exemplo: **Configurações → Dados de demonstração**. São criados
produtos, clientes, fornecedores e seis meses de vendas, compras e movimentação financeira, além dos
usuários `gerente` / `gerente123` e `vendedor` / `vendedor123`.

### Gerar o executável

```bash
dotnet publish MeuERP/MeuERP.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publicar
```

O arquivo `publicar/MeuERP.exe` roda em qualquer Windows 64 bits, sem instalar o .NET.

---

## Banco de dados

O banco SQLite é criado sozinho na primeira execução em:

```
%LOCALAPPDATA%\MeuERP\meuerp.db
```

Faça cópias de segurança pelo menu **Configurações → Backup**. A restauração guarda uma cópia do banco
anterior ao lado do original (`meuerp.db.antes-da-restauracao`).

---

## Arquitetura

```
MeuERP/
├── Program.cs            Ponto de entrada (cultura pt-BR, login, tratamento global de erros)
├── AutoTeste*.cs         Verificação automática das regras de negócio
├── Models/               Entidades e constantes de domínio
├── Nucleo/               Sessão, senhas (PBKDF2), formatação e validação de CPF/CNPJ
├── Dados/                Conexão SQLite, esquema e repositórios (Dapper)
├── Servicos/             Regras de negócio: estoque, financeiro, vendas, compras, relatórios
├── UI/                   Tema visual e componentes (grid, botões, cartões, páginas, diálogos)
└── Telas/                Login, janela principal e as páginas de cada módulo
```

- Toda alteração de estoque passa pelo `EstoqueService` e fica registrada no histórico.
- Títulos e caixa só são alterados pelo `FinanceiroService`.
- Vendas, compras, cancelamentos e baixas rodam em transação: se uma parte falhar, nada é gravado.
- Senhas são guardadas com hash PBKDF2 (SHA-256, 100 mil iterações, sal aleatório).

### Testes

```bash
cd MeuERP
dotnet build
bin/Debug/net10.0-windows/MeuERP.exe --autoteste
```

O autoteste roda em um banco temporário e grava o resultado em `%TEMP%\meuerp-autoteste.log`.
Código de saída 0 significa que todas as verificações passaram.

---

## Licença

MIT.
