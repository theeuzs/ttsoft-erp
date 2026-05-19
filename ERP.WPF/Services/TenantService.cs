using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Gera o TenantId da loja a partir do CNPJ na licença.
/// O TenantId é um SHA-256 determinístico do CNPJ limpo —
/// ou seja: o mesmo CNPJ sempre gera o mesmo Guid, em qualquer máquina.
/// </summary>
public static class TenantService
{
    private static Guid? _tenantId;

    public static Guid GetCurrentTenantId()
    {
        if (_tenantId.HasValue) return _tenantId.Value;

        string cnpj = ObterCnpjDaLicenca();
        _tenantId = GerarTenantId(cnpj);
        return _tenantId.Value;
    }

    private static string ObterCnpjDaLicenca()
    {
        string path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "licenca.json");

        if (!File.Exists(path))
            throw new InvalidOperationException(
                "licenca.json não encontrado. Reinstale o sistema.");

        string json = File.ReadAllText(path);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Lê a propriedade 'Cnpj' ou 'cnpj' (case-insensitive)
        string cnpj = string.Empty;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.Equals("cnpj", StringComparison.OrdinalIgnoreCase))
            {
                cnpj = prop.Value.GetString() ?? string.Empty;
                break;
            }
        }

        // Remove formatação: pontos, traços, barras
        return new string(cnpj.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Gera um Guid determinístico a partir do CNPJ usando SHA-256.
    /// Os primeiros 16 bytes do hash formam o Guid.
    /// </summary>
    public static Guid GerarTenantId(string cnpjLimpo)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(cnpjLimpo));
        byte[] guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
