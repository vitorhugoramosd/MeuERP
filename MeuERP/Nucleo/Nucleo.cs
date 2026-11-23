using System.Globalization;
using System.Security.Cryptography;
using MeuERP.Models;

namespace MeuERP.Nucleo;

/// <summary>Erro de regra de negócio/validação: a mensagem é mostrada ao usuário como aviso.</summary>
public class RegraException : Exception
{
    public RegraException(string mensagem) : base(mensagem) { }
}

/// <summary>Usuário logado na sessão atual.</summary>
public static class Sessao
{
    public static Usuario? Usuario { get; set; }
    public static int? UsuarioId => Usuario?.Id;
    public static bool EhAdmin => Usuario?.Perfil == Perfis.Admin;
    /// <summary>Administrador ou gerente.</summary>
    public static bool PodeGerenciar => Usuario?.Perfil is Perfis.Admin or Perfis.Gerente;
}

public static class Seguranca
{
    private const int Iteracoes = 100_000;

    public static string GerarHash(string senha)
    {
        byte[] sal = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(sal) + ":" + Convert.ToBase64String(hash);
    }

    public static bool Verificar(string senha, string hashArmazenado)
    {
        var partes = hashArmazenado.Split(':');
        if (partes.Length != 2) return false;
        try
        {
            byte[] sal = Convert.FromBase64String(partes[0]);
            byte[] esperado = Convert.FromBase64String(partes[1]);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, esperado);
        }
        catch (FormatException) { return false; }
    }
}

public static class Formato
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal v) => v.ToString("C2", PtBr);
    public static string Numero(decimal v, int casas = 2) => v.ToString("N" + casas, PtBr);
    /// <summary>Quantidade sem zeros desnecessários (até 3 casas).</summary>
    public static string Qtd(decimal v) => v.ToString("#,##0.###", PtBr);
    public static string Data(DateTime d) => d.ToString("dd/MM/yyyy", PtBr);
    public static string DataHora(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", PtBr);
    public static string Percentual(decimal v) => v.ToString("N1", PtBr) + "%";

    public static decimal Arredondar(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static bool TentarDecimal(string? texto, out decimal valor)
    {
        texto = (texto ?? "").Replace("R$", "").Trim();
        if (texto.Length == 0) { valor = 0; return true; }
        return decimal.TryParse(texto, NumberStyles.Number, PtBr, out valor);
    }

    public static string SoDigitos(string? s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    public static string Documento(string? doc)
    {
        string d = SoDigitos(doc);
        if (d.Length == 11) return Convert.ToUInt64(d).ToString(@"000\.000\.000\-00");
        if (d.Length == 14) return Convert.ToUInt64(d).ToString(@"00\.000\.000\/0000\-00");
        return doc ?? "";
    }

    public static bool CpfValido(string? cpf)
    {
        string d = SoDigitos(cpf);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;
        for (int j = 9; j < 11; j++)
        {
            int soma = 0;
            for (int i = 0; i < j; i++) soma += (d[i] - '0') * (j + 1 - i);
            int dig = soma * 10 % 11 % 10;
            if (dig != d[j] - '0') return false;
        }
        return true;
    }

    public static bool CnpjValido(string? cnpj)
    {
        string d = SoDigitos(cnpj);
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;
        int[] p1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] p2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int Dig(int[] pesos)
        {
            int soma = 0;
            for (int i = 0; i < pesos.Length; i++) soma += (d[i] - '0') * pesos[i];
            int r = soma % 11;
            return r < 2 ? 0 : 11 - r;
        }
        return Dig(p1) == d[12] - '0' && Dig(p2) == d[13] - '0';
    }

    /// <summary>Valida CPF (11 dígitos) ou CNPJ (14 dígitos). Vazio é considerado válido.</summary>
    public static bool DocumentoValido(string? doc)
    {
        string d = SoDigitos(doc);
        return d.Length switch { 0 => true, 11 => CpfValido(d), 14 => CnpjValido(d), _ => false };
    }
}
