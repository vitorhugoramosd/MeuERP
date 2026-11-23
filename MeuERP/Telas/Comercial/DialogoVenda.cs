using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.Servicos;
using MeuERP.UI;

namespace MeuERP.Telas.Comercial;

/// <summary>
/// Venda / orçamento no estilo PDV: inclusão rápida de itens pelo teclado (código ou código de barras + Enter),
/// totais em destaque e pagamento. Vendas finalizadas ou canceladas abrem somente para leitura.
/// Atalhos: F2 busca produto, F10 finaliza, Delete no grid remove o item.
/// </summary>
public class DialogoVenda : DialogoBase
{
    private readonly int _vendaId;
    private readonly bool _somenteLeitura;
    private readonly List<VendaItem> _itens = new();
    private Cliente? _cliente;
    private Produto? _produto;
    private bool _definindoCodigo;

    private readonly Label _lblCliente, _lblClienteInfo, _lblProduto, _lblSubtotal, _lblTotal, _lblQtdItens;
    private readonly BotaoModerno _btnSelCliente, _btnLimparCliente, _btnBuscar, _btnAdicionar, _btnRemover;
    private readonly BotaoModerno _btnFinalizar, _btnOrcamento, _btnImprimir;
    private readonly TextBox _txtCodigo, _txtObs;
    private readonly CampoDecimal _txtQtd, _txtPreco, _txtDescItem, _txtDesconto;
    private readonly GridModerno _grid;
    private readonly ComboBox _cbForma;
    private readonly NumericUpDown _nudParcelas;
    private readonly DateTimePicker _dtVenc;
    private readonly Panel _pnlInclusao;

    /// <summary>True se algo foi gravado (venda finalizada ou orçamento salvo).</summary>
    public bool Gravou { get; private set; }

    private static string TituloDe(Venda? v) => v == null ? "Nova venda"
        : v.Status == StatusVenda.Orcamento ? $"Orçamento nº {v.Id}"
        : $"Venda nº {v.Id}  -  {Tema.TextoStatus(v.Status)}  -  {Formato.DataHora(v.DataVenda)}";

    /// <param name="venda">null para nova venda; orçamento (editável) ou venda finalizada/cancelada (somente leitura), já com itens.</param>
    public DialogoVenda(Venda? venda = null) : base(TituloDe(venda), 1000, 660)
    {
        _vendaId = venda?.Id ?? 0;
        _somenteLeitura = venda != null && venda.Status != StatusVenda.Orcamento;
        AcceptButton = null; // o Enter é tratado campo a campo (fluxo de PDV)
        BtnSalvar.Visible = false;

        // ---------- Cliente ----------
        var pnlCliente = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White };
        pnlCliente.Controls.Add(ComercialUi.RotuloTexto("Cliente", new Point(0, 2), Tema.FontePequena));
        _lblCliente = new Label
        {
            Location = new Point(0, 18), Size = new Size(380, 24), Font = Tema.FonteSubtitulo, ForeColor = Tema.Texto,
            AutoEllipsis = true, Text = "Consumidor final"
        };
        _lblClienteInfo = new Label
        {
            Location = new Point(390, 22), Size = new Size(160, 20), Font = Tema.FontePequena, ForeColor = Tema.TextoSuave,
            AutoEllipsis = true
        };
        _btnSelCliente = new BotaoModerno("Selecionar", EstiloBotao.Secundario, SelecionarCliente) { Location = new Point(560, 8) };
        _btnLimparCliente = new BotaoModerno("Limpar", EstiloBotao.Secundario, () => DefinirCliente(null))
            { Location = new Point(_btnSelCliente.Right + 6, 8) };
        var dica = new Label
        {
            Text = "F2 buscar produto  •  F10 finalizar\nDelete remove o item selecionado", Location = new Point(790, 6),
            Size = new Size(180, 36), Font = Tema.FontePequena, ForeColor = Tema.TextoSuave, TextAlign = ContentAlignment.MiddleRight
        };
        pnlCliente.Controls.AddRange(new Control[] { _lblCliente, _lblClienteInfo, _btnSelCliente, _btnLimparCliente, dica });

