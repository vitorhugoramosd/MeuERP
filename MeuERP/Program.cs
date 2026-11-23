using System.Globalization;
using MeuERP.Dados;
using MeuERP.Nucleo;
using MeuERP.Telas;
using MeuERP.UI;

namespace MeuERP;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var cultura = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;

        // Verificação automática das regras de negócio em um banco temporário (sem interface).
        if (args.Contains("--autoteste")) return AutoTeste.Executar();

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            if (e.Exception is RegraException) Mensagem.Aviso(e.Exception.Message);
            else Mensagem.Erro("Ocorreu um erro inesperado:\n\n" + e.Exception.Message);
        };

        try
        {
            Database.Inicializar();
        }
        catch (Exception ex)
        {
            Mensagem.Erro("Não foi possível abrir o banco de dados:\n\n" + ex.Message);
            return 1;
        }

        while (true)
        {
            Sessao.Usuario = null;
            using (var login = new TelaLogin())
                if (login.ShowDialog() != DialogResult.OK) break;

            using var principal = new TelaPrincipal();
            Application.Run(principal);
            if (!principal.TrocarUsuario) break;
        }
        return 0;
    }
}
