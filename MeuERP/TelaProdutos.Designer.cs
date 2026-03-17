namespace MeuERP
{
    partial class TelaProdutos
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            txtCodigo = new TextBox();
            label2 = new Label();
            txtDescricao = new TextBox();
            label3 = new Label();
            cbUnidade = new ComboBox();
            label4 = new Label();
            txtPreco = new TextBox();
            btnNovo = new Button();
            btnSalvar = new Button();
            btnExcluir = new Button();
            gridProdutos = new DataGridView();
            colId = new DataGridViewTextBoxColumn();
            colCodigo = new DataGridViewTextBoxColumn();
            colDescricao = new DataGridViewTextBoxColumn();
            colUnidade = new DataGridViewTextBoxColumn();
            colPreco = new DataGridViewTextBoxColumn();
            colEstoque = new DataGridViewTextBoxColumn();
            label5 = new Label();
            txtBuscar = new TextBox();
            btnBuscar = new Button();
            ((System.ComponentModel.ISupportInitialize)gridProdutos).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 30);
            label1.Name = "label1";
            label1.Size = new Size(46, 15);
            label1.TabIndex = 0;
            label1.Text = "Código";
            label1.Click += label1_Click;
            // 
            // txtCodigo
            // 
            txtCodigo.Location = new Point(76, 27);
            txtCodigo.Name = "txtCodigo";
            txtCodigo.Size = new Size(100, 23);
            txtCodigo.TabIndex = 1;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 74);
            label2.Name = "label2";
            label2.Size = new Size(58, 15);
            label2.TabIndex = 2;
            label2.Text = "Descrição";
            // 
            // txtDescricao
            // 
            txtDescricao.Location = new Point(76, 71);
            txtDescricao.Name = "txtDescricao";
            txtDescricao.Size = new Size(300, 23);
            txtDescricao.TabIndex = 3;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 120);
            label3.Name = "label3";
            label3.Size = new Size(51, 15);
            label3.TabIndex = 4;
            label3.Text = "Unidade";
            // 
            // cbUnidade
            // 
            cbUnidade.FormattingEnabled = true;
            cbUnidade.Location = new Point(76, 117);
            cbUnidade.Name = "cbUnidade";
            cbUnidade.Size = new Size(100, 23);
            cbUnidade.TabIndex = 5;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(12, 168);
            label4.Name = "label4";
            label4.Size = new Size(37, 15);
            label4.TabIndex = 6;
            label4.Text = "Preço";
            // 
            // txtPreco
            // 
            txtPreco.Location = new Point(76, 165);
            txtPreco.Name = "txtPreco";
            txtPreco.Size = new Size(100, 23);
            txtPreco.TabIndex = 7;
            // 
            // btnNovo
            // 
            btnNovo.Location = new Point(12, 216);
            btnNovo.Name = "btnNovo";
            btnNovo.Size = new Size(75, 23);
            btnNovo.TabIndex = 8;
            btnNovo.Text = "Novo";
            btnNovo.UseVisualStyleBackColor = true;
            // 
            // btnSalvar
            // 
            btnSalvar.Location = new Point(93, 216);
            btnSalvar.Name = "btnSalvar";
            btnSalvar.Size = new Size(75, 23);
            btnSalvar.TabIndex = 9;
            btnSalvar.Text = "Salvar";
            btnSalvar.UseVisualStyleBackColor = true;
            btnSalvar.Click += btnSalvar_Click;
            // 
            // btnExcluir
            // 
            btnExcluir.Location = new Point(174, 216);
            btnExcluir.Name = "btnExcluir";
            btnExcluir.Size = new Size(75, 23);
            btnExcluir.TabIndex = 10;
            btnExcluir.Text = "Excluir";
            btnExcluir.UseVisualStyleBackColor = true;
            btnExcluir.Click += btnExcluir_Click;
            // 
            // gridProdutos
            // 
            gridProdutos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridProdutos.Columns.AddRange(new DataGridViewColumn[] { colId, colCodigo, colDescricao, colUnidade, colPreco, colEstoque });
            gridProdutos.Location = new Point(29, 301);
            gridProdutos.Name = "gridProdutos";
            gridProdutos.RowHeadersWidth = 51;
            gridProdutos.Size = new Size(803, 320);
            gridProdutos.TabIndex = 11;
            gridProdutos.CellClick += gridProdutos_CellClick;
            // 
            // colId
            // 
            colId.HeaderText = "Id";
            colId.MinimumWidth = 6;
            colId.Name = "colId";
            colId.Width = 125;
            // 
            // colCodigo
            // 
            colCodigo.HeaderText = "Código";
            colCodigo.MinimumWidth = 6;
            colCodigo.Name = "colCodigo";
            colCodigo.Width = 125;
            // 
            // colDescricao
            // 
            colDescricao.HeaderText = "Descrição";
            colDescricao.MinimumWidth = 6;
            colDescricao.Name = "colDescricao";
            colDescricao.Width = 125;
            // 
            // colUnidade
            // 
            colUnidade.HeaderText = "Unidade";
            colUnidade.MinimumWidth = 6;
            colUnidade.Name = "colUnidade";
            colUnidade.Width = 125;
            // 
            // colPreco
            // 
            colPreco.HeaderText = "Preço";
            colPreco.MinimumWidth = 6;
            colPreco.Name = "colPreco";
            colPreco.Width = 125;
            // 
            // colEstoque
            // 
            colEstoque.HeaderText = "Estoque";
            colEstoque.MinimumWidth = 6;
            colEstoque.Name = "colEstoque";
            colEstoque.Width = 125;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(29, 269);
            label5.Name = "label5";
            label5.Size = new Size(45, 15);
            label5.TabIndex = 12;
            label5.Text = "Buscar:";
            // 
            // txtBuscar
            // 
            txtBuscar.Location = new Point(80, 266);
            txtBuscar.Name = "txtBuscar";
            txtBuscar.Size = new Size(100, 23);
            txtBuscar.TabIndex = 13;
            // 
            // btnBuscar
            // 
            btnBuscar.Location = new Point(186, 265);
            btnBuscar.Name = "btnBuscar";
            btnBuscar.Size = new Size(75, 23);
            btnBuscar.TabIndex = 14;
            btnBuscar.Text = "Buscar";
            btnBuscar.UseVisualStyleBackColor = true;
            btnBuscar.Click += btnBuscar_Click;
            // 
            // TelaProdutos
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1445, 633);
            Controls.Add(btnBuscar);
            Controls.Add(txtBuscar);
            Controls.Add(label5);
            Controls.Add(gridProdutos);
            Controls.Add(btnExcluir);
            Controls.Add(btnSalvar);
            Controls.Add(btnNovo);
            Controls.Add(txtPreco);
            Controls.Add(label4);
            Controls.Add(cbUnidade);
            Controls.Add(label3);
            Controls.Add(txtDescricao);
            Controls.Add(label2);
            Controls.Add(txtCodigo);
            Controls.Add(label1);
            Name = "TelaProdutos";
            Text = "Cadastro de Produtos";
            Load += TelaProdutos_Load;
            ((System.ComponentModel.ISupportInitialize)gridProdutos).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox txtCodigo;
        private Label label2;
        private TextBox txtDescricao;
        private Label label3;
        private ComboBox cbUnidade;
        private Label label4;
        private TextBox txtPreco;
        private Button btnNovo;
        private Button btnSalvar;
        private Button btnExcluir;
        private DataGridView gridProdutos;
        private DataGridViewTextBoxColumn colId;
        private DataGridViewTextBoxColumn colCodigo;
        private DataGridViewTextBoxColumn colDescricao;
        private DataGridViewTextBoxColumn colUnidade;
        private DataGridViewTextBoxColumn colPreco;
        private DataGridViewTextBoxColumn colEstoque;
        private Label label5;
        private TextBox txtBuscar;
        private Button btnBuscar;
    }
}