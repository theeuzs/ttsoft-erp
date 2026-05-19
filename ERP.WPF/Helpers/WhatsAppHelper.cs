using System;
using System.Diagnostics;
using System.Linq;

namespace ERP.WPF.Helpers;

/// <summary>
/// Helper centralizado para abrir WhatsApp Web no navegador padrão.
/// Usa https://wa.me/ — funciona sem app do WhatsApp instalado no Windows.
/// </summary>
public static class WhatsAppHelper
{
    /// <summary>
    /// Abre o WhatsApp Web com mensagem pré-formatada.
    /// Com telefone → conversa direta | Sem telefone → WhatsApp Web genérico.
    /// </summary>
    public static void Abrir(string mensagem, string? telefone = null)
    {
        var enc = Uri.EscapeDataString(mensagem);
        string url;

        if (!string.IsNullOrWhiteSpace(telefone))
        {
            var numero = new string(telefone.Where(char.IsDigit).ToArray());
            if (numero.Length >= 10)
            {
                if (!numero.StartsWith("55")) numero = "55" + numero;
                url = $"https://wa.me/{numero}?text={enc}";
            }
            else
                url = $"https://web.whatsapp.com/send?text={enc}";
        }
        else
            url = $"https://web.whatsapp.com/send?text={enc}";

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}
