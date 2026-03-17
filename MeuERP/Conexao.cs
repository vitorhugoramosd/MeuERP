using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;


namespace MeuERP
{
    public class Conexao
    {
        public static NpgsqlConnection ObterConexao()
        {
            string conexao =
            "Host=localhost;Port=5432;Username=postgres;Password=martha01;Database=meuerp";

            return new NpgsqlConnection(conexao);

        }

    }
}
