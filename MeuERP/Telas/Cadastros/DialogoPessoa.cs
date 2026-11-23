using MeuERP.Dados;
using MeuERP.Models;
using MeuERP.Nucleo;
using MeuERP.UI;

namespace MeuERP.Telas.Cadastros;

/// <summary>Diálogo compartilhado de cliente e fornecedor (o tipo do objeto define os campos extras).</summary>
public class DialogoPessoa : DialogoBase
{
    private readonly Pessoa _p;
    private readonly ComboBox _cmbTipo, _cmbUf;
    private readonly TextBox _txtNome, _txtFantasia, _txtDocumento, _txtRg, _txtTelefone, _txtEmail,
        _txtCep, _txtEndereco, _txtNumero, _txtBairro, _txtCidade, _txtObs;
    private readonly TextBox? _txtContato;
    private readonly CampoDecimal? _txtLimite;
    private readonly CheckBox _chkAtivo;
    private readonly Label _rotNome, _rotDocumento, _rotRg;

    private static readonly string[] Tipos = { "Física", "Jurídica" };

    // 6 linhas de 56px + observações (96) + cabeçalho 52 + rodapé 58 + margens 30
    public DialogoPessoa(Pessoa pessoa)
        : base(Titulo(pessoa), 740, 6 * 56 + 96 + 52 + 58 + 30)
    {
        _p = pessoa;
        bool ehCliente = pessoa is Cliente;

        var f = new PainelFormulario(4);
        _cmbTipo = f.Combo("Tipo de pessoa", 1);
        _txtNome = f.Texto("Nome", 3, 150);
        _txtFantasia = f.Texto("Nome fantasia", 2, 150);
        _txtDocumento = f.Texto("CPF", 1, 18);
        _txtRg = f.Texto("RG", 1, 30);
        _txtTelefone = f.Texto("Telefone", 1, 30);
        _txtEmail = f.Texto("E-mail", 2, 120);
        if (pessoa is Cliente)
            _txtLimite = f.Decimal("Limite de crédito (R$)");
        else
            _txtContato = f.Texto("Contato", 1, 80);
        _txtCep = f.Texto("CEP", 1, 10);
        _txtEndereco = f.Texto("Endereço", 2, 150);
        _txtNumero = f.Texto("Número", 1, 20);
        _txtBairro = f.Texto("Bairro", 1, 80);
        _txtCidade = f.Texto("Cidade", 2, 80);
        _cmbUf = f.Combo("UF", 1);
        _txtObs = f.Multilinha("Observações", 4);
        _chkAtivo = f.Check("Ativo", 1, true);
        if (ehCliente && pessoa.Id != 0)
        {
            decimal saldo = ClienteRepository.SaldoDevedor(pessoa.Id);
            var lbl = f.Rotulo("Saldo devedor atual (contas a receber em aberto)", 3, Formato.Moeda(saldo));
            lbl.Font = Tema.FonteNegrito;
            lbl.ForeColor = saldo > 0 ? Tema.Perigo : Tema.Sucesso;
        }
        Corpo.Controls.Add(f);

        _rotNome = RotuloDe(_txtNome);
        _rotDocumento = RotuloDe(_txtDocumento);
        _rotRg = RotuloDe(_txtRg);

        _cmbTipo.CarregarTextos(Tipos, _p.TipoPessoa == "J" ? Tipos[1] : Tipos[0]);
        _cmbUf.CarregarTextos(new[] { "" }.Concat(Ufs.Todas), _p.Uf);
        _txtNome.Text = _p.Nome;
        _txtFantasia.Text = _p.NomeFantasia;
        _txtDocumento.Text = Formato.Documento(_p.Documento);
        _txtRg.Text = _p.InscricaoRg;
        _txtTelefone.Text = _p.Telefone;
        _txtEmail.Text = _p.Email;
        _txtCep.Text = _p.Cep;
        _txtEndereco.Text = _p.Endereco;
        _txtNumero.Text = _p.Numero;
        _txtBairro.Text = _p.Bairro;
        _txtCidade.Text = _p.Cidade;
        _txtObs.Text = _p.Observacoes;
        _chkAtivo.Checked = _p.Ativo;
        if (_p is Cliente c && _txtLimite != null) _txtLimite.Valor = c.LimiteCredito;
        if (_p is Fornecedor fo && _txtContato != null) _txtContato.Text = fo.Contato;

        AtualizarRotulos();
        _cmbTipo.SelectedIndexChanged += (_, _) => AtualizarRotulos();
        _txtDocumento.Leave += (_, _) => _txtDocumento.Text = Formato.Documento(_txtDocumento.Text.Trim());
        _txtCep.Leave += (_, _) =>
        {
            string d = Formato.SoDigitos(_txtCep.Text);
            if (d.Length == 8) _txtCep.Text = d[..5] + "-" + d[5..];
        };

        Shown += (_, _) => _cmbTipo.Focus();
    }

    private static string Titulo(Pessoa p) => p switch
    {
        Cliente => p.Id == 0 ? "Novo cliente" : "Editar cliente",
        _ => p.Id == 0 ? "Novo fornecedor" : "Editar fornecedor"
    };

    private bool Juridica => _cmbTipo.SelectedIndex == 1;

    private void AtualizarRotulos()
    {
        _rotNome.Text = Juridica ? "Razão social" : "Nome";
        _rotDocumento.Text = Juridica ? "CNPJ" : "CPF";
        _rotRg.Text = Juridica ? "Inscrição estadual" : "RG";
    }

    /// <summary>Rótulo criado pelo PainelFormulario acima do controle (mesma célula).</summary>
    private static Label RotuloDe(Control c) => c.Parent!.Controls.OfType<Label>().First(l => l != c);

    protected override void AoSalvar()
    {
        if (_txtLimite != null && !_txtLimite.Valido)
        {
            _txtLimite.Focus();
            throw new RegraException("Limite de crédito inválido.");
        }
        string doc = Formato.SoDigitos(_txtDocumento.Text);
        if (doc.Length > 0 && doc.Length != (Juridica ? 14 : 11))
        {
            _txtDocumento.Focus();
            throw new RegraException(Juridica ? "O CNPJ deve ter 14 dígitos." : "O CPF deve ter 11 dígitos.");
        }

        _p.TipoPessoa = Juridica ? "J" : "F";
        _p.Nome = _txtNome.Text;
        _p.NomeFantasia = _txtFantasia.Text.Trim();
        _p.Documento = doc;
        _p.InscricaoRg = _txtRg.Text.Trim();
        _p.Telefone = _txtTelefone.Text.Trim();
        _p.Email = _txtEmail.Text.Trim();
        _p.Cep = _txtCep.Text.Trim();
        _p.Endereco = _txtEndereco.Text.Trim();
        _p.Numero = _txtNumero.Text.Trim();
        _p.Bairro = _txtBairro.Text.Trim();
        _p.Cidade = _txtCidade.Text.Trim();
        _p.Uf = _cmbUf.SelectedItem as string ?? "";
        _p.Observacoes = _txtObs.Text.Trim();
        _p.Ativo = _chkAtivo.Checked;

        switch (_p)
        {
            case Cliente c:
                c.LimiteCredito = _txtLimite?.Valor ?? 0;
                ClienteRepository.Salvar(c);
                break;
            case Fornecedor fo:
                fo.Contato = _txtContato?.Text.Trim() ?? "";
                FornecedorRepository.Salvar(fo);
                break;
        }
    }
}
