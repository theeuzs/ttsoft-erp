using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

// ── Entrega ───────────────────────────────────────────────────────────────────

public class EntregaDto
{
    public Guid          Id              { get; set; }
    public Guid          SaleId          { get; set; }
    public string        ClienteNome     { get; set; } = "";
    public string?       Logradouro      { get; set; }
    public string?       Numero          { get; set; }
    public string?       Complemento     { get; set; }
    public string?       Bairro          { get; set; }
    public string?       Cidade          { get; set; }
    public string?       UF              { get; set; }
    public string?       CEP             { get; set; }
    public string?       Referencia      { get; set; }
    public string        EnderecoCompleto =>
        $"{Logradouro}, {Numero}{(string.IsNullOrEmpty(Complemento) ? "" : " " + Complemento)} — {Bairro}, {Cidade}/{UF}";

    public DateTime      DataPrevista    { get; set; }
    public DateTime?     DataEntrega     { get; set; }
    public string?       JanelaHorario   { get; set; }
    public StatusEntrega Status          { get; set; }
    public string        StatusTexto     => Status switch
    {
        StatusEntrega.Pendente   => "Pendente",
        StatusEntrega.EmRota     => "Em rota",
        StatusEntrega.Entregue   => "Entregue",
        StatusEntrega.Cancelada  => "Cancelada",
        StatusEntrega.Reagendada => "Reagendada",
        _ => "?"
    };

    public Guid?         MotoristaId     { get; set; }
    public string?       MotoristaNome   { get; set; }
    public Guid?         VeiculoId       { get; set; }
    public string?       VeiculoPlaca    { get; set; }
    public string?       Observacoes     { get; set; }
    public string?       MotivoProblema  { get; set; }
    public string?       AssinadoPor     { get; set; }
    public decimal       CustoEntrega    { get; set; }
    public DateTime      CreatedAt       { get; set; }
}

public class CreateEntregaDto
{
    public Guid      SaleId          { get; set; }
    public Guid?     CustomerId      { get; set; }
    public string    ClienteNome     { get; set; } = "";
    public string?   Logradouro      { get; set; }
    public string?   Numero          { get; set; }
    public string?   Complemento     { get; set; }
    public string?   Bairro          { get; set; }
    public string?   Cidade          { get; set; }
    public string?   UF              { get; set; }
    public string?   CEP             { get; set; }
    public string?   Referencia      { get; set; }
    public DateTime  DataPrevista    { get; set; } = DateTime.Today.AddDays(1);
    public string?   JanelaHorario   { get; set; }
    public Guid?     MotoristaId     { get; set; }
    public Guid?     VeiculoId       { get; set; }
    public string?   Observacoes     { get; set; }
    public decimal   CustoEntrega    { get; set; } = 0;
}

public class AtualizarStatusEntregaDto
{
    public StatusEntrega Status          { get; set; }
    public string?       MotivoProblema  { get; set; }
    public string?       AssinadoPor     { get; set; }
    public string?       FotoComprovante { get; set; }
    /// <summary>Nova data quando Status = Reagendada.</summary>
    public DateTime?     NovaDataPrevista { get; set; }
}

public class AtribuirMotoristaDto
{
    public Guid  MotoristaId { get; set; }
    public Guid? VeiculoId   { get; set; }
}

public class RelatorioEntregasDto
{
    public int     TotalDia          { get; set; }
    public int     Entregues         { get; set; }
    public int     Pendentes         { get; set; }
    public int     EmRota            { get; set; }
    public int     Canceladas        { get; set; }
    public decimal CustoTotal        { get; set; }
    public decimal TaxaEntrega       => TotalDia > 0
        ? Math.Round((decimal)Entregues / TotalDia * 100, 1) : 0;
    public List<RelatorioMotoristaDto> PorMotorista { get; set; } = new();
}

public class RelatorioMotoristaDto
{
    public string  MotoristaNome  { get; set; } = "";
    public int     TotalEntregas  { get; set; }
    public int     Entregues      { get; set; }
    public int     Pendentes      { get; set; }
    public decimal CustoTotal     { get; set; }
}

// ── Veículo ───────────────────────────────────────────────────────────────────

public class VeiculoDto
{
    public Guid    Id         { get; set; }
    public string  Placa      { get; set; } = "";
    public string  Tipo       { get; set; } = "";
    public string  Modelo     { get; set; } = "";
    public decimal Capacidade { get; set; }
    public bool    IsAtivo    { get; set; }
}

public class CreateVeiculoDto
{
    public string  Placa      { get; set; } = "";
    public string  Tipo       { get; set; } = "";
    public string  Modelo     { get; set; } = "";
    public decimal Capacidade { get; set; }
}
