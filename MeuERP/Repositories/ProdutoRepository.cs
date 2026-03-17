using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using MeuERP.Models;
using System.Runtime.InteropServices.Marshalling;

namespace MeuERP.Repositories;

public class ProdutoRepository
{
    public void Inserir(Produto produto)
    {
        using (var conn = Conexao.ObterConexao())
        {
            conn.Open();

            string sql = @"INSERT INTO produtos
                               (codigo, descricao, unidade, preco)
                               VALUES
                               (@codigo, @descricao, @unidade, @preco)";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("codigo", produto.Codigo);
                cmd.Parameters.AddWithValue("descricao", produto.Descricao);
                cmd.Parameters.AddWithValue("unidade", produto.Unidade);
                cmd.Parameters.AddWithValue("preco", produto.Preco);

                cmd.ExecuteNonQuery();
            }
        }
    }

    public void Atualizar(Produto produto)
    {
        using (var conn = Conexao.ObterConexao())
        {
            conn.Open();

            string sql = @"UPDATE produtos 
                           SET codigo = @codigo,
                               descricao = @descricao,
                               unidade = @unidade,
                               preco = @preco
                           WHERE id = @id";

            using (var cmd = new NpgsqlCommand(sql,conn))
            {
                cmd.Parameters.AddWithValue("Id", produto.Id);
                cmd.Parameters.AddWithValue("codigo", produto.Codigo);
                cmd.Parameters.AddWithValue("descricao", produto.Descricao);
                cmd.Parameters.AddWithValue("unidade", produto.Unidade);
                cmd.Parameters.AddWithValue("preco", produto.Preco);

                cmd.ExecuteNonQuery();
            }
        }
    }

    public void Excluir(int id)
    {
        using (var conn = Conexao.ObterConexao())
        {
            conn.Open();

            string sql = "DELETE FROM produtos WHERE id = @id";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("Id",id);

                cmd.ExecuteNonQuery();
            }
        }
    }

    public List<Produto> ListarTodos()
    {
        List<Produto> produtos = new List<Produto>();

        using (var conn = Conexao.ObterConexao())
        {
            conn.Open();

            string sql = "SELECT id, codigo, descricao, unidade, preco, estoque FROM produtos ORDER BY id";

            using (var cmd = new NpgsqlCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Produto produto = new Produto();

                    produto.Id = Convert.ToInt32(reader["id"]);
                    produto.Codigo = reader["codigo"].ToString() ?? string.Empty;
                    produto.Descricao = reader["descricao"].ToString() ?? string.Empty;
                    produto.Unidade = reader["unidade"].ToString() ?? string.Empty;
                    produto.Preco = Convert.ToDecimal(reader["preco"]);
                    produto.Estoque = Convert.ToDecimal(reader["estoque"]);

                    produtos.Add(produto);
                }
            }
        }

        return produtos;
    }


    public List<Produto> Buscar(string texto)
    {
        List<Produto> produtos = new List<Produto>();

        using (var conn = Conexao.ObterConexao())
        {
            conn.Open();

            string sql = @"SELECT id, codigo, descricao, unidade, preco, estoque
                       FROM produtos
                       WHERE codigo ILIKE @texto
                          OR descricao ILIKE @texto
                       ORDER BY id";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("texto", "%" + texto + "%");

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Produto produto = new Produto();

                        produto.Id = Convert.ToInt32(reader["id"]);
                        produto.Codigo = reader["codigo"]?.ToString() ?? string.Empty;
                        produto.Descricao = reader["descricao"]?.ToString() ?? string.Empty;
                        produto.Unidade = reader["unidade"]?.ToString() ?? string.Empty;
                        produto.Preco = Convert.ToDecimal(reader["preco"]);
                        produto.Estoque = Convert.ToDecimal(reader["estoque"]);

                        produtos.Add(produto);
                    }
                }
            }
        }

        return produtos;
    }

    public void AtualizarEstoque(int produtoId, decimal quantidade)
    {
        using (var conn = Conexao.ObterConexao())
        {
            conn.Open();

            string sql = @"UPDATE produtos
                       SET estoque = estoque + @qtd
                       WHERE id = @id";

            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("qtd", quantidade);
                cmd.Parameters.AddWithValue("id", produtoId);

                cmd.ExecuteNonQuery();
            }
        }
    }

}
