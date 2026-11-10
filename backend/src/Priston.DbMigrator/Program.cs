using DbUp;
using Microsoft.Extensions.Configuration;

// Aplica as migrações versionadas (database/migrations) no banco do PORTAL.
// Uso: Priston.DbMigrator [--ConnectionStrings:PortalDbDeploy="..."] [--create-database]
// A connection string usa o login de deploy (com DDL), que só existe durante a publicação.

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "PRISTON_")
    .AddCommandLine(args.Where(a => a != "--create-database").ToArray())
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
return 0;
