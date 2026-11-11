using Microsoft.Extensions.Configuration;

namespace Priston.Infrastructure.Hosting;

/// <summary>
/// Em produção, connection strings e segredos ficam num arquivo fora da pasta do programa
/// (C:\NexusPortal\config\portal.json), legível só pelos administradores e pelas contas dos serviços.
/// O caminho vem da variável NEXUSPORTAL_CONFIG, gravada no registro de cada serviço pelo instalador.
/// Assim uma nova versão substitui a pasta inteira sem tocar nos segredos.
/// </summary>
public static class PortalConfigFile
{
    public const string EnvironmentVariable = "NEXUSPORTAL_CONFIG";

    /// <summary>Acrescenta o arquivo por último: o que estiver nele vale sobre o appsettings.</summary>
    public static IConfigurationBuilder AddPortalConfigFile(this IConfigurationBuilder builder)
    {
        var path = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return builder;
        }

        if (!File.Exists(path))
        {
            // Variável definida e arquivo ausente é erro de instalação: melhor não subir do que subir sem banco.
            throw new FileNotFoundException($"{EnvironmentVariable} aponta para um arquivo que não existe.", path);
        }

        return builder.AddJsonFile(path, optional: false, reloadOnChange: false);
    }
}
