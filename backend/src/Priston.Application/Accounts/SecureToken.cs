using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Priston.Application.Accounts;

/// <summary>Tokens aleatórios de sessão e de uso único. O banco guarda só o SHA-256.</summary>
public static class SecureToken
{
    public static (string Token, byte[] Hash) Create()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var token = Base64Url.EncodeToString(bytes);
        return (token, Hash(token));
    }

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    /// <summary>Formato esperado: 43 caracteres base64url. Evita consultar o banco com lixo.</summary>
    public static bool LooksValid(string? token) =>
        token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
