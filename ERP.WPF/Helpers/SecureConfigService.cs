using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ERP.WPF.Helpers;

/// <summary>
/// Armazena a connection string criptografada em "conexao.dat" ao lado do executável.
/// Usa DPAPI com escopo de MÁQUINA (LocalMachine), então qualquer usuário do PC acessa,
/// mas ninguém de outra máquina consegue ler o arquivo.
/// </summary>
public static class SecureConfigService
{
    // Arquivo fica na mesma pasta do .exe — nunca em um caminho fixo
    private static readonly string CaminhoArquivo =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "conexao.dat");

    // Entropia adicional: dificulta ataques mesmo que alguém copie o .dat
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("ERP-Materiais-2025-Salt");

    /// <summary>Verifica se o arquivo de configuração já foi criado (primeira instalação).</summary>
    public static bool Existe() => File.Exists(CaminhoArquivo);

    /// <summary>Salva a connection string criptografada no disco.</summary>
    public static void Salvar(string connectionString)
    {
        byte[] dados = Encoding.UTF8.GetBytes(connectionString);
        byte[] criptografado = ProtectedData.Protect(dados, Entropia, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(CaminhoArquivo, criptografado);
    }

    /// <summary>Lê e descriptografa a connection string do disco.</summary>
    public static string Carregar()
    {
        if (!Existe())
            throw new FileNotFoundException("Arquivo de configuração não encontrado. Execute o assistente de instalação.");

        byte[] criptografado = File.ReadAllBytes(CaminhoArquivo);
        byte[] dados = ProtectedData.Unprotect(criptografado, Entropia, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(dados);
    }

    /// <summary>Remove o arquivo (útil para "resetar configuração" em suporte).</summary>
    public static void Remover()
    {
        if (Existe())
            File.Delete(CaminhoArquivo);
    }
}
