using ERP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Fase 3 da validação de NF-e importada:
/// Consulta o webservice NFeConsultaProtocolo4 da SEFAZ para verificar que a NF-e
/// existe no ambiente SEFAZ, está autorizada e não foi cancelada.
///
/// Requer:
///   - Certificado digital A1 (.pfx) do tenant configurado em appsettings.json → "Sefaz:CertificadoPath"
///   - Senha do certificado em "Sefaz:CertificadoSenha" (ou user-secrets em dev)
///   - Ambiente em "Sefaz:Ambiente" (1=Produção, 2=Homologação)
///
/// Skip gracioso: se CertificadoPath estiver vazio ou o arquivo não existir, retorna null
/// e NfeImportService pula a Fase 3 sem falhar. Útil em dev/staging sem certificado.
/// </summary>
public class SefazConsultaService : ISefazConsultaService
{
    private readonly ILogger<SefazConsultaService> _log;
    private readonly string _tpAmb;
    private readonly X509Certificate2?  _cert;

    // ── Tabela de endpoints NFeConsultaProtocolo4 por cUF ────────────────────
    // cUF é extraído dos 2 primeiros dígitos da chave de acesso (chNFe).
    // Atualizado conforme portaria SEFAZ em vigor em 2026.
    // Fonte: https://www.nfe.fazenda.gov.br/portal/webServices.aspx
    private static readonly System.Collections.Generic.Dictionary<int, string> Endpoints = new()
    {
        // ── Sefaz Virtual RS (SVRS) ─────────────────────────────────────────
        // Autorizado: AC, AL, AP, DF, ES, PB, PR, RJ, RO, RR, SC, SE, TO
        { 12, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // AC
        { 27, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // AL
        { 16, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // AP
        { 53, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // DF
        { 32, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // ES
        { 25, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // PB
        { 41, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // PR ← Vila Verde
        { 33, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // RJ
        { 11, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // RO
        { 14, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // RR
        { 42, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // SC
        { 28, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // SE
        { 17, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // TO

        // ── Infraestrutura RS própria ─────────────────────────────────────────
        { 43, "https://nfe.sefazrs.rs.gov.br/ws/NFeConsultaProtocolo/NFeConsultaProtocolo4.asmx" }, // RS

        // ── Sefaz Virtual AN (SVCAN — Goiás) ────────────────────────────────
        // Autorizado: AM, BA, CE, GO, MA, MS, PA, PE, PI, RN
        { 13, "https://www.sefaz.go.gov.br/nfe/services/NFeConsultaProtocolo4" }, // AM
        { 21, "https://www.sefaz.go.gov.br/nfe/services/NFeConsultaProtocolo4" }, // MA
        { 15, "https://www.sefaz.go.gov.br/nfe/services/NFeConsultaProtocolo4" }, // PA

        // ── Infraestrutura própria ───────────────────────────────────────────
        { 29, "https://nfe.sefaz.ba.gov.br/webservices/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx" }, // BA
        { 23, "https://nfe.sefaz.ce.gov.br/nfe4/services/NFeConsultaProtocolo4" },                          // CE
        { 52, "https://www.sefaz.go.gov.br/nfe/services/NFeConsultaProtocolo4" },                           // GO
        { 31, "https://nfe.fazenda.mg.gov.br/nfe/services/NFeConsultaProtocolo4" },                         // MG
        { 50, "https://nfe.sefaz.ms.gov.br/ws/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx" },         // MS
        { 51, "https://nfe.sefaz.mt.gov.br/nfews/v2/services/NfeConsulta3" },                               // MT
        { 26, "https://nfe.sefaz.pe.gov.br/nfe-service/NFeConsultaProtocolo4" },                            // PE
        { 22, "https://nfe.sefaz.pi.gov.br/nfe/services/NFeConsultaProtocolo4" },                           // PI
        { 24, "https://nfe.sefaz.rn.gov.br/nfe/services/NFeConsultaProtocolo4" },                           // RN
        { 35, "https://nfe.fazenda.sp.gov.br/ws/nfeConsulta2.asmx" },                                       // SP
    };

    public SefazConsultaService(IConfiguration config, ILogger<SefazConsultaService> log)
    {
        _log    = log;
        _tpAmb  = config["Sefaz:Ambiente"] ?? "1";

        var certPath   = config["Sefaz:CertificadoPath"];
        var certSenha  = config["Sefaz:CertificadoSenha"];

        if (!string.IsNullOrWhiteSpace(certPath) && File.Exists(certPath))
        {
            try
            {
                _cert = new X509Certificate2(certPath, certSenha,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
                _log.LogInformation("Sefaz: certificado carregado — {Subject} válido até {NotAfter:dd/MM/yyyy}",
                    _cert.Subject, _cert.NotAfter);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Sefaz: falha ao carregar certificado de {Path} — Fase 3 desativada.", certPath);
            }
        }
        else
        {
            _log.LogInformation("Sefaz: CertificadoPath não configurado — consulta SEFAZ (Fase 3) desativada.");
        }
    }

    public async Task<SefazConsultaResultado?> ConsultarAsync(string chNFe, CancellationToken ct = default)
    {
        if (_cert is null)
            return null; // skip gracioso — sem certificado

        if (string.IsNullOrWhiteSpace(chNFe) || chNFe.Length != 44)
        {
            _log.LogWarning("Sefaz: chNFe inválida '{ChNFe}' — consulta ignorada.", chNFe);
            return null;
        }

        if (!int.TryParse(chNFe[..2], out var cUF) || !Endpoints.TryGetValue(cUF, out var endpoint))
        {
            _log.LogWarning("Sefaz: UF {CUF} sem endpoint mapeado — consulta ignorada.", chNFe[..2]);
            return null;
        }

        var soap = BuildSoapEnvelope(cUF, chNFe, _tpAmb);

        using var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(_cert);
        // SEFAZ usa certificados de CAs confiáveis — não desabilitar validação SSL em produção
        // Em caso de problemas com cert SEFAZ, adicionar CA raiz ICP-Brasil ao SO

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        using var content = new StringContent(soap, Encoding.UTF8, "application/soap+xml");
        content.Headers.Add("SOAPAction",
            "\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4/nfeConsultaNF\"");

        HttpResponseMessage resp;
        try
        {
            resp = await http.PostAsync(endpoint, content, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Sefaz: falha ao consultar {Endpoint} para chNFe {ChNFe}.", endpoint, chNFe);
            return null; // skip gracioso — SEFAZ temporariamente indisponível
        }

        var xml = await resp.Content.ReadAsStringAsync(ct);
        return ParseRetorno(xml, chNFe);
    }

    // ── SOAP envelope NFeConsultaProtocolo4 ─────────────────────────────────
    private static string BuildSoapEnvelope(int cUF, string chNFe, string tpAmb) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope">
           <soap12:Header>
             <nfeCabecMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4">
               <cUF>{cUF}</cUF>
               <versaoDados>4.00</versaoDados>
             </nfeCabecMsg>
           </soap12:Header>
           <soap12:Body>
             <nfeDadosMsg xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4">
               <consSitNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00">
                 <tpAmb>{tpAmb}</tpAmb>
                 <xServ>CONSULTAR</xServ>
                 <chNFe>{chNFe}</chNFe>
               </consSitNFe>
             </nfeDadosMsg>
           </soap12:Body>
         </soap12:Envelope>
         """;

    // ── Parse da resposta retConsSitNFe ──────────────────────────────────────
    private SefazConsultaResultado? ParseRetorno(string xml, string chNFe)
    {
        try
        {
            var doc  = XDocument.Parse(xml);
            var ns   = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");

            // Tenta retConsSitNFe direto ou dentro de envelope SOAP
            var ret  = doc.Descendants(ns + "retConsSitNFe").FirstOrDefault();
            if (ret is null)
            {
                _log.LogWarning("Sefaz: resposta sem retConsSitNFe para {ChNFe}. XML: {Xml}", chNFe, xml[..Math.Min(500, xml.Length)]);
                return null;
            }

            var cStat    = ret.Descendants(ns + "cStat").FirstOrDefault()?.Value ?? "";
            var xMotivo  = ret.Descendants(ns + "xMotivo").FirstOrDefault()?.Value ?? "";
            var nProt    = ret.Descendants(ns + "nProt").FirstOrDefault()?.Value;
            var dhStr    = ret.Descendants(ns + "dhRecbto").FirstOrDefault()?.Value;
            DateTime.TryParse(dhStr, out var dhRecbto);

            // cStat 100 = Autorizado; 150 = Autorizado fora de prazo (contingência)
            bool autorizada = cStat is "100" or "150";

            _log.LogInformation("Sefaz: chNFe={ChNFe} → cStat={CStat} {XMotivo} nProt={NProt}",
                chNFe, cStat, xMotivo, nProt);

            return new SefazConsultaResultado(cStat, xMotivo, nProt,
                dhRecbto == default ? null : dhRecbto, autorizada);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Sefaz: erro ao parsear retorno para {ChNFe}.", chNFe);
            return null;
        }
    }
}
