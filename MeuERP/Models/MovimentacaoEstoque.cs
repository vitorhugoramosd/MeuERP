using System;
using System.Collections.Generic;
using System.Text;

namespace MeuERP.Models
{
    public class MovimentacaoEstoque
    {
        public int Id { get; set; }

        public int ProdutoId { get; set; }

        public string Tipo { get; set; } = "";

        public decimal Quantidade { get; set; }

        public DateTime DataMovimento { get; set; }
    }
}
