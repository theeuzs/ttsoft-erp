// ERP.WPF/Services/ConnectivityIndicatorState.cs
namespace ERP.WPF.Services;

/// <summary>
/// Fase 3 do Offline-First (docs/OFFLINE_FIRST_ARCHITECTURE.md, §12) —
/// estado compartilhado do indicador 🟢/🔴. Não implementa detecção de rede
/// própria de propósito: reaproveita o resultado do próprio ciclo do
/// SyncEngine que já roda a cada minuto (MainWindow) — se a última
/// tentativa de processar a Outbox teve sucesso, está online; se falhou por
/// conectividade, está offline. Estático porque o app inteiro (qualquer
/// tela) precisa do mesmo estado, sem precisar injetar nada.
/// </summary>
public static class ConnectivityIndicatorState
{
    private static bool _online = true;
    public static bool Online
    {
        get => _online;
        private set
        {
            if (_online == value) return;
            _online = value;
            Changed?.Invoke();
        }
    }

    private static int _vendasPendentes;
    public static int VendasPendentes
    {
        get => _vendasPendentes;
        private set
        {
            if (_vendasPendentes == value) return;
            _vendasPendentes = value;
            Changed?.Invoke();
        }
    }

    private static DateTime? _ultimaSincronizacaoOk;
    public static DateTime? UltimaSincronizacaoOk
    {
        get => _ultimaSincronizacaoOk;
        private set { _ultimaSincronizacaoOk = value; Changed?.Invoke(); }
    }

    /// <summary>Disparado sempre que qualquer campo acima muda — telas
    /// (PdvViewModel) se inscrevem nisso pra atualizar o indicador sem
    /// precisar de polling próprio.</summary>
    public static event Action? Changed;

    /// <summary>Chamado pelo MainWindow depois de cada ciclo do SyncEngine
    /// (sucesso ou falha) — nunca pelas telas, que só leem o estado.</summary>
    public static void AtualizarAposCiclo(bool sucesso, int vendasPendentes)
    {
        Online = sucesso;
        VendasPendentes = vendasPendentes;
        if (sucesso) UltimaSincronizacaoOk = DateTime.Now;
    }
}
