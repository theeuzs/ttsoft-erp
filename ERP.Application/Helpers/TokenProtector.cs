// ── ERP.Application/Helpers/TokenProtector.cs ───────────────────────────────
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Helpers;

/// <summary>
/// Criptografia portátil pra segredos que precisam ser lidos tanto pelo WPF
/// (Windows) quanto pela API (Linux/Azure) — diferente do CriptografiaService
/// do WPF, que usa DPAPI (ProtectedData.Protect com DataProtectionScope.
/// CurrentUser): isso só funciona no Windows, e só pro mesmo usuário que
/// criptografou, então nunca seria legível do lado do servidor. Mesma postura
/// de segurança já aceita no projeto pra esse tipo de segredo (chave fixa
/// embutida, igual o EntropiaAdicional do CriptografiaService original) —
/// gerenciamento de segredo "de verdade" (Key Vault etc.) fica pra quando
/// houver motivo concreto, não antes.
/// </summary>
public static class TokenProtector
{
    private static readonly byte[] Key =
        SHA256.HashData(Encoding.UTF8.GetBytes("TTSoft_ERP_Fiscal_SharedKey_2026"));

    public static string Proteger(string textoPlano)
    {
        if (string.IsNullOrWhiteSpace(textoPlano)) return textoPlano;

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var bytesTexto = Encoding.UTF8.GetBytes(textoPlano);
        var bytesCifrados = encryptor.TransformFinalBlock(bytesTexto, 0, bytesTexto.Length);

        var resultado = new byte[aes.IV.Length + bytesCifrados.Length];
        Buffer.BlockCopy(aes.IV, 0, resultado, 0, aes.IV.Length);
        Buffer.BlockCopy(bytesCifrados, 0, resultado, aes.IV.Length, bytesCifrados.Length);
        return Convert.ToBase64String(resultado);
    }

    public static string Desproteger(string textoCifrado)
    {
        if (string.IsNullOrWhiteSpace(textoCifrado)) return textoCifrado;

        try
        {
            var todosBytes = Convert.FromBase64String(textoCifrado);

            using var aes = Aes.Create();
            aes.Key = Key;
            var iv = new byte[16];
            Buffer.BlockCopy(todosBytes, 0, iv, 0, 16);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var bytesCifrados = new byte[todosBytes.Length - 16];
            Buffer.BlockCopy(todosBytes, 16, bytesCifrados, 0, bytesCifrados.Length);
            var bytesTexto = decryptor.TransformFinalBlock(bytesCifrados, 0, bytesCifrados.Length);
            return Encoding.UTF8.GetString(bytesTexto);
        }
        catch
        {
            return textoCifrado;
        }
    }
}