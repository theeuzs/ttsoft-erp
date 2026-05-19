using System.Security.Cryptography;
using System.Text;

namespace ERP.Api.Models;

/// <summary>
/// Gera o TenantId a partir do CNPJ usando o mesmo algoritmo SHA-256 do WPF.
/// Garante que a API resolva o mesmo TenantId que o sistema desktop.
/// </summary>
public static class TenantHelper
{
    public static Guid FromCnpj(string cnpj)
    {
        var cnpjLimpo = new string(cnpj.Where(char.IsDigit).ToArray());
        using var sha = SHA256.Create();
        var hash      = sha.ComputeHash(Encoding.UTF8.GetBytes(cnpjLimpo));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
