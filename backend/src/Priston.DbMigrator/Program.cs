using System.Text.RegularExpressions;
using DbUp;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Priston.Application.Admin;

// Aplica as migrações versionadas (database/migrations) no banco do PORTAL.
// Uso: Priston.DbMigrator [--ConnectionStrings:PortalDbDeploy="..."] [--create-database] [--criar-superadmin <usuario>]
// --criar-superadmin pede a senha no próprio terminal do servidor (sem eco). Ela nunca passa por linha de comando, chat ou log.
// A connection string usa o login de deploy (com DDL), que só existe durante a publicação.

var superIndex = Array.IndexOf(args, "--criar-superadmin");
var superUser = superIndex >= 0 && superIndex + 1 < args.Length ? args[superIndex + 1] : null;
if (superIndex >= 0 && superUser is null)
{
    Console.Error.WriteLine("Informe o usuário: --criar-superadmin <usuario>");
    return 2;
}

var configArgs = args.Where((a, i) => a != "--create-database" && i != superIndex && i != superIndex + 1).ToArray();
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "PRISTON_")
    .AddCommandLine(configArgs)
    .Build();

var connectionString = configuration.GetConnectionString("PortalDbDeploy");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Defina ConnectionStrings:PortalDbDeploy (variável PRISTON_ConnectionStrings__PortalDbDeploy).");
    return 2;
}

if (args.Contains("--create-database"))
{
    EnsureDatabase.For.SqlDatabase(connectionString);
}

var upgrader = DeployChanges.To
    .SqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly, name => name.Contains(".migrations.", StringComparison.Ordinal))
    .WithTransactionPerScript()
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
if (!result.Successful)
{
    Console.Error.WriteLine($"Falha na migração: {result.ErrorScript?.Name}");
    return 1;
}

Console.WriteLine("Migrações aplicadas.");
return superUser is null ? 0 : await CreateSuperAdminAsync(connectionString, superUser);

static async Task<int> CreateSuperAdminAsync(string connectionString, string username)
{
    if (!Regex.IsMatch(username, "^[A-Za-z0-9_.-]{3,32}$"))
    {
        Console.Error.WriteLine("Usuário inválido: use de 3 a 32 letras, números, ponto, hífen ou sublinhado.");
        return 2;
    }

    var password = ReadSecret("Senha do superadministrador: ");
    if (StaffPassword.Validate(password, username) is { } error)
    {
        Console.Error.WriteLine(error);
        return 2;
    }

    if (ReadSecret("Repita a senha: ") != password)
    {
        Console.Error.WriteLine("As senhas não conferem.");
        return 2;
    }

    await using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        IF EXISTS (SELECT 1 FROM dbo.StaffUser WHERE Username = @u) SELECT 0;
        ELSE BEGIN
            INSERT INTO dbo.StaffUser (Username, DisplayName, PasswordHash, Role, MustChangePassword, CreatedBy)
            VALUES (@u, @u, @h, 'superadmin', 0, 'instalacao');
            INSERT INTO dbo.AuditLog (ActorType, ActorId, Action, TargetType, TargetId) VALUES ('system', 'instalacao', 'staff.created', 'staff', @u);
            SELECT 1;
        END
        """;
    cmd.Parameters.Add(new SqlParameter("@u", System.Data.SqlDbType.VarChar, 32) { Value = username });
    cmd.Parameters.Add(new SqlParameter("@h", System.Data.SqlDbType.VarChar, 200) { Value = StaffPassword.Hash(password) });
    if ((int)(await cmd.ExecuteScalarAsync())! == 0)
    {
        Console.Error.WriteLine("Este usuário já existe. Para trocar a senha, use o painel.");
        return 1;
    }

    Console.WriteLine($"Superadministrador {username} criado.");
    return 0;
}

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    if (Console.IsInputRedirected)
    {
        var line = Console.ReadLine() ?? string.Empty;
        Console.WriteLine();
        return line;
    }

    var buffer = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (buffer.Length > 0)
            {
                buffer.Length--;
            }
        }
        else if (!char.IsControl(key.KeyChar))
        {
            buffer.Append(key.KeyChar);
        }
    }

    Console.WriteLine();
    return buffer.ToString();
}
