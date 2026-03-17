using System;
using System.Collections.Generic;
using System.Text;
using MeuERP.Models;
using MeuERP.Repositories;

namespace MeuERP.Services
{
    public class MovimentacaoService
    {
        private readonly MovimentacaoRepository _repository;

        public MovimentacaoService()
        {
            _repository = new MovimentacaoRepository();
        }

        public void Entrada(int produtoId, decimal quantidade)
        {
            if (quantidade <= 0)
                throw new Exception("Quantidade deve ser maior que zero.");

            MovimentacaoEstoque mov = new MovimentacaoEstoque
            {
                ProdutoId = produtoId,
                Tipo = "ENTRADA",
                Quantidade = quantidade
            };

            _repository.Registrar(mov);
        }

        public void Saida(int produtoId, decimal quantidade)
        {
            if (quantidade <= 0)
                throw new Exception("Quantidade deve ser maior que zero.");

            MovimentacaoEstoque mov = new MovimentacaoEstoque
            {
                ProdutoId = produtoId,
                Tipo = "SAIDA",
                Quantidade = quantidade
            };

            _repository.Registrar(mov);
        }
    }
}