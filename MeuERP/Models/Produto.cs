using System;
using System.Collections.Generic;
using System.Text;

namespace MeuERP.Models
{
    public class Produto
    {
        public int Id { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string Descricao { get; set; } = string.Empty;

        public string Unidade { get; set; } = string.Empty;

        public decimal Preco { get; set; }

        public decimal Estoque { get; set; }

    }
}