        // ---------- Inclusão de item ----------
        _pnlInclusao = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.White };
        _txtCodigo = ComercialUi.Campo(_pnlInclusao, "Código / código de barras", new TextBox(), 0, 220);
        _btnBuscar = new BotaoModerno("Buscar (F2)", EstiloBotao.Secundario, BuscarProduto) { Location = new Point(226, 16) };
        _pnlInclusao.Controls.Add(_btnBuscar);
        _txtQtd = ComercialUi.Campo(_pnlInclusao, "Quantidade", new CampoDecimal { Casas = 3 }, 350, 90);
        _txtPreco = ComercialUi.Campo(_pnlInclusao, "Preço unitário", new CampoDecimal(), 450, 110);
        _txtDescItem = ComercialUi.Campo(_pnlInclusao, "Desconto do item (R$)", new CampoDecimal(), 570, 120);
        _btnAdicionar = new BotaoModerno("Adicionar", EstiloBotao.Primario, Adicionar) { Location = new Point(700, 16) };
        _btnRemover = new BotaoModerno("Remover item", EstiloBotao.Secundario, RemoverItem);
        _btnRemover.Location = new Point(970 - _btnRemover.Width, 16);
        _pnlInclusao.Controls.Add(_btnAdicionar);
        _pnlInclusao.Controls.Add(_btnRemover);
        _lblProduto = new Label
        {
            Location = new Point(0, 54), Size = new Size(970, 20), Font = Tema.Fonte, ForeColor = Tema.TextoSuave, AutoEllipsis = true
        };
        _pnlInclusao.Controls.Add(_lblProduto);
        _txtQtd.Valor = 1;
        if (!Sessao.PodeGerenciar) { _txtPreco.ReadOnly = true; _txtPreco.TabStop = false; }

        // ---------- Grid ----------
        _grid = new GridModerno();
        _grid.Coluna(nameof(VendaItem.ProdutoCodigo), "Código", 100);
        _grid.Coluna(nameof(VendaItem.ProdutoDescricao), "Descrição");
        _grid.Coluna(nameof(VendaItem.Unidade), "Un.", 50);
        _grid.ColunaNumero(nameof(VendaItem.Quantidade), "Qtd", 90);
        _grid.ColunaMoeda(nameof(VendaItem.PrecoUnitario), "Preço", 110);
        _grid.ColunaMoeda(nameof(VendaItem.Desconto), "Desconto", 100);
        _grid.ColunaMoeda(nameof(VendaItem.Total), "Total", 120);
        var cartaoGrid = new Cartao { Dock = DockStyle.Fill, Padding = new Padding(6) };
        cartaoGrid.Controls.Add(_grid);

        // ---------- Pagamento e totais ----------
        var pnlRodape = new Panel { Dock = DockStyle.Bottom, Height = 166, BackColor = Color.White };
        _cbForma = ComercialUi.Campo(pnlRodape, "Forma de pagamento", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 0, 190, 10);
        _cbForma.CarregarTextos(FormasPagamento.Todas, venda?.FormaPagamento ?? FormasPagamento.Dinheiro);
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
            e.Graphics.DrawLine(pen, 12, 80, pnlTotais.Width - 12, 80);
        };
        pnlTotais.Controls.Add(ComercialUi.RotuloTexto("Subtotal", new Point(14, 14)));
        _lblSubtotal = ComercialUi.RotuloValor(Formato.Moeda(0), new Point(150, 10), new Size(196, 24), Tema.FonteSubtitulo);
        pnlTotais.Controls.Add(_lblSubtotal);
        pnlTotais.Controls.Add(ComercialUi.RotuloTexto("Desconto geral (R$)", new Point(14, 48)));
        _txtDesconto = new CampoDecimal { Location = new Point(226, 44), Width = 120, Font = Tema.Fonte };
        pnlTotais.Controls.Add(_txtDesconto);
        pnlTotais.Controls.Add(ComercialUi.RotuloTexto("TOTAL", new Point(14, 104), Tema.FonteSubtitulo, Tema.Texto));
        _lblTotal = ComercialUi.RotuloValor(Formato.Moeda(0), new Point(90, 88), new Size(256, 50), ComercialUi.FonteTotal, Tema.Primaria);
        pnlTotais.Controls.Add(_lblTotal);
        pnlRodape.Controls.Add(pnlTotais);

        // ordem de docking: rodapé (bottom), cliente (top), inclusão (top), grid (fill)
        Corpo.Controls.Add(cartaoGrid);
        Corpo.Controls.Add(_pnlInclusao);
        Corpo.Controls.Add(pnlCliente);
        Corpo.Controls.Add(pnlRodape);
        cartaoGrid.BringToFront();
        pnlCliente.TabIndex = 0; _pnlInclusao.TabIndex = 1; cartaoGrid.TabIndex = 2; pnlRodape.TabIndex = 3;

        // ---------- Botões do rodapé ----------
        _btnFinalizar = new BotaoModerno("Finalizar venda (F10)", EstiloBotao.Primario, Finalizar);
        _btnOrcamento = new BotaoModerno("Salvar orçamento", EstiloBotao.Secundario, SalvarOrcamento);
        _btnImprimir = new BotaoModerno("Imprimir", EstiloBotao.Secundario, Imprimir) { Visible = _vendaId > 0 };
        Rodape.Controls.Add(_btnFinalizar);
        Rodape.Controls.Add(_btnOrcamento);
        Rodape.Controls.Add(_btnImprimir);

        // ---------- Eventos ----------
        _txtCodigo.TextChanged += (_, _) => { if (!_definindoCodigo && _produto != null) { _produto = null; AtualizarInfoProduto(); } };
        _txtCodigo.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SelecionarPorCodigo(); }
            else if (e.KeyCode == Keys.Down && _grid.Rows.Count > 0) { e.Handled = true; _grid.Focus(); }
        };
        foreach (var campo in new[] { _txtQtd, _txtPreco, _txtDescItem })
            campo.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Adicionar(); } };
        _txtDesconto.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _cbForma.Focus(); } };
        _txtDesconto.TextChanged += (_, _) => AtualizarTotais();
        _grid.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete && !_somenteLeitura) { e.Handled = true; RemoverItem(); }
        };
        _cbForma.SelectedIndexChanged += (_, _) => AtualizarPagamento();
        FormClosing += AoFechar;
        Shown += (_, _) => { if (!_somenteLeitura) _txtCodigo.Focus(); else BtnCancelar.Focus(); };

        // ---------- Carga ----------
        if (venda != null)
        {
            foreach (var i in venda.Itens) _itens.Add(Copiar(i));
            _txtDesconto.Valor = venda.Desconto;
            _nudParcelas.Value = Math.Clamp(venda.Parcelas, 1, 24);
            _txtObs.Text = venda.Observacoes;
            if (venda.ClienteId is int idCliente)
            {
                var cli = ClienteRepository.Obter(idCliente);
                if (cli != null) DefinirCliente(cli);
                else _lblCliente.Text = venda.ClienteNome;
            }
        }
        AtualizarPagamento();
        AtualizarGrid();
        AtualizarInfoProduto();
        if (_somenteLeitura) AplicarSomenteLeitura();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // F2/F10 tratados aqui (e não no KeyDown) para o F10 não ativar o menu do sistema
        if (keyData == Keys.F2) { BuscarProduto(); return true; }
        if (keyData == Keys.F10) { Finalizar(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void AplicarSomenteLeitura()
    {
        _pnlInclusao.Enabled = false;
        _btnSelCliente.Visible = _btnLimparCliente.Visible = false;
        _cbForma.Enabled = _nudParcelas.Enabled = _dtVenc.Enabled = false;
        _txtDesconto.ReadOnly = true;
        _txtObs.ReadOnly = true;
        _btnFinalizar.Visible = _btnOrcamento.Visible = false;
        BtnCancelar.Text = "Fechar";
        _lblProduto.Text = "";
    }

    // ---------- Cliente ----------

    private void SelecionarCliente()
    {
        var c = Seletor.Cliente();
        if (c != null) DefinirCliente(c);
        _txtCodigo.Focus();
    }

    private void DefinirCliente(Cliente? c)
    {
        _cliente = c;
        _lblCliente.Text = c?.Nome ?? "Consumidor final";
        _lblClienteInfo.Text = "";
        if (c == null) return;
        string info = c.DocumentoFormatado;
        if (c.LimiteCredito > 0)
        {
            decimal saldo = 0;
            Mensagem.Tentar(() => saldo = ClienteRepository.SaldoDevedor(c.Id));
            info += (info.Length > 0 ? "  •  " : "") + $"Limite disponível: {Formato.Moeda(Math.Max(0, c.LimiteCredito - saldo))}";
        }
        _lblClienteInfo.Text = info;
    }

    // ---------- Inclusão de itens ----------

    private void BuscarProduto()
    {
        if (_somenteLeitura) return;
        var p = Seletor.Produto();
        if (p != null) { DefinirProduto(p); FocarQuantidade(); }
        else _txtCodigo.Focus();
    }

    private void SelecionarPorCodigo()
    {
        if (_somenteLeitura) return;
        if (_txtCodigo.Text.Trim().Length == 0) return;
        if (_produto != null) { FocarQuantidade(); return; }
        var p = ComercialUi.ResolverProduto(_txtCodigo.Text);
        if (p != null) { DefinirProduto(p); FocarQuantidade(); }
        else { _txtCodigo.Focus(); _txtCodigo.SelectAll(); }
    }

    private void FocarQuantidade()
    {
        _txtQtd.Focus();
        _txtQtd.SelectAll();
    }

    private void DefinirProduto(Produto p)
    {
        _produto = p;
        _definindoCodigo = true;
        _txtCodigo.Text = p.Codigo;
        _definindoCodigo = false;
        _txtPreco.Valor = p.PrecoVenda;
        _txtDescItem.Valor = 0;
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
        decimal naVenda = _itens.Where(i => i.ProdutoId == p.Id).Sum(i => i.Quantidade);
        _lblProduto.ForeColor = p.Estoque <= 0 ? Tema.Perigo : Tema.Texto;
        _lblProduto.Text = $"{p.Descricao}   •   Preço {Formato.Moeda(p.PrecoVenda)}   •   Estoque disponível: {Formato.Qtd(p.Estoque)} {p.Unidade}" +
                           (naVenda > 0 ? $"   •   Já na venda: {Formato.Qtd(naVenda)}" : "");
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
        if (!_txtPreco.Valido || _txtPreco.Valor < 0) { Mensagem.Aviso("Preço unitário inválido."); _txtPreco.Focus(); return; }
        if (!_txtDescItem.Valido || _txtDescItem.Valor < 0) { Mensagem.Aviso("Desconto do item inválido."); _txtDescItem.Focus(); return; }

        decimal qtd = Math.Round(_txtQtd.Valor, 3);
        decimal preco = Formato.Arredondar(_txtPreco.Valor);
        decimal desc = Formato.Arredondar(_txtDescItem.Valor);

        var existente = _itens.FirstOrDefault(i => i.ProdutoId == p.Id);
        decimal novaQtd = (existente?.Quantidade ?? 0) + qtd;
        decimal precoItem = existente?.PrecoUnitario ?? preco;
        decimal novoDesc = (existente?.Desconto ?? 0) + desc;
        if (novoDesc > novaQtd * precoItem)
        {
            Mensagem.Aviso("O desconto do item não pode ser maior que o valor do item.");
            _txtDescItem.Focus();
            return;
        }

        if (existente != null)
        {
            existente.Quantidade = novaQtd;
            existente.Desconto = novoDesc;
            existente.Total = Formato.Arredondar(novaQtd * precoItem - novoDesc);
        }
        else
        {
            existente = new VendaItem
            {
                ProdutoId = p.Id, ProdutoCodigo = p.Codigo, ProdutoDescricao = p.Descricao, Unidade = p.Unidade,
                Quantidade = qtd, PrecoUnitario = preco, Desconto = desc, CustoUnitario = p.PrecoCusto,
                Total = Formato.Arredondar(qtd * preco - desc)
            };
            _itens.Add(existente);
        }

        AtualizarGrid(existente);

        if (novaQtd > p.Estoque)
            Mensagem.Aviso($"A quantidade de \"{p.Descricao}\" na venda ({Formato.Qtd(novaQtd)} {p.Unidade}) é maior que o estoque " +
                           $"disponível ({Formato.Qtd(p.Estoque)} {p.Unidade}).\n\nA venda não poderá ser finalizada sem saldo suficiente.");

        LimparInclusao();
    }

    private void LimparInclusao()
    {
        _produto = null;
        _definindoCodigo = true;
        _txtCodigo.Text = "";
        _definindoCodigo = false;
        _txtQtd.Valor = 1;
        _txtPreco.Valor = 0;
        _txtDescItem.Valor = 0;
        AtualizarInfoProduto();
        _txtCodigo.Focus();
    }

    private void RemoverItem()
    {
        if (_somenteLeitura) return;
        var item = _grid.Selecionado<VendaItem>();
        if (item == null) { Mensagem.Info("Selecione o item a remover."); return; }
        int linha = _grid.CurrentRow?.Index ?? 0;
        _itens.Remove(item);
        AtualizarGrid();
        if (_grid.Rows.Count > 0)
        {
            linha = Math.Min(linha, _grid.Rows.Count - 1);
            _grid.CurrentCell = _grid.Rows[linha].Cells[0];
            _grid.Focus();
        }
        else _txtCodigo.Focus();
        AtualizarInfoProduto();
    }

    private void AtualizarGrid(VendaItem? selecionar = null)
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

    private void AtualizarTotais()
    {
        decimal subtotal = Subtotal;
        decimal desconto = _txtDesconto.Valido ? _txtDesconto.Valor : 0;
        decimal total = Formato.Arredondar(subtotal - desconto);
        _lblSubtotal.Text = Formato.Moeda(subtotal);
        _lblTotal.Text = Formato.Moeda(total);
        _lblTotal.ForeColor = desconto < 0 || desconto > subtotal || !_txtDesconto.Valido ? Tema.Perigo : Tema.Primaria;
        decimal qtd = _itens.Sum(i => i.Quantidade);
        _lblQtdItens.Text = _itens.Count == 0 ? "" : $"{_itens.Count} {(_itens.Count == 1 ? "item" : "itens")}  •  {Formato.Qtd(qtd)} unid.";
    }

    private string FormaSelecionada => _cbForma.SelectedItem as string ?? FormasPagamento.Dinheiro;

    private void AtualizarPagamento()
    {
        bool aVista = FormasPagamento.EhAVista(FormaSelecionada);
        if (aVista) _nudParcelas.Value = 1;
        _nudParcelas.Enabled = !aVista && !_somenteLeitura;
        _dtVenc.Enabled = !aVista && !_somenteLeitura;
    }

    // ---------- Gravação ----------

    private static VendaItem Copiar(VendaItem i) => new()
    {
        ProdutoId = i.ProdutoId, ProdutoCodigo = i.ProdutoCodigo, ProdutoDescricao = i.ProdutoDescricao, Unidade = i.Unidade,
        Quantidade = i.Quantidade, PrecoUnitario = i.PrecoUnitario, Desconto = i.Desconto, Total = i.Total, CustoUnitario = i.CustoUnitario
    };

    private Venda MontarVenda()
    {
        if (_itens.Count == 0) throw new RegraException("Inclua pelo menos um item na venda.");
        if (!_txtDesconto.Valido) throw new RegraException("O desconto geral informado é inválido.");
        return new Venda
        {
            Id = _vendaId,
            ClienteId = _cliente?.Id,
            Desconto = Formato.Arredondar(_txtDesconto.Valor),
            FormaPagamento = FormaSelecionada,
            Parcelas = (int)_nudParcelas.Value,
            Observacoes = _txtObs.Text.Trim(),
            Itens = _itens.Select(Copiar).ToList()
        };
    }

    private void Finalizar()
    {
        if (_somenteLeitura) return;
        Venda? v = null;
        if (!Mensagem.Tentar(() => v = MontarVenda()) || v == null) { if (_itens.Count == 0) _txtCodigo.Focus(); return; }

        bool aVista = FormasPagamento.EhAVista(v.FormaPagamento);
        decimal total = Formato.Arredondar(Subtotal - v.Desconto);
        DateTime? venc = aVista ? null : _dtVenc.Value.Date;
        string pagamento = ComercialUi.TextoPagamento(v.FormaPagamento, v.Parcelas, venc);

        if (!ComercialUi.Perguntar($"Confirma a finalização da venda?\n\nCliente: {_lblCliente.Text}\nItens: {_itens.Count}\n" +
                                    $"Total: {Formato.Moeda(total)}\nPagamento: {pagamento}"))
            return;

        decimal troco = 0;
        if (v.FormaPagamento == FormasPagamento.Dinheiro && total > 0)
        {
            using var d = new DialogoValorRecebido(total);
            if (!d.Abrir(this)) return;
            troco = d.Troco;
        }

        int id = 0;
        if (!Mensagem.Tentar(() => id = VendaService.Finalizar(v, venc))) return;
        Gravou = true;

        string msg = $"Venda nº {id} finalizada com sucesso." + (troco > 0 ? $"\n\nTroco: {Formato.Moeda(troco)}" : "") +
                     "\n\nDeseja imprimir o comprovante?";
        if (ComercialUi.Perguntar(msg)) Mensagem.Tentar(() => ComprovanteVenda.Visualizar(id, this));

        DialogResult = DialogResult.OK;
        Close();
    }

    private void SalvarOrcamento()
    {
        if (_somenteLeitura) return;
        Venda? v = null;
        if (!Mensagem.Tentar(() => v = MontarVenda()) || v == null) return;
        int id = 0;
        if (!Mensagem.Tentar(() => id = VendaService.SalvarOrcamento(v))) return;
        Gravou = true;
        if (Mensagem.Confirmar($"Orçamento nº {id} salvo com sucesso.\n\nDeseja imprimir o orçamento?"))
            Mensagem.Tentar(() => ComprovanteVenda.Visualizar(id, this));
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Imprimir()
    {
        if (_vendaId <= 0) return;
        Mensagem.Tentar(() => ComprovanteVenda.Visualizar(_vendaId, this));
    }

    private void AoFechar(object? sender, FormClosingEventArgs e)
    {
        if (_somenteLeitura || Gravou || DialogResult == DialogResult.OK || _itens.Count == 0) return;
        if (e.CloseReason != CloseReason.UserClosing) return;
        string texto = _vendaId > 0
            ? "Descartar as alterações deste orçamento e fechar?"
            : "Há itens incluídos nesta venda. Deseja descartá-la e fechar?";
        if (!Mensagem.Confirmar(texto)) e.Cancel = true;
    }
}
