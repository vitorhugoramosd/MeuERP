using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using MeuERP.Repositories;
using MeuERP.Models;
using MeuERP.Services;
using System.Drawing;

namespace MeuERP
{
    public partial class TelaProdutos : Form

    {

        private int produtoIdSelecionado = 0;

        public TelaProdutos()
        {
            InitializeComponent();
            CarregarProdutos();

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void btnNovo_Click(object sender, EventArgs e)
        {
            LimparCampos();
        }

        private void btnSalvar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidarCampos())
                    return;

                Produto produto = new Produto
                {
                    Id = produtoIdSelecionado,
                    Codigo = txtCodigo.Text.Trim(),
                    Descricao = txtDescricao.Text.Trim(),
                    Unidade = cbUnidade.Text.Trim(),
                    Preco = Convert.ToDecimal(txtPreco.Text)
                };

                ProdutoService service = new ProdutoService();
                service.Salvar(produto);

                MessageBox.Show("Produto salvo com sucesso!");

                CarregarProdutos();
                LimparCampos();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void CarregarProdutos()
        {
            ProdutoService service = new ProdutoService();
            List<Produto> produtos = service.ListarTodos();

            PreencherGrid(produtos);
        }

        private void LimparCampos()
        {
            produtoIdSelecionado = 0;

            txtCodigo.Clear();
            txtDescricao.Clear();
            txtPreco.Clear();
            cbUnidade.SelectedIndex = -1;

            txtCodigo.Focus();
        }

        private void gridProdutos_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                DataGridViewRow linha = gridProdutos.Rows[e.RowIndex];

                produtoIdSelecionado = Convert.ToInt32(linha.Cells["colId"].Value);

                txtCodigo.Text = linha.Cells["colCodigo"].Value?.ToString() ?? string.Empty;
                txtDescricao.Text = linha.Cells["colDescricao"].Value?.ToString() ?? string.Empty;
                cbUnidade.Text = linha.Cells["colUnidade"].Value?.ToString() ?? string.Empty;
                txtPreco.Text = linha.Cells["colPreco"].Value?.ToString() ?? string.Empty;
            }
        }

        private void btnExcluir_Click(object sender, EventArgs e)
        {
            if (produtoIdSelecionado == 0)
            {
                MessageBox.Show("Selecione um produto para excluir.");
                return;
            }

            DialogResult resultado = MessageBox.Show(
                "Deseja realmente excluir este produto?",
                "Confirmação",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (resultado == DialogResult.Yes)
            {
                ProdutoRepository repo = new ProdutoRepository();
                repo.Excluir(produtoIdSelecionado);

                MessageBox.Show("Produto excluído com sucesso!");

                CarregarProdutos();
                LimparCampos();
            }
        }

        private void TelaProdutos_Load(object sender, EventArgs e)
        {

        }

        private void PreencherGrid(List<Produto> produtos)
        {
            gridProdutos.Rows.Clear();

            foreach (Produto produto in produtos)
            {
                gridProdutos.Rows.Add(
                    produto.Id,
                    produto.Codigo,
                    produto.Descricao,
                    produto.Unidade,
                    produto.Preco,
                    produto.Estoque
                );
            }
        }

        private void btnBuscar_Click(object sender, EventArgs e)
        {
            ProdutoService service = new ProdutoService();
            List<Produto> produtos = service.Buscar(txtBuscar.Text);

            PreencherGrid(produtos);
        }

        private void txtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnBuscar_Click(sender, e);
            }
        }

        private bool ValidarCampos()
        {
            LimparValidacaoVisual();

            if (string.IsNullOrWhiteSpace(txtCodigo.Text))
            {
                txtCodigo.BackColor = Color.MistyRose;
                MessageBox.Show("Informe o código do produto.");
                txtCodigo.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtDescricao.Text))
            {
                txtDescricao.BackColor = Color.MistyRose;
                MessageBox.Show("Informe a descrição do produto.");
                txtDescricao.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(cbUnidade.Text))
            {
                cbUnidade.BackColor = Color.MistyRose;
                MessageBox.Show("Informe a unidade do produto.");
                cbUnidade.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtPreco.Text))
            {
                txtPreco.BackColor = Color.MistyRose;
                MessageBox.Show("Informe o preço do produto.");
                txtPreco.Focus();
                return false;
            }

            decimal preco;
            if (!decimal.TryParse(txtPreco.Text, out preco))
            {
                txtPreco.BackColor = Color.MistyRose;
                MessageBox.Show("Informe um preço válido.");
                txtPreco.Focus();
                return false;
            }

            if (preco < 0)
            {
                txtPreco.BackColor = Color.MistyRose;
                MessageBox.Show("O preço não pode ser negativo.");
                txtPreco.Focus();
                return false;
            }

            return true;
        }

        private void LimparValidacaoVisual()
        {
            txtCodigo.BackColor = Color.White;
            txtDescricao.BackColor = Color.White;
            cbUnidade.BackColor = Color.White;
            txtPreco.BackColor = Color.White;
        }
    }
}
