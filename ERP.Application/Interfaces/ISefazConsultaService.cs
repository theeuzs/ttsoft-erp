namespace ERP.Application.Interfaces;

/// <summary>
/// Consulta o status de autorização de uma NF-e diretamente na SEFAZ.
/// Fase 3 da validação de NF-e importada — verifica que a nota existe, está autorizada e não foi cancelada.
/// A implementação usa SOAP mTLS com o certificado digital A1 do tenant.
/// Retorna null se o certificado não estiver configurado — skip gracioso em dev/staging sem cert.
/// </summary>
public interface ISefazConsultaService
{
    Task<SefazConsultaResultado?> ConsultarAsync(string chNFe, CancellationToken ct = default);
}

/// <summary>Resultado da consulta de situação de NF-e na SEFAZ.</summary>
/// <param name="CStat">Código de status (100=Autorizada, 101=Cancelada, 110=Denegada...).</param>
/// <param name="XMotivo">Descrição do status retornada pela SEFAZ.</param>
/// <param name="NumeroProt">Número do protocolo de autorização (ausente em rejeição).</param>
/// <param name="DhRecbto">Data/hora do recebimento pela SEFAZ (UTC).</param>
/// <param name="Autorizada">true apenas quando cStat=100 ou 150 (autorização fora de prazo).</param>
public record SefazConsultaResultado(
    string   CStat,
    string   XMotivo,
    string?  NumeroProt,
    DateTime? DhRecbto,
    bool     Autorizada);
