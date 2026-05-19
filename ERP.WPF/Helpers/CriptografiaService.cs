using System;
using System.Security.Cryptography;
using System.Text;

namespace ERP.WPF.Helpers;

public static class CriptografiaService
{
    // A chave extra de entropia aumenta ainda mais a segurança
    private static readonly byte[] EntropiaAdicional = Encoding.UTF8.GetBytes("TTSoft_ERP_SecureKey_2026");

    public static string Encriptar(string textoPlano)
    {
        if (string.IsNullOrWhiteSpace(textoPlano)) return textoPlano;

        try
        {
            var bytesTexto = Encoding.UTF8.GetBytes(textoPlano);
            // O DataProtectionScope.CurrentUser garante que só o utilizador atual do Windows consegue ler
            var bytesProtegidos = ProtectedData.Protect(bytesTexto, EntropiaAdicional, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytesProtegidos);
        }
        catch
        {
            return textoPlano; // Em caso de erro extremo, não quebra a aplicação
        }
    }

    public static string Desencriptar(string textoCifrado)
    {
        if (string.IsNullOrWhiteSpace(textoCifrado)) return textoCifrado;

        try
        {
            var bytesProtegidos = Convert.FromBase64String(textoCifrado);
            var bytesTexto = ProtectedData.Unprotect(bytesProtegidos, EntropiaAdicional, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytesTexto);
        }
        catch
        {
            // O Catch é o nosso Salva-Vidas! 
            // Se o sistema tentar desencriptar um token antigo que ainda está em "texto limpo",
            // ele vai dar erro. Neste caso, nós apenas devolvemos o texto original para não quebrar nada!
            return textoCifrado;
        }
    }
}