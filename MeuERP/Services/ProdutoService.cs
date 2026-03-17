using System;
using System.Collections.Generic;
using System.Text;
using MeuERP.Models;
using MeuERP.Repositories;

namespace MeuERP.Services
{
    public class ProdutoService
    {
        private readonly ProdutoRepository _repository;

        public ProdutoService()
        {
            _repository = new ProdutoRepository();
        }

        public void Salvar(Produto produto)
        {
            ValidarProduto(produto);

            if (produto.Id == 0)
                _repository.Inserir(produto);
            else
                _repository.Atualizar(produto);
        }

        private void ValidarProduto(Produto produto)
        {
            if (string.IsNullOrWhiteSpace(produto.Codigo))
                throw new Exception("O código do produto é obrigatório.");

            if (string.IsNullOrWhiteSpace(produto.Descricao))
                throw new Exception("A descrição do produto é obrigatória.");

            if (string.IsNullOrWhiteSpace(produto.Unidade))
                throw new Exception("A unidade do produto é obrigatória.");

            if (produto.Preco < 0)
                throw new Exception("O preço não pode ser negativo.");
        }

        public List<Produto> Buscar(string texto)
        {
            return _repository.Buscar(texto);
        }

        public List<Produto> ListarTodos()
        {
            return _repository.ListarTodos();
        }
    }
}

