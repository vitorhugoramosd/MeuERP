using MeuERP.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;

namespace MeuERP.Repositories
{
    public class MovimentacaoRepository
    {
        public void Registrar(MovimentacaoEstoque mov)
        {
            using (var conn = Conexao.ObterConexao())
            {
                conn.Open();

                string sql = @"INSERT INTO movimentacoes_estoque
                               (produto_id, tipo, quantidade)
                               VALUES
                               (@produto, @tipo, @quantidade)";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("produto", mov.ProdutoId);
                    cmd.Parameters.AddWithValue("tipo", mov.Tipo);
                    cmd.Parameters.AddWithValue("quantidade", mov.Quantidade);

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}