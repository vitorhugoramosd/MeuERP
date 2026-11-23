using System.Drawing.Drawing2D;

namespace MeuERP.UI;

/// <summary>Cores, fontes e utilitários visuais do sistema.</summary>
public static class Tema
{
    public static readonly Color Sidebar = ColorTranslator.FromHtml("#111827");
    public static readonly Color SidebarHover = ColorTranslator.FromHtml("#1F2937");
    public static readonly Color SidebarTexto = ColorTranslator.FromHtml("#D1D5DB");
    public static readonly Color SidebarSecao = ColorTranslator.FromHtml("#6B7280");

    public static readonly Color Fundo = ColorTranslator.FromHtml("#F3F4F6");
    public static readonly Color Cartao = Color.White;
    public static readonly Color Borda = ColorTranslator.FromHtml("#E5E7EB");
    public static readonly Color Texto = ColorTranslator.FromHtml("#111827");
    public static readonly Color TextoSuave = ColorTranslator.FromHtml("#6B7280");
    public static readonly Color Cabecalho = ColorTranslator.FromHtml("#F9FAFB");
    public static readonly Color Selecao = ColorTranslator.FromHtml("#DBEAFE");

    public static readonly Color Primaria = ColorTranslator.FromHtml("#2563EB");
    public static readonly Color PrimariaHover = ColorTranslator.FromHtml("#1D4ED8");
    public static readonly Color Sucesso = ColorTranslator.FromHtml("#16A34A");
    public static readonly Color Perigo = ColorTranslator.FromHtml("#DC2626");
    public static readonly Color Aviso = ColorTranslator.FromHtml("#D97706");
    public static readonly Color Roxo = ColorTranslator.FromHtml("#7C3AED");

    public static readonly Font Fonte = new("Segoe UI", 9.5f);
    public static readonly Font FonteNegrito = new("Segoe UI Semibold", 9.5f);
    public static readonly Font FontePequena = new("Segoe UI", 8.5f);
    public static readonly Font FonteTitulo = new("Segoe UI Semibold", 16f);
    public static readonly Font FonteSubtitulo = new("Segoe UI Semibold", 11f);
    public static readonly Font FonteKpi = new("Segoe UI Semibold", 18f);
    public static readonly Font FonteIcone = new("Segoe MDL2 Assets", 11f);

    public static GraphicsPath Arredondado(Rectangle r, int raio)
    {
        int d = raio * 2;
        var path = new GraphicsPath();
        if (d <= 0 || r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Cor associada a um status (ABERTO, PAGO, VENCIDO, CANCELADO, FINALIZADA, etc.).</summary>
    public static Color CorStatus(string? status) => status switch
    {
        "PAGO" or "FINALIZADA" or "RECEBIDA" or "ENTRADA" => Sucesso,
        "VENCIDO" or "CANCELADO" or "CANCELADA" or "SAIDA" => Perigo,
        "ABERTO" or "PEDIDO" or "ORCAMENTO" or "AJUSTE" => Aviso,
        _ => Texto
    };

    /// <summary>Texto amigável para os códigos de status gravados no banco.</summary>
    public static string TextoStatus(string? status) => status switch
    {
        "ORCAMENTO" => "Orçamento",
        "FINALIZADA" => "Finalizada",
        "CANCELADA" => "Cancelada",
        "PEDIDO" => "Pedido",
        "RECEBIDA" => "Recebida",
        "ABERTO" => "Em aberto",
        "PAGO" => "Pago",
        "CANCELADO" => "Cancelado",
        "VENCIDO" => "Vencido",
        "ENTRADA" => "Entrada",
        "SAIDA" => "Saída",
        "AJUSTE" => "Ajuste",
        "ADMIN" => "Administrador",
        "GERENTE" => "Gerente",
        "OPERADOR" => "Operador",
        _ => status ?? ""
    };
}

public static class Mensagem
{
    public static void Info(string texto) => MessageBox.Show(texto, "MeuERP", MessageBoxButtons.OK, MessageBoxIcon.Information);
    public static void Aviso(string texto) => MessageBox.Show(texto, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    public static void Erro(string texto) => MessageBox.Show(texto, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
    public static bool Confirmar(string texto) =>
        MessageBox.Show(texto, "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    /// <summary>Executa a ação tratando erros: RegraException vira aviso, o resto vira mensagem de erro. Retorna true se deu certo.</summary>
    public static bool Tentar(Action acao)
    {
        try { acao(); return true; }
        catch (Nucleo.RegraException ex) { Aviso(ex.Message); return false; }
        catch (Exception ex) { Erro("Ocorreu um erro inesperado:\n\n" + ex.Message); return false; }
    }
}
