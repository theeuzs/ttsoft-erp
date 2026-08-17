using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

Console.WriteLine("=== TTSoft Updater ===");

try 
{
    if (args.Length < 4) 
    {
        Console.WriteLine("Erro: Faltam argumentos!");
        Console.ReadLine();
        return;
    }

    string exeNovo = args[0];
    string exeDestino = args[1];
    int pidErp = int.Parse(args[2]);
    string sha256Esperado = args[3];

    Console.WriteLine("Aguardando o ERP fechar...");
    try { Process.GetProcessById(pidErp).WaitForExit(10000); } catch { }
    
    Console.WriteLine("Sucesso!");
    Thread.Sleep(5000);

    // S17 FIX: barreira final de integridade. Este processo recebe o
    // caminho do novo exe por linha de comando e nao confia que quem o
    // chamou (UpdateService) ja validou -- calcula o hash de novo e so
    // segue para o File.Copy se bater com o esperado.
    Console.WriteLine("Verificando integridade do arquivo...");
    string hashCalculado = CalcularSha256(exeNovo);

    if (string.IsNullOrWhiteSpace(sha256Esperado)
        || !string.Equals(sha256Esperado.Trim(), hashCalculado.Trim(), StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("\nSHA-256 nao confere com o esperado. Atualizacao ABORTADA por seguranca.");
        Console.WriteLine($"Esperado : {sha256Esperado}");
        Console.WriteLine($"Calculado: {hashCalculado}");
        Console.WriteLine("\nO executavel atual NAO foi substituido. Pressione ENTER para sair...");
        Console.ReadLine();
        return;
    }

    Console.WriteLine("Integridade confirmada.");
    Console.WriteLine("Atualizando arquivos...");
    File.Copy(exeNovo, exeDestino, overwrite: true);
    File.Delete(exeNovo);

    Console.WriteLine("Reiniciando sistema...");
    Process.Start(new ProcessStartInfo
    {
        FileName = exeDestino,
        WorkingDirectory = Path.GetDirectoryName(exeDestino), // O SEGREDO PARA ABRIR O .NET
        UseShellExecute = true
    });

    Console.WriteLine("Sucesso!");
    Thread.Sleep(2000); // Se deu certo, fecha sozinho em 2 segundos
}
catch (Exception ex)
{
    // SE DER QUALQUER ERRO, ELE TRAVA AQUI E TE MOSTRA
    Console.WriteLine($"\nDEU RUIM: {ex.Message}");
    Console.WriteLine("\nPressione ENTER para sair...");
    Console.ReadLine();
}

// S17 FIX: mesmo algoritmo/formato usado no lado do UpdateService (WPF) --
// hex sem separadores, comparado case-insensitive depois de Trim().
static string CalcularSha256(string caminhoArquivo)
{
    using var sha256 = SHA256.Create();
    using var stream = File.OpenRead(caminhoArquivo);
    return Convert.ToHexString(sha256.ComputeHash(stream));
}