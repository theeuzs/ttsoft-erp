using Serilog;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Interruptor da recuperacao fiscal por tenant (Etapa 4A-6a). Decide para quais tenants a recuperacao nova cuida das
/// NFC-e pendentes; para os demais, o worker antigo segue como sempre. UMA unica fonte da verdade, lida pelos dois workers.
/// </summary>
public interface IFiscalRecoverySwitch
{
    bool EstaHabilitadoPara(Guid tenantId);

    /// <summary>
    /// Os tenants habilitados (usado pelo worker novo para nem abrir escopo quando a lista esta vazia). Implementacao padrao: nenhum,
    /// de modo que dubles simples (de teste) continuam validos; FiscalRecoverySwitch a sobrescreve.
    /// </summary>
    IReadOnlyCollection<Guid> TenantsHabilitados => Array.Empty<Guid>();
}

/// <summary>
/// Le a lista de tenants de uma configuracao de TEXTO (App Setting <c>FiscalRecovery__TenantsHabilitados</c>, no codigo
/// <c>FiscalRecovery:TenantsHabilitados</c>): GUIDs no formato 8-4-4-4-12 separados por VIRGULA.
///
/// FALHA FECHADA: ausente, vazia ou em branco = nenhum tenant habilitado. Qualquer item invalido (vazio por virgula sobrando,
/// outro separador, outro formato de GUID, "*", GUID zerado) invalida a lista INTEIRA: nenhum tenant e habilitado, e o motivo
/// vai para o log como erro. Nunca se habilita "o que deu para entender" de uma configuracao quebrada, e nao existe
/// sintaxe de "todos os tenants".
///
/// Imutavel e sem estado externo: depois de construido, EstaHabilitadoPara so consulta um conjunto em memoria.
/// </summary>
public sealed class FiscalRecoverySwitch : IFiscalRecoverySwitch
{
    public const string ChaveDeConfiguracao = "FiscalRecovery:TenantsHabilitados";

    private const int TamanhoMaximoDoTrechoLogado = 40;

    private readonly HashSet<Guid> _habilitados;

    public FiscalRecoverySwitch(string? valorConfigurado)
    {
        var (habilitados, motivoInvalido) = Interpretar(valorConfigurado);

        _habilitados = habilitados;
        MotivoDaInvalidez = motivoInvalido;

        if (motivoInvalido is not null)
        {
            Log.Error(
                "Recuperacao fiscal: a configuracao {Chave} esta INVALIDA ({Motivo}). NENHUM tenant esta habilitado (falha fechada); o worker antigo segue como sempre. Corrija a configuracao.",
                ChaveDeConfiguracao, motivoInvalido);
        }
        else if (_habilitados.Count > 0)
        {
            Log.Information("Recuperacao fiscal: habilitada para {Quantidade} tenant(s).", _habilitados.Count);
        }
    }

    /// <summary>False quando a configuracao tem qualquer item invalido (e, nesse caso, nenhum tenant esta habilitado).</summary>
    public bool ConfiguracaoValida => MotivoDaInvalidez is null;

    public string? MotivoDaInvalidez { get; }

    public IReadOnlyCollection<Guid> TenantsHabilitados => _habilitados;

    public bool EstaHabilitadoPara(Guid tenantId) => tenantId != Guid.Empty && _habilitados.Contains(tenantId);

    private static (HashSet<Guid> Habilitados, string? MotivoInvalido) Interpretar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return (new HashSet<Guid>(), null);

        var resultado = new HashSet<Guid>();
        var itens = valor.Split(',');

        for (var i = 0; i < itens.Length; i++)
        {
            var item = itens[i].Trim();
            var numero = i + 1;

            if (item.Length == 0)
                return (new HashSet<Guid>(), $"o item {numero} da lista esta vazio (virgula sobrando?)");

            if (!Guid.TryParseExact(item, "D", out var id))
                return (new HashSet<Guid>(), $"o item {numero} ('{Truncar(item)}') nao e um GUID no formato 8-4-4-4-12");

            if (id == Guid.Empty)
                return (new HashSet<Guid>(), $"o item {numero} e o GUID zerado");

            resultado.Add(id);
        }

        return (resultado, null);
    }

    private static string Truncar(string texto) =>
        texto.Length <= TamanhoMaximoDoTrechoLogado ? texto : texto.Substring(0, TamanhoMaximoDoTrechoLogado) + "...";
}
