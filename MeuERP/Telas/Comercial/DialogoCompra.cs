using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Comercial;

/// <summary>
/// Pedido de compra: fornecedor, NF, itens (código/busca, quantidade, custo), desconto, frete e pagamento.
/// Compras recebidas ou canceladas abrem somente para leitura. Atalho: F2 busca produto.
/// </summary>
public class DialogoCompra : DialogoBase
{
    private int _compraId;
    private readonly bool _somenteLeitura;
    private readonly List<CompraItem> _itens = new();
    private Fornecedor? _fornecedor;
    private Produto? _produto;
    private bool _definindoCodigo;

    private readonly Label _lblFornecedor, _lblFornecedorInfo, _lblProduto, _lblSubtotal, _lblTotal, _lblQtdItens;
    private readonly BotaoModerno _btnSelFornecedor, _btnSalvarPedido, _btnSalvarReceber;
    private readonly TextBox _txtNf, _txtCodigo, _txtObs;
    private readonly CampoDecimal _txtQtd, _txtCusto, _txtDesconto, _txtFrete;
    private readonly GridModerno _grid;
    private readonly ComboBox _cbForma;
    private readonly NumericUpDown _nudParcelas;
    private readonly DateTimePicker _dtVenc;
    private readonly Panel _pnlInclusao;

    /// <summary>True se algo foi gravado (mesmo que o recebimento tenha falhado depois de salvar o pedido).</summary>
    public bool Gravou { get; private set; }

    private static string TituloDe(Compra? c) => c == null ? "Nova compra"
        : c.Status == StatusCompra.Pedido ? $"Pedido de compra nº {c.Id}"
        : $"Compra nº {c.Id}  -  {Tema.TextoStatus(c.Status)}" +
          (c.DataRecebimento.HasValue ? $" em {Formato.DataHora(c.DataRecebimento.Value)}" : "");

    public DialogoCompra(Compra? compra = null) : base(TituloDe(compra), 1000, 660)
    {
        _compraId = compra?.Id ?? 0;
        _somenteLeitura = compra != null && compra.Status != StatusCompra.Pedido;
        AcceptButton = null;
        BtnSalvar.Visible = false;

        // ---------- Fornecedor e NF ----------
        var pnlTopo = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Color.White };
        pnlTopo.Controls.Add(ComercialUi.RotuloTexto("Fornecedor *", new Point(0, 2), Tema.FontePequena));
        _lblFornecedor = new Label
        {
            Location = new Point(0, 18), Size = new Size(400, 24), Font = Tema.FonteSubtitulo, ForeColor = Tema.Perigo,
            AutoEllipsis = true, Text = "Nenhum fornecedor selecionado"
        };
        _lblFornecedorInfo = new Label
        {
            Location = new Point(410, 22), Size = new Size(200, 20), Font = Tema.FontePequena, ForeColor = Tema.TextoSuave, AutoEllipsis = true
        };
        _btnSelFornecedor = new BotaoModerno("Selecionar", EstiloBotao.Secundario, SelecionarFornecedor) { Location = new Point(620, 10) };
        pnlTopo.Controls.AddRange(new Control[] { _lblFornecedor, _lblFornecedorInfo, _btnSelFornecedor });
        _txtNf = ComercialUi.Campo(pnlTopo, "Nº da nota fiscal", new TextBox { MaxLength = 20 }, 810, 160, 2);

