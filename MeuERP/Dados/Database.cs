using System.Data;
using System.Globalization;
using Dapper;
using MeuERP.Models;
using MeuERP.Nucleo;
using Microsoft.Data.Sqlite;

namespace MeuERP.Dados;

/// <summary>
/// Banco SQLite embutido. O arquivo fica em %LOCALAPPDATA%\MeuERP\meuerp.db e é criado
/// (com esquema e dados iniciais) na primeira execução.
/// Convenções: colunas snake_case mapeadas para propriedades PascalCase; datas gravadas como
/// texto "yyyy-MM-dd HH:mm:ss"; valores decimais gravados como REAL.
/// </summary>
public static class Database
{
    public static string Caminho { get; private set; } = "";
    private static string _connString = "";
    private static bool _dapperConfigurado;

    public static string PastaPadrao =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeuERP");

    public static void Inicializar(string? caminho = null)
    {
        Caminho = caminho ?? Path.Combine(PastaPadrao, "meuerp.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Caminho)!);
        _connString = new SqliteConnectionStringBuilder { DataSource = Caminho, ForeignKeys = true }.ToString();

        if (!_dapperConfigurado)
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.RemoveTypeMap(typeof(decimal));
            SqlMapper.RemoveTypeMap(typeof(decimal?));
            SqlMapper.RemoveTypeMap(typeof(DateTime));
            SqlMapper.RemoveTypeMap(typeof(DateTime?));
            SqlMapper.AddTypeHandler(new DecimalHandler());
            SqlMapper.AddTypeHandler(new DateTimeHandler());
            _dapperConfigurado = true;
        }

        using var c = Abrir();
        c.Execute("PRAGMA journal_mode=WAL;");
        c.Execute(Esquema.Sql);
        DadosIniciais(c);
    }

    /// <summary>Abre uma conexão nova (use sempre com <c>using</c>).</summary>
    public static SqliteConnection Abrir()
    {
        var c = new SqliteConnection(_connString);
        c.Open();
        return c;
    }

    public static void EmTransacao(Action<IDbConnection, IDbTransaction> acao)
    {
        using var c = Abrir();
        using var tx = c.BeginTransaction();
        acao(c, tx);
        tx.Commit();
    }

    public static T EmTransacao<T>(Func<IDbConnection, IDbTransaction, T> acao)
    {
        using var c = Abrir();
        using var tx = c.BeginTransaction();
        T r = acao(c, tx);
        tx.Commit();
        return r;
    }

    /// <summary>True se a exceção for violação de chave estrangeira (registro em uso).</summary>
    public static bool EhRegistroEmUso(Exception ex) =>
        ex is SqliteException { SqliteErrorCode: 19 } s && s.SqliteExtendedErrorCode is 787 or 1811;

    /// <summary>True se a exceção for violação de UNIQUE (valor duplicado).</summary>
    public static bool EhDuplicado(Exception ex) =>
        ex is SqliteException { SqliteErrorCode: 19 } s && s.SqliteExtendedErrorCode is 2067 or 1555;

    /// <summary>Copia o banco inteiro (consistente) para o arquivo de destino.</summary>
    public static void Backup(string destino)
    {
        if (File.Exists(destino)) File.Delete(destino);
        using var origem = Abrir();
        using var dest = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destino, Pooling = false }.ToString());
        dest.Open();
        origem.BackupDatabase(dest);
    }

    /// <summary>Substitui o banco atual pelo arquivo de backup informado (valida antes).</summary>
    public static void Restaurar(string arquivoBackup)
    {
        using (var teste = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = arquivoBackup, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            try
            {
                teste.Open();
                long n = teste.ExecuteScalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('usuarios','produtos','vendas')");
                if (n < 3) throw new RegraException("O arquivo selecionado não é um backup válido do MeuERP.");
            }
            catch (SqliteException)
            {
                throw new RegraException("O arquivo selecionado não é um banco de dados válido.");
            }
        }

        SqliteConnection.ClearAllPools();
        string seguranca = Caminho + ".antes-da-restauracao";
        File.Copy(Caminho, seguranca, true);
        foreach (string sufixo in new[] { "-wal", "-shm" })
            if (File.Exists(Caminho + sufixo)) File.Delete(Caminho + sufixo);
        File.Copy(arquivoBackup, Caminho, true);
        Inicializar(Caminho);
    }

    private static void DadosIniciais(SqliteConnection c)
    {
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM usuarios") == 0)
        {
            c.Execute("INSERT INTO usuarios (nome, login, senha_hash, perfil, ativo, criado_em) VALUES (@n, 'admin', @h, @p, 1, @d)",
                new { n = "Administrador", h = Seguranca.GerarHash("admin"), p = Perfis.Admin, d = DateTime.Now });
        }
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM empresa") == 0)
            c.Execute("INSERT INTO empresa (id, razao_social, nome_fantasia) VALUES (1, 'Minha Empresa Ltda', 'Minha Empresa')");
        if (c.ExecuteScalar<long>("SELECT COUNT(*) FROM categorias") == 0)
            c.Execute("INSERT INTO categorias (nome) VALUES ('Geral')");
    }

    private class DecimalHandler : SqlMapper.TypeHandler<decimal>
    {
        public override decimal Parse(object value) => value switch
        {
            double d => (decimal)d,
            long l => l,
            string s => decimal.Parse(s, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };

        public override void SetValue(IDbDataParameter parameter, decimal value)
        {
            parameter.DbType = DbType.Double;
            parameter.Value = (double)value;
        }
    }

    private class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override DateTime Parse(object value) => value switch
        {
            DateTime d => d,
            string s => DateTime.Parse(s, CultureInfo.InvariantCulture),
            _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture)
        };

        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
