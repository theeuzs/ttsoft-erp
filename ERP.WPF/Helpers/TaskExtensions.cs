using Serilog;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ERP.WPF.Helpers;

/// <summary>
/// Extensões para execução segura de Tasks em contextos fire-and-forget.
/// Substitui o padrão perigoso de `async void` nos ViewModels.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Executa uma Task em fire-and-forget de forma segura:
    /// captura exceções, loga via Serilog e exibe mensagem amigável.
    /// Uso: _ = MinhaTaskAsync().SafeFireAndForgetAsync();
    /// </summary>
    public static async void SafeFireAndForgetAsync(
        this Task task,
        string? contexto = null,
        Action<Exception>? onError = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            var msg = contexto ?? "operação em background";
            Log.Error(ex, "Erro em {Contexto}", msg);

            onError?.Invoke(ex);

            // Exibe na UI thread se disponível
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            {
                await dispatcher.InvokeAsync(() =>
                    System.Windows.MessageBox.Show(
                        $"Erro ao executar {msg}:\n{ex.Message}",
                        "Erro", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
        }
    }

    /// <summary>
    /// Versão silenciosa — loga mas não exibe MessageBox.
    /// Ideal para recarregamentos automáticos em background.
    /// </summary>
    public static async void SafeFireAndForgetSilentAsync(
        this Task task,
        string? contexto = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro silencioso em {Contexto}", contexto ?? "background task");
        }
    }
}