using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

Console.WriteLine("=== TTSoft Updater ===");

try 
{
    if (args.Length < 3) 
    {
        Console.WriteLine("Erro: Faltam argumentos!");
        Console.ReadLine();
        return;
    }

    string exeNovo = args[0];
    string exeDestino = args[1];
    int pidErp = int.Parse(args[2]);

    Console.WriteLine("Aguardando o ERP fechar...");
    try { Process.GetProcessById(pidErp).WaitForExit(10000); } catch { }
    
    Console.WriteLine("Sucesso!");
    Thread.Sleep(5000);

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