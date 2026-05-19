using System;
using System.Text;

namespace ERP.WPF.Helpers;

/// <summary>
/// Gera o payload do QR Code PIX estático (padrão EMV/BRCode do Banco Central).
/// Não precisa de API externa — funciona offline.
/// </summary>
public static class PixQrCodeService
{
    /// <summary>
    /// Monta o payload PIX para geração do QR Code.
    /// </summary>
    /// <param name="chavePix">Chave PIX (CPF, CNPJ, e-mail, celular ou chave aleatória)</param>
    /// <param name="nomeBeneficiario">Nome do recebedor (máx 25 caracteres)</param>
    /// <param name="cidade">Cidade do recebedor (máx 15 caracteres)</param>
    /// <param name="valor">Valor da transação (0 = qualquer valor)</param>
    /// <param name="txid">Identificador da transação (máx 25 chars, sem espaços)</param>
    public static string GerarPayload(
        string chavePix,
        string nomeBeneficiario,
        string cidade,
        decimal valor = 0,
        string txid = "***")
    {
        // Limpa espaços e formatações indesejadas que o cliente possa ter digitado (ex: traços e parênteses)
    chavePix = chavePix.Trim().Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

    // Se a chave só tem números, tem 11 dígitos e não é um CPF válido,
    // ou se você tem um campo "TipoChave" no banco, force a adição do +55
    // Aqui vou colocar uma regra simples: se o cliente digitou um celular puro, a gente arruma.
    if (chavePix.Length == 11 && ehProvavelCelular(chavePix)) 
    {
         // Dica: Para o seu ERP, o ideal é ter um Enum "TipoChavePix" nas configurações 
         // para você saber com certeza se esses 11 dígitos são CPF ou Celular.
         chavePix = "+55" + chavePix;
    }
    else if (chavePix.Length == 12 || chavePix.Length == 13) // Caso o cliente digitou 55419... sem o +
    {
         if (!chavePix.StartsWith("+"))
             chavePix = "+" + chavePix;
    }
        nomeBeneficiario = Limpar(nomeBeneficiario, 25);
        cidade           = Limpar(cidade, 15);
        txid             = LimparTxId(txid, 25);

        // Merchant Account Info (MAI)
        string gui  = Campo("00", "BR.GOV.BCB.PIX");
        string key  = Campo("01", chavePix);
        string mai  = Campo("26", gui + key);

        // Transaction Amount
        string amount = valor > 0
            ? Campo("54", valor.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
            : string.Empty;

        // Additional data
        string addData = Campo("62", Campo("05", txid));

        // Payload sem CRC
        string payload =
            Campo("00", "01") +   // Payload format indicator
            mai +
            Campo("52", "0000") + // MCC
            Campo("53", "986") +  // BRL
            amount +
            Campo("58", "BR") +   // Country
            Campo("59", nomeBeneficiario) +
            Campo("60", cidade) +
            addData +
            "6304";               // CRC placeholder

        return payload + Crc16Ccitt(payload);
    }

    // ── Validações de Tipo de Chave ────────────────────────────────────────

    private static bool ehProvavelCelular(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) || chave.Length != 11) 
            return false;

        // Se a matemática disser que é um CPF válido, não é celular
        if (IsCpf(chave)) 
            return false;

        // Se tem 11 dígitos, não é CPF, e o 3º dígito é '9' (Ex: 41 9 8438...), é celular!
        return chave[2] == '9';
    }

    private static bool IsCpf(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;
        
        cpf = cpf.Trim().Replace(".", "").Replace("-", "");
        if (cpf.Length != 11) return false;

        // Rejeita CPFs inválidos conhecidos (tudo igual)
        if (new string(cpf[0], 11) == cpf) return false;

        int[] multiplicador1 = new int[9] { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] multiplicador2 = new int[10] { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        
        string tempCpf = cpf.Substring(0, 9);
        int soma = 0;

        for (int i = 0; i < 9; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador1[i];

        int resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;

        string digito = resto.ToString();
        tempCpf = tempCpf + digito;
        soma = 0;

        for (int i = 0; i < 10; i++)
            soma += int.Parse(tempCpf[i].ToString()) * multiplicador2[i];

        resto = soma % 11;
        resto = resto < 2 ? 0 : 11 - resto;

        digito = digito + resto.ToString();
        return cpf.EndsWith(digito);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static string Campo(string id, string value)
        => $"{id}{value.Length:D2}{value}";

    private static string Limpar(string s, int max)
    {
        s = s.Trim()
             .Replace("ã","a").Replace("ç","c").Replace("é","e")
             .Replace("á","a").Replace("ó","o").Replace("ú","u")
             .Replace("â","a").Replace("ê","e").Replace("ô","o")
             .Replace("Ã","A").Replace("Ç","C").Replace("É","E")
             .Replace("Á","A").Replace("Ó","O").Replace("Ú","U");
        return s.Length > max ? s.Substring(0, max) : s;
    }

    private static string LimparTxId(string s, int max)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        string r = sb.Length == 0 ? "ERP" : sb.ToString();
        return r.Length > max ? r.Substring(0, max) : r;
    }

    private static string Crc16Ccitt(string payload)
    {
        ushort crc = 0xFFFF;
        foreach (char c in Encoding.UTF8.GetBytes(payload))
        {
            crc ^= (ushort)(c << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }
}
