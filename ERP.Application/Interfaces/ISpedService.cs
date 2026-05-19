using System;
using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface ISpedService
{
    /// <summary>
    /// Gera o arquivo EFD-ICMS/IPI com as vendas (NF-e) do período.
    /// Retorna o conteúdo do arquivo .txt pronto para envio ao SEFAZ.
    /// </summary>
    Task<string> GerarEfdIcmsAsync(SpedParametros parametros);

    /// <summary>
    /// Gera o arquivo EFD-Contribuições (PIS/COFINS) do período.
    /// </summary>
    Task<string> GerarEfdContribuicoesAsync(SpedParametros parametros);
}

public class SpedParametros
{
    public DateTime DataInicio      { get; set; }
    public DateTime DataFim         { get; set; }
    public string   RazaoSocial     { get; set; } = string.Empty;
    public string   NomeFantasia    { get; set; } = string.Empty;
    public string   CNPJ            { get; set; } = string.Empty;
    public string   IE              { get; set; } = string.Empty;
    public string   IM              { get; set; } = string.Empty;
    public string   CodigoMunicipio { get; set; } = "4106902"; // Curitiba default
    public string   UF              { get; set; } = "PR";
    public string   Endereco        { get; set; } = string.Empty;
    public string   IndPerfil       { get; set; } = "C"; // C = Simples Nacional
    // Contabilista
    public string ContabNome  { get; set; } = string.Empty;
    public string ContabCpf   { get; set; } = string.Empty;
    public string ContabCrc   { get; set; } = string.Empty;
    public string ContabEmail { get; set; } = string.Empty;
    public string ContabFone  { get; set; } = string.Empty;
}
