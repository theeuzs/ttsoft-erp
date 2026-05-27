using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly AppDbContext   _db;
    private readonly IRequestTenant _tenant;

    public ConfigController(AppDbContext db, IRequestTenant tenant)
    {
        _db     = db;
        _tenant = tenant;
    }

    /// <summary>
    /// Config pública da loja — usada pela CalculadoraPublica e CatalogoPublico.
    /// AllowAnonymous: não expõe dados sensíveis, só nome fantasia e telefone.
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic([FromQuery] Guid? tenantId = null)
    {
        // Quando chamado sem JWT, o tenantId vem como query param (calculadora pública)
        var tid = tenantId ?? _tenant.TenantId;
        if (tid == Guid.Empty) return Ok(new { NomeFantasia = "Loja", Telefone = "" });

        var branch = await _db.Branches
            .AsNoTracking()
            .Where(b => b.TenantId == tid && b.IsMatriz)
            .Select(b => new { b.Name, b.Telefone })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            NomeFantasia = branch?.Name ?? "Loja",
            Telefone     = branch?.Telefone ?? ""
        });
    }

    /// <summary>Retorna as configurações completas da loja (filial matriz).</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get()
    {
        var branch = await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsMatriz)
            .FirstOrDefaultAsync();

        if (branch is null)
            return Ok(new ConfigLojaDto());

        return Ok(new ConfigLojaDto
        {
            Id       = branch.Id,
            Nome     = branch.Name,
            CNPJ     = branch.CNPJ ?? "",
            Endereco = branch.Endereco ?? "",
            Telefone = branch.Telefone ?? ""
        });
    }

    /// <summary>Atualiza as configurações da loja.</summary>
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Put([FromBody] ConfigLojaDto dto)
    {
        var branch = await _db.Branches
            .Where(b => b.IsMatriz)
            .FirstOrDefaultAsync();

        if (branch is null)
        {
            // Cria a filial matriz se não existir
            branch = new Branch
            {
                Id       = Guid.NewGuid(),
                IsMatriz = true,
                IsActive = true
            };
            _db.Branches.Add(branch);
        }

        branch.Name     = dto.Nome;
        branch.CNPJ     = dto.CNPJ;
        branch.Endereco = dto.Endereco;
        branch.Telefone = dto.Telefone;

        await _db.SaveChangesAsync();
        return Ok(new { mensagem = "Configurações salvas." });
    }
}

public class ConfigLojaDto
{
    public Guid   Id       { get; set; }
    public string Nome     { get; set; } = "";
    public string CNPJ     { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Telefone { get; set; } = "";
}