        // ---------- Inclusão de item ----------
        _pnlInclusao = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.White };
        _txtCodigo = ComercialUi.Campo(_pnlInclusao, "Código / código de barras", new TextBox(), 0, 220);
        var btnBuscar = new BotaoModerno("Buscar (F2)", EstiloBotao.Secundario, BuscarProduto) { Location = new Point(226, 16) };
        _pnlInclusao.Controls.Add(btnBuscar);
        _txtQtd = ComercialUi.Campo(_pnlInclusao, "Quantidade", new CampoDecimal { Casas = 3 }, 350, 90);
        _txtCusto = ComercialUi.Campo(_pnlInclusao, "Custo unitário (R$)", new CampoDecimal(), 450, 120);
        var btnAdicionar = new BotaoModerno("Adicionar", EstiloBotao.Primario, Adicionar) { Location = new Point(580, 16) };
        var btnRemover = new BotaoModerno("Remover item", EstiloBotao.Secundario, RemoverItem);
        btnRemover.Location = new Point(970 - btnRemover.Width, 16);
        _pnlInclusao.Controls.Add(btnAdicionar);
        _pnlInclusao.Controls.Add(btnRemover);
        _lblProduto = new Label { Location = new Point(0, 54), Size = new Size(970, 20), ForeColor = Tema.TextoSuave, AutoEllipsis = true };
        _pnlInclusao.Controls.Add(_lblProduto);
        _txtQtd.Valor = 1;

        // ---------- Grid ----------
        _grid = new GridModerno();
        _grid.Coluna(nameof(CompraItem.ProdutoCodigo), "Código", 100);
        _grid.Coluna(nameof(CompraItem.ProdutoDescricao), "Descrição");
        _grid.Coluna(nameof(CompraItem.Unidade), "Un.", 50);
        _grid.ColunaNumero(nameof(CompraItem.Quantidade), "Qtd", 100);
        _grid.ColunaMoeda(nameof(CompraItem.CustoUnitario), "Custo unitário", 130);
        _grid.ColunaMoeda(nameof(CompraItem.Total), "Total", 130);
        var cartaoGrid = new Cartao { Dock = DockStyle.Fill, Padding = new Padding(6) };
        cartaoGrid.Controls.Add(_grid);

        // ---------- Pagamento e totais ----------
        var pnlRodape = new Panel { Dock = DockStyle.Bottom, Height = 166, BackColor = Color.White };
        _cbForma = ComercialUi.Campo(pnlRodape, "Forma de pagamento", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 0, 190, 10);
        _cbForma.CarregarTextos(FormasPagamento.Todas, compra?.FormaPagamento ?? FormasPagamento.Boleto);
        _nudParcelas = ComercialUi.Campo(pnlRodape, "Parcelas", new NumericUpDown { Minimum = 1, Maximum = 24, Value = 1 }, 200, 80, 10);
        _dtVenc = ComercialUi.Campo(pnlRodape, "1º vencimento",
            new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(30) }, 290, 120, 10);
        _lblQtdItens = ComercialUi.RotuloTexto("", new Point(420, 33));
        pnlRodape.Controls.Add(_lblQtdItens);
        _txtObs = ComercialUi.Campo(pnlRodape, "Observações",
            new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, MaxLength = 1000 }, 0, 590, 66);
        _txtObs.Height = 70;

        var pnlTotais = new Panel { Location = new Point(610, 10), Size = new Size(360, 150), BackColor = Tema.Cabecalho };
        pnlTotais.Paint += (_, e) =>
        {
            using var pen = new Pen(Tema.Borda);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlTotais.Width - 1, pnlTotais.Height - 1);
            e.Graphics.DrawLine(pen, 12, 96, pnlTotais.Width - 12, 96);
        };
        pnlTotais.Controls.Add(ComercialUi.RotuloTexto("Subtotal", new Point(14, 12)));
        _lblSubtotal = ComercialUi.RotuloValor(Formato.Moeda(0), new Point(150, 8), new Size(196, 24), Tema.FonteSubtitulo);
        pnlTotais.Controls.Add(_lblSubtotal);
        pnlTotais.Controls.Add(ComercialUi.RotuloTexto("Desconto (R$)", new Point(14, 42)));
        _txtDesconto = new CampoDecimal { Location = new Point(226, 38), Width = 120, Font = Tema.Fonte };
        pnlTotais.Controls.Add(_txtDesconto);
        pnlTotais.Controls.Add(ComercialUi.RotuloTexto("Frete (R$)", new Point(14, 70)));
        _txtFrete = new CampoDecimal { Location = new Point(226, 66), Width = 120, Font = Tema.Fonte };
        pnlTotais.Controls.Add(_txtFrete);
        pnlTotais.Controls.Add(ComercialUi.RotuloTexto("TOTAL", new Point(14, 114), Tema.FonteSubtitulo, Tema.Texto));
        _lblTotal = ComercialUi.RotuloValor(Formato.Moeda(0), new Point(90, 100), new Size(256, 46), ComercialUi.FonteTotal, Tema.Primaria);
        pnlTotais.Controls.Add(_lblTotal);
        pnlRodape.Controls.Add(pnlTotais);

        Corpo.Controls.Add(cartaoGrid);
        Corpo.Controls.Add(_pnlInclusao);
        Corpo.Controls.Add(pnlTopo);
        Corpo.Controls.Add(pnlRodape);
        cartaoGrid.BringToFront();
        pnlTopo.TabIndex = 0; _pnlInclusao.TabIndex = 1; cartaoGrid.TabIndex = 2; pnlRodape.TabIndex = 3;

        // ---------- Botões ----------
        _btnSalvarReceber = new BotaoModerno("Salvar e receber", EstiloBotao.Sucesso, SalvarEReceber);
        _btnSalvarPedido = new BotaoModerno("Salvar pedido", EstiloBotao.Primario, SalvarPedido);
        Rodape.Controls.Add(_btnSalvarReceber);
        Rodape.Controls.Add(_btnSalvarPedido);

        // ---------- Eventos ----------
        _txtCodigo.TextChanged += (_, _) => { if (!_definindoCodigo && _produto != null) { _produto = null; AtualizarInfoProduto(); } };
        _txtCodigo.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SelecionarPorCodigo(); }
            else if (e.KeyCode == Keys.Down && _grid.Rows.Count > 0) { e.Handled = true; _grid.Focus(); }
        };
        foreach (var campo in new[] { _txtQtd, _txtCusto })
            campo.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Adicionar(); } };
        _txtNf.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _txtCodigo.Focus(); } };
        _txtDesconto.TextChanged += (_, _) => AtualizarTotais();
        _txtFrete.TextChanged += (_, _) => AtualizarTotais();
        _grid.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete && !_somenteLeitura) { e.Handled = true; RemoverItem(); } };
        _cbForma.SelectedIndexChanged += (_, _) => AtualizarPagamento();
        FormClosing += AoFechar;
        Shown += (_, _) =>
        {
            if (_somenteLeitura) BtnCancelar.Focus();
            else if (_fornecedor == null) _btnSelFornecedor.Focus();
            else _txtCodigo.Focus();
        };

        // ---------- Carga ----------
        if (compra != null)
        {
            foreach (var i in compra.Itens) _itens.Add(Copiar(i));
            _txtNf.Text = compra.NumeroNf;
            _txtDesconto.Valor = compra.Desconto;
            _txtFrete.Valor = compra.Frete;
            _nudParcelas.Value = Math.Clamp(compra.Parcelas, 1, 24);
            _txtObs.Text = compra.Observacoes;
            var f = Dados.FornecedorRepository.Obter(compra.FornecedorId);
            if (f != null) DefinirFornecedor(f);
            else { _lblFornecedor.Text = compra.FornecedorNome; _lblFornecedor.ForeColor = Tema.Texto; }
        }
        AtualizarPagamento();
        AtualizarGrid();
        AtualizarInfoProduto();

        if (_somenteLeitura)
        {
            _pnlInclusao.Enabled = false;
            _btnSelFornecedor.Visible = false;
            _txtNf.ReadOnly = _txtObs.ReadOnly = _txtDesconto.ReadOnly = _txtFrete.ReadOnly = true;
            _cbForma.Enabled = _nudParcelas.Enabled = _dtVenc.Enabled = false;
            _btnSalvarPedido.Visible = _btnSalvarReceber.Visible = false;
            BtnCancelar.Text = "Fechar";
            _lblProduto.Text = "";
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F2) { BuscarProduto(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- Fornecedor ----------

    private void SelecionarFornecedor()
    {
        var f = Seletor.Fornecedor();
        if (f != null) { DefinirFornecedor(f); _txtNf.Focus(); }
    }

    private void DefinirFornecedor(Fornecedor f)
    {
        _fornecedor = f;
        _lblFornecedor.Text = f.Nome;
        _lblFornecedor.ForeColor = Tema.Texto;
        _lblFornecedorInfo.Text = string.Join("  •  ", new[] { f.DocumentoFormatado, f.Telefone }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    // ---------- Itens ----------

    private void BuscarProduto()
    {
        if (_somenteLeitura) return;
        var p = Seletor.Produto();
        if (p != null) { DefinirProduto(p); FocarQuantidade(); }
        else _txtCodigo.Focus();
    }

    private void SelecionarPorCodigo()
    {
        if (_somenteLeitura || _txtCodigo.Text.Trim().Length == 0) return;
        if (_produto != null) { FocarQuantidade(); return; }
        var p = ComercialUi.ResolverProduto(_txtCodigo.Text);
        if (p != null) { DefinirProduto(p); FocarQuantidade(); }
        else { _txtCodigo.Focus(); _txtCodigo.SelectAll(); }
    }

    private void FocarQuantidade() { _txtQtd.Focus(); _txtQtd.SelectAll(); }

    private void DefinirProduto(Produto p)
    {
        _produto = p;
        _definindoCodigo = true;
        _txtCodigo.Text = p.Codigo;
        _definindoCodigo = false;
        _txtCusto.Valor = p.PrecoCusto;
        if (!_txtQtd.Valido || _txtQtd.Valor <= 0) _txtQtd.Valor = 1;
        AtualizarInfoProduto();
    }

    private void AtualizarInfoProduto()
    {
        if (_somenteLeitura) return;
        if (_produto == null)
        {
            _lblProduto.ForeColor = Tema.TextoSuave;
            _lblProduto.Text = "Digite o código ou passe o leitor de código de barras e tecle Enter.";
            return;
        }
        var p = _produto;
        _lblProduto.ForeColor = Tema.Texto;
        _lblProduto.Text = $"{p.Descricao}   •   Custo atual {Formato.Moeda(p.PrecoCusto)}   •   Preço de venda {Formato.Moeda(p.PrecoVenda)}" +
                           $"   •   Estoque atual: {Formato.Qtd(p.Estoque)} {p.Unidade}";
    }

    private void Adicionar()
    {
        if (_somenteLeitura) return;
        if (_produto == null)
        {
            SelecionarPorCodigo();
            if (_produto == null) { _txtCodigo.Focus(); return; }
        }
        var p = _produto;
        if (!_txtQtd.Valido || _txtQtd.Valor <= 0) { Mensagem.Aviso("Informe uma quantidade maior que zero."); FocarQuantidade(); return; }
        if (!_txtCusto.Valido || _txtCusto.Valor < 0) { Mensagem.Aviso("Custo unitário inválido."); _txtCusto.Focus(); return; }

        decimal qtd = Math.Round(_txtQtd.Valor, 3);
        decimal custo = Math.Round(_txtCusto.Valor, 4);
        var item = _itens.FirstOrDefault(i => i.ProdutoId == p.Id);
        if (item != null)
        {
            item.Quantidade += qtd;
            item.Total = Formato.Arredondar(item.Quantidade * item.CustoUnitario);
        }
        else
        {
            item = new CompraItem
            {
                ProdutoId = p.Id, ProdutoCodigo = p.Codigo, ProdutoDescricao = p.Descricao, Unidade = p.Unidade,
                Quantidade = qtd, CustoUnitario = custo, Total = Formato.Arredondar(qtd * custo)
            };
            _itens.Add(item);
        }
        AtualizarGrid(item);

        _produto = null;
        _definindoCodigo = true;
        _txtCodigo.Text = "";
        _definindoCodigo = false;
        _txtQtd.Valor = 1;
        _txtCusto.Valor = 0;
        AtualizarInfoProduto();
        _txtCodigo.Focus();
    }

    private void RemoverItem()
    {
        if (_somenteLeitura) return;
        var item = _grid.Selecionado<CompraItem>();
        if (item == null) { Mensagem.Info("Selecione o item a remover."); return; }
        int linha = _grid.CurrentRow?.Index ?? 0;
        _itens.Remove(item);
        AtualizarGrid();
        if (_grid.Rows.Count > 0)
        {
            _grid.CurrentCell = _grid.Rows[Math.Min(linha, _grid.Rows.Count - 1)].Cells[0];
            _grid.Focus();
        }
        else _txtCodigo.Focus();
    }

    private void AtualizarGrid(CompraItem? selecionar = null)
    {
        _grid.Carregar(_itens.ToList());
        if (selecionar != null)
        {
            int idx = _itens.IndexOf(selecionar);
            if (idx >= 0 && idx < _grid.Rows.Count) _grid.CurrentCell = _grid.Rows[idx].Cells[0];
        }
        AtualizarTotais();
    }

    // ---------- Totais e pagamento ----------

    private decimal Subtotal => Formato.Arredondar(_itens.Sum(i => i.Total));

    private decimal TotalCalculado =>
        Formato.Arredondar(Subtotal - (_txtDesconto.Valido ? _txtDesconto.Valor : 0) + (_txtFrete.Valido ? _txtFrete.Valor : 0));

    private void AtualizarTotais()
    {
        decimal subtotal = Subtotal;
        _lblSubtotal.Text = Formato.Moeda(subtotal);
        _lblTotal.Text = Formato.Moeda(TotalCalculado);
        bool invalido = !_txtDesconto.Valido || !_txtFrete.Valido || _txtDesconto.Valor < 0 || _txtFrete.Valor < 0 || _txtDesconto.Valor > subtotal;
        _lblTotal.ForeColor = invalido ? Tema.Perigo : Tema.Primaria;
        _lblQtdItens.Text = _itens.Count == 0 ? "" : $"{_itens.Count} {(_itens.Count == 1 ? "item" : "itens")}  •  {Formato.Qtd(_itens.Sum(i => i.Quantidade))} unid.";
    }

    private string FormaSelecionada => _cbForma.SelectedItem as string ?? FormasPagamento.Boleto;

    private void AtualizarPagamento()
    {
        bool aVista = FormasPagamento.EhAVista(FormaSelecionada);
        if (aVista) _nudParcelas.Value = 1;
        _nudParcelas.Enabled = _dtVenc.Enabled = !aVista && !_somenteLeitura;
    }

    // ---------- Gravação ----------

    private static CompraItem Copiar(CompraItem i) => new()
    {
        ProdutoId = i.ProdutoId, ProdutoCodigo = i.ProdutoCodigo, ProdutoDescricao = i.ProdutoDescricao, Unidade = i.Unidade,
        Quantidade = i.Quantidade, CustoUnitario = i.CustoUnitario, Total = i.Total
    };

    private Compra MontarCompra()
    {
        if (_fornecedor == null) throw new RegraException("Selecione o fornecedor.");
        if (_itens.Count == 0) throw new RegraException("Inclua pelo menos um item na compra.");
        if (!_txtDesconto.Valido) throw new RegraException("O desconto informado é inválido.");
        if (!_txtFrete.Valido) throw new RegraException("O frete informado é inválido.");
        return new Compra
        {
            Id = _compraId,
            FornecedorId = _fornecedor.Id,
            NumeroNf = _txtNf.Text.Trim(),
            Desconto = Formato.Arredondar(_txtDesconto.Valor),
            Frete = Formato.Arredondar(_txtFrete.Valor),
            FormaPagamento = FormaSelecionada,
            Parcelas = (int)_nudParcelas.Value,
            Observacoes = _txtObs.Text.Trim(),
            Itens = _itens.Select(Copiar).ToList()
        };
    }

    private void SalvarPedido()
    {
        if (_somenteLeitura) return;
        Compra? c = null;
        if (!Mensagem.Tentar(() => c = MontarCompra()) || c == null) return;
        int id = 0;
        if (!Mensagem.Tentar(() => id = CompraService.SalvarPedido(c))) return;
        _compraId = id;
        Gravou = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SalvarEReceber()
    {
        if (_somenteLeitura) return;
        Compra? c = null;
        if (!Mensagem.Tentar(() => c = MontarCompra()) || c == null) return;

        bool aVista = FormasPagamento.EhAVista(c.FormaPagamento);
        DateTime? venc = aVista ? null : _dtVenc.Value.Date;
        if (!ComercialUi.Perguntar($"Salvar o pedido e receber a mercadoria agora?\n\nFornecedor: {_fornecedor!.Nome}\n" +
                                    $"Total: {Formato.Moeda(TotalCalculado)}\nPagamento: {ComercialUi.TextoPagamento(c.FormaPagamento, c.Parcelas, venc)}\n\n" +
                                    "Os itens entrarão no estoque e os títulos a pagar serão gerados."))
            return;

        int id = 0;
        if (!Mensagem.Tentar(() => id = CompraService.SalvarPedido(c))) return;
        _compraId = id;  // se o recebimento falhar, as próximas gravações atualizam este pedido
        Gravou = true;
        if (!Mensagem.Tentar(() => CompraService.Receber(id, venc)))
        {
            Text = $"Pedido de compra nº {id}";
            return;
        }
        Mensagem.Info($"Compra nº {id} recebida com sucesso.");
        DialogResult = DialogResult.OK;
        Close();
    }

    private void AoFechar(object? sender, FormClosingEventArgs e)
    {
        if (_somenteLeitura || DialogResult == DialogResult.OK || _itens.Count == 0 || e.CloseReason != CloseReason.UserClosing) return;
        if (!Mensagem.Confirmar("Descartar as alterações desta compra e fechar?")) e.Cancel = true;
    }
}
