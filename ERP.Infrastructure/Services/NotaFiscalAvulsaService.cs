// ── ERP.Infrastructure/Services/NotaFiscalAvulsaService.cs ─────────────────
using ERP.Application.DTOs;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ERP.Infrastructure.Services;

public class NotaFiscalAvulsaService : INotaFiscalAvulsaService
{
    private readonly Persistence.Context.AppDbContext _ctx;
    private readonly IUnitOfWork _uow;
    private readonly IMotorFiscalService _motorFiscal;
    private readonly IFiscalConfigurationProvider _configProvider;
    private readonly INfeEmissionService _nfeService;
    private readonly INfeCancellationService _cancelService;
    private readonly INfeStatusService _statusService;
    private readonly IRequestTenant _tenant;

    public NotaFiscalAvulsaService(
        Persistence.Context.AppDbContext ctx, IUnitOfWork uow, IMotorFiscalService motorFiscal,
        IFiscalConfigurationProvider configProvider, INfeEmissionService nfeService,
        INfeCancellationService cancelService, INfeStatusService statusService, IRequestTenant tenant)
    {
        _ctx            = ctx;
        _uow            = uow;
        _motorFiscal    = motorFiscal;
        _configProvider = configProvider;
        _nfeService     = nfeService;
        _cancelService  = cancelService;
        _statusService  = statusService;
        _tenant         = tenant;
    }

    public async Task<Guid> SalvarRascunhoAsync(SalvarNotaFiscalAvulsaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DestinatarioNome))
            throw new InvalidOperationException("Nome do destinatário é obrigatório.");
        if (!dto.Itens.Any())
            throw new InvalidOperationException("Adicione pelo menos um item.");

        Domain.Entities.NotaFiscal nota;

        if (dto.Id.HasValue)
        {
            var notaExistente = await _ctx.NotasFiscais.Include(n => n.Itens).Include(n => n.Pagamentos)
                .FirstOrDefaultAsync(n => n.Id == dto.Id.Value)
                ?? throw new KeyNotFoundException("Nota não encontrada.");

            if (notaExistente.Status != "Rascunho")
                throw new InvalidOperationException("Só é possível editar uma nota em Rascunho.");

            // S27 correção (19/08) — remover os itens/pagamentos antigos E
            // adicionar os novos na MESMA SaveChangesAsync (delete+insert no
            // mesmo lote) causava DbUpdateConcurrencyException "0 rows
            // affected" ao editar o mesmo rascunho pela segunda vez. Não
            // confirmei o mecanismo exato do EF Core aqui (não consigo
            // rodar o projeto), mas separar a remoção num commit à parte
            // resolve independente da causa exata. AppDbContext roda
            // ChangeTracker.Clear() depois de cada SaveChangesAsync — por
            // isso a nota precisa ser buscada de novo depois desse commit.
            if (notaExistente.Itens.Any())
                _ctx.NotaFiscalItens.RemoveRange(notaExistente.Itens);
            if (notaExistente.Pagamentos.Any())
                _ctx.NotaFiscalPagamentos.RemoveRange(notaExistente.Pagamentos);
            await _ctx.SaveChangesAsync();

            nota = await _ctx.NotasFiscais.AsTracking().FirstAsync(n => n.Id == dto.Id.Value);
        }
        else
        {
            nota = new Domain.Entities.NotaFiscal
            {
                Tipo   = "NFE",
                Status = "Rascunho",
            };
            _ctx.NotasFiscais.Add(nota);
        }

        nota.NaturezaOperacao          = dto.NaturezaOperacao;
        nota.TipoOperacaoEntradaSaida  = dto.TipoOperacaoEntradaSaida;
        nota.Finalidade                = dto.Finalidade;
        nota.DestinatarioNome          = dto.DestinatarioNome;
        nota.DestinatarioDocumento     = dto.DestinatarioDocumento;
        nota.DestinatarioLogradouro    = dto.DestinatarioLogradouro;
        nota.DestinatarioNumero        = dto.DestinatarioNumero;
        nota.DestinatarioBairro        = dto.DestinatarioBairro;
        nota.DestinatarioMunicipio     = dto.DestinatarioMunicipio;
        nota.DestinatarioUf            = dto.DestinatarioUf;
        nota.DestinatarioCep           = dto.DestinatarioCep;
        nota.DestinatarioIe            = dto.DestinatarioIe;
        nota.IndicadorIeDestinatario   = dto.IndicadorIeDestinatario;

        // Achados da revisão de arquitetura (18/08)
        nota.RefNFe                      = dto.RefNfeReferenciada;
        nota.InformacoesComplementares   = dto.InformacoesComplementares;
        nota.ModalidadeFrete             = dto.ModalidadeFrete;
        nota.TransportadoraNome          = dto.TransportadoraNome;
        nota.TransportadoraDocumento     = dto.TransportadoraDocumento;
        nota.TransportadoraIe            = dto.TransportadoraIe;
        nota.TransportadoraEndereco      = dto.TransportadoraEndereco;
        nota.TransportadoraMunicipio     = dto.TransportadoraMunicipio;
        nota.TransportadoraUf            = dto.TransportadoraUf;
        nota.VeiculoPlaca                = dto.VeiculoPlaca;
        nota.VeiculoUf                   = dto.VeiculoUf;
        nota.QuantidadeVolumes           = dto.QuantidadeVolumes;
        nota.EspecieVolumes              = dto.EspecieVolumes;
        nota.PesoBrutoKg                 = dto.PesoBrutoKg;
        nota.PesoLiquidoKg               = dto.PesoLiquidoKg;

        // S27 correção (19/08), 2ª tentativa — adicionar via nota.Itens.Add()/
        // nota.Pagamentos.Add() numa nota que foi buscada SEM .Include() (o
        // caso do caminho de edição, depois do commit intermediário acima)
        // pode não registrar o fixup de FK corretamente, porque o EF não tem
        // como saber se a coleção representa "tudo" ou só o que foi
        // adicionado agora. Setando a FK na mão e adicionando direto no
        // DbSet, sem depender da coleção de navegação, elimina essa
        // ambiguidade de vez.
        foreach (var item in dto.Itens)
        {
            _ctx.NotaFiscalItens.Add(new Domain.Entities.NotaFiscalItem
            {
                NotaFiscalId  = nota.Id,
                ProductId     = item.ProductId,
                ProductName   = item.ProductName,
                Quantidade    = item.Quantidade,
                ValorUnitario = item.ValorUnitario,
                Cfop          = item.Cfop,
            });
        }

        // Pagamentos — os antigos já foram removidos (e commitados) lá em
        // cima, no caminho de edição; aqui só reconstrói do zero.
        foreach (var pag in dto.Pagamentos)
        {
            _ctx.NotaFiscalPagamentos.Add(new Domain.Entities.NotaFiscalPagamento
            {
                NotaFiscalId   = nota.Id,
                FormaPagamento = pag.FormaPagamento,
                Valor          = pag.Valor,
            });
        }

        await _ctx.SaveChangesAsync();
        return nota.Id;
    }

    public async Task<Guid> CopiarComoRascunhoAsync(Guid idOrigem)
    {
        var origem = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens).Include(n => n.Pagamentos)
            .FirstOrDefaultAsync(n => n.Id == idOrigem)
            ?? throw new KeyNotFoundException("Nota original não encontrada.");

        var copia = new Domain.Entities.NotaFiscal
        {
            Tipo                     = "NFE",
            Status                   = "Rascunho",
            NaturezaOperacao         = origem.NaturezaOperacao,
            TipoOperacaoEntradaSaida = origem.TipoOperacaoEntradaSaida,
            Finalidade               = origem.Finalidade,
            DestinatarioNome         = origem.DestinatarioNome,
            DestinatarioDocumento    = origem.DestinatarioDocumento,
            DestinatarioLogradouro   = origem.DestinatarioLogradouro,
            DestinatarioNumero       = origem.DestinatarioNumero,
            DestinatarioBairro       = origem.DestinatarioBairro,
            DestinatarioMunicipio    = origem.DestinatarioMunicipio,
            DestinatarioUf           = origem.DestinatarioUf,
            DestinatarioCep          = origem.DestinatarioCep,
            DestinatarioIe           = origem.DestinatarioIe,
            IndicadorIeDestinatario  = origem.IndicadorIeDestinatario,

            // Achados da revisão de arquitetura (18/08) — transporte e
            // informações complementares fazem sentido copiar (tendem a se
            // repetir). RefNFe NUNCA é copiado: cada devolução referencia
            // uma venda original diferente — copiar a mesma chave criaria
            // uma segunda nota apontando pra venda errada.
            InformacoesComplementares = origem.InformacoesComplementares,
            ModalidadeFrete           = origem.ModalidadeFrete,
            TransportadoraNome        = origem.TransportadoraNome,
            TransportadoraDocumento   = origem.TransportadoraDocumento,
            TransportadoraIe          = origem.TransportadoraIe,
            TransportadoraEndereco    = origem.TransportadoraEndereco,
            TransportadoraMunicipio   = origem.TransportadoraMunicipio,
            TransportadoraUf          = origem.TransportadoraUf,
            VeiculoPlaca              = origem.VeiculoPlaca,
            VeiculoUf                 = origem.VeiculoUf,
            QuantidadeVolumes         = origem.QuantidadeVolumes,
            EspecieVolumes            = origem.EspecieVolumes,
            PesoBrutoKg               = origem.PesoBrutoKg,
            PesoLiquidoKg             = origem.PesoLiquidoKg,
        };

        foreach (var pag in origem.Pagamentos)
            copia.Pagamentos.Add(new Domain.Entities.NotaFiscalPagamento
            {
                FormaPagamento = pag.FormaPagamento,
                Valor          = pag.Valor,
            });

        foreach (var item in origem.Itens)
            copia.Itens.Add(new Domain.Entities.NotaFiscalItem
            {
                ProductId     = item.ProductId,
                ProductName   = item.ProductName,
                Quantidade    = item.Quantidade,
                ValorUnitario = item.ValorUnitario,
                Cfop          = item.Cfop,
            });

        _ctx.NotasFiscais.Add(copia);
        await _ctx.SaveChangesAsync();
        return copia.Id;
    }

    public async Task<NotaFiscalAvulsaDto?> ObterAsync(Guid id)
    {
        var nota = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens).Include(n => n.Pagamentos)
            .FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null) return null;

        return new NotaFiscalAvulsaDto
        {
            Id                          = nota.Id,
            Status                      = nota.Status,
            UrlDanfe                    = nota.UrlDanfe,
            DataEmissao                 = nota.DataEmissao,
            NaturezaOperacao            = nota.NaturezaOperacao ?? "",
            TipoOperacaoEntradaSaida    = nota.TipoOperacaoEntradaSaida,
            Finalidade                  = nota.Finalidade,
            DestinatarioNome            = nota.DestinatarioNome ?? "",
            DestinatarioDocumento       = nota.DestinatarioDocumento,
            DestinatarioLogradouro      = nota.DestinatarioLogradouro,
            DestinatarioNumero          = nota.DestinatarioNumero,
            DestinatarioBairro          = nota.DestinatarioBairro,
            DestinatarioMunicipio       = nota.DestinatarioMunicipio,
            DestinatarioUf              = nota.DestinatarioUf,
            DestinatarioCep             = nota.DestinatarioCep,
            DestinatarioIe              = nota.DestinatarioIe,
            IndicadorIeDestinatario     = nota.IndicadorIeDestinatario,
            RefNfeReferenciada          = nota.RefNFe,
            InformacoesComplementares   = nota.InformacoesComplementares,
            ModalidadeFrete             = nota.ModalidadeFrete,
            TransportadoraNome          = nota.TransportadoraNome,
            TransportadoraDocumento     = nota.TransportadoraDocumento,
            TransportadoraIe            = nota.TransportadoraIe,
            TransportadoraEndereco      = nota.TransportadoraEndereco,
            TransportadoraMunicipio     = nota.TransportadoraMunicipio,
            TransportadoraUf            = nota.TransportadoraUf,
            VeiculoPlaca                = nota.VeiculoPlaca,
            VeiculoUf                   = nota.VeiculoUf,
            QuantidadeVolumes           = nota.QuantidadeVolumes,
            EspecieVolumes              = nota.EspecieVolumes,
            PesoBrutoKg                 = nota.PesoBrutoKg,
            PesoLiquidoKg               = nota.PesoLiquidoKg,
            Itens = nota.Itens.Select(i => new NotaFiscalAvulsaItemDto
            {
                ProductId     = i.ProductId,
                ProductName   = i.ProductName,
                Quantidade    = i.Quantidade,
                ValorUnitario = i.ValorUnitario,
                Cfop          = i.Cfop,
            }).ToList(),
            Pagamentos = nota.Pagamentos.Select(p => new NotaFiscalAvulsaPagamentoDto
            {
                FormaPagamento = p.FormaPagamento,
                Valor          = p.Valor,
            }).ToList(),
        };
    }

    public async Task<IReadOnlyList<NotaFiscalAvulsaResumoDto>> ListarAsync()
    {
        var notas = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens)
            .Where(n => n.VendaId == null) // só avulsa — nota de venda não entra aqui
            .OrderByDescending(n => n.DataEmissao)
            .ToListAsync();

        return notas.Select(n => new NotaFiscalAvulsaResumoDto(
            n.Id, n.NaturezaOperacao ?? "", n.DestinatarioNome ?? "",
            n.Itens.Sum(i => i.Quantidade * i.ValorUnitario),
            n.Status, n.DataEmissao)).ToList();
    }

    public async Task ExcluirRascunhoAsync(Guid id)
    {
        var nota = await _ctx.NotasFiscais.Include(n => n.Itens).Include(n => n.Pagamentos).FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != "Rascunho")
            throw new InvalidOperationException("Só é possível excluir uma nota em Rascunho — nota já emitida se cancela, não se exclui.");

        _ctx.NotaFiscalItens.RemoveRange(nota.Itens);
        _ctx.NotaFiscalPagamentos.RemoveRange(nota.Pagamentos);
        _ctx.NotasFiscais.Remove(nota);
        await _ctx.SaveChangesAsync();
    }

    public async Task<ConferenciaFiscalDto> ConferirAsync(Guid id)
    {
        var nota = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        var itensConferencia = new List<ConferenciaItemDto>();
        decimal totalProdutos = 0, totalImpostos = 0;

        foreach (var item in nota.Itens)
        {
            var produto = await _uow.Products.GetByIdAsync(item.ProductId)
                ?? throw new KeyNotFoundException($"Produto '{item.ProductName}' não encontrado.");

            var tributos = _motorFiscal.CalcularTributosVenda(produto, item.Quantidade, item.ValorUnitario);
            var valorTotalItem = item.Quantidade * item.ValorUnitario;

            itensConferencia.Add(new ConferenciaItemDto(item.ProductName, item.Quantidade, item.ValorUnitario, valorTotalItem, tributos));
            totalProdutos += valorTotalItem;
            totalImpostos += tributos.ValorIcms + tributos.ValorIcmsSt;
        }

        return new ConferenciaFiscalDto(itensConferencia, totalProdutos, totalImpostos);
    }

    public async Task<FiscalEmissionResult> EmitirAsync(Guid id)
    {
        // S27 correção (20/08) — achado com evidência direta no banco
        // (UpdatedAt ficava NULL mesmo depois do "sucesso"): AppDbContext
        // roda com QueryTrackingBehavior.NoTracking GLOBAL (WPF e API, ver
        // App.xaml.cs/Program.cs). Sem .AsTracking() aqui, `nota.Status =
        // "..."` mais embaixo simplesmente não é rastreado — SaveChangesAsync
        // "funciona" mas não salva nada, porque não tem o que salvar. Só
        // Add()/Remove()/RemoveRange() explícitos escapam disso (marcam o
        // estado na mão), por isso os Itens/Pagamentos sempre gravaram
        // certo enquanto o Status da própria nota nunca gravava.
        var nota = await _ctx.NotasFiscais.AsTracking().Include(n => n.Itens).Include(n => n.Pagamentos)
            .FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != "Rascunho")
            throw new InvalidOperationException("Essa nota já foi emitida — não é possível emitir de novo.");

        if (!nota.Itens.Any())
            throw new InvalidOperationException("Nota sem itens.");

        // Achado da revisão de arquitetura (18/08) — Finalidade "4" (devolução)
        // sem a chave da nota original é rejeição garantida da SEFAZ. Bloqueia
        // aqui em vez de deixar a Focus rejeitar depois.
        if (nota.Finalidade == "4" && string.IsNullOrWhiteSpace(nota.RefNFe))
            throw new InvalidOperationException(
                "Nota de devolução precisa da chave de acesso (44 dígitos) da nota fiscal original antes de emitir.");
        if (nota.Finalidade == "4" && nota.RefNFe!.Where(char.IsDigit).Count() != 44)
            throw new InvalidOperationException(
                "Chave da nota referenciada precisa ter exatamente 44 dígitos.");

        // Fix 2 (plano premium) — endereço inventado autorizado numa NF-e
        // pra CNPJ é pior que rejeição: vira documento fiscal errado em nome
        // de outra empresa. Bloqueia em vez de mandar fallback silencioso.
        string? docLimpoValidacao = string.IsNullOrWhiteSpace(nota.DestinatarioDocumento)
            ? null : new string(nota.DestinatarioDocumento.Where(char.IsDigit).ToArray());
        bool ehCnpj = docLimpoValidacao?.Length == 14;
        if (ehCnpj)
        {
            var faltando = new List<string>();
            if (string.IsNullOrWhiteSpace(nota.DestinatarioLogradouro)) faltando.Add("Logradouro");
            if (string.IsNullOrWhiteSpace(nota.DestinatarioMunicipio)) faltando.Add("Município");
            if (string.IsNullOrWhiteSpace(nota.DestinatarioUf)) faltando.Add("UF");
            if (string.IsNullOrWhiteSpace(nota.DestinatarioCep)) faltando.Add("CEP");
            if (faltando.Any())
                throw new InvalidOperationException(
                    $"Destinatário com CNPJ precisa de endereço completo antes de emitir — faltando: {string.Join(", ", faltando)}.");
        }

        var config = await _configProvider.ObterConfiguracaoAsync();
        string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";

        var itensRequest = new List<FocusItemRequest>();
        decimal valorTotalItens = 0;
        foreach (var (item, index) in nota.Itens.Select((it, idx) => (it, idx)))
        {
            var produto = await _uow.Products.GetByIdAsync(item.ProductId);

            // Fix 6 (plano premium) — NF-e valida NCM de verdade (diferente
            // da NFCe); "00000000" de fallback vai rejeitar. Bloqueia com
            // mensagem clara em vez de deixar a SEFAZ rejeitar depois.
            var ncmLimpo = produto?.NCM?.Replace(".", "").Replace("-", "").Trim();
            if (string.IsNullOrWhiteSpace(ncmLimpo) || ncmLimpo.Length != 8)
                throw new InvalidOperationException(
                    $"Produto '{item.ProductName}' está sem NCM válido — edite o cadastro do produto antes de emitir.");

            // Fix 4 (plano premium) — GUID truncado como código no DANFE é
            // sem significado e arrisca colisão. Usa SKU de verdade.
            string codigoProduto = !string.IsNullOrWhiteSpace(produto?.SKU) ? produto!.SKU!
                : !string.IsNullOrWhiteSpace(produto?.Barcode) ? produto!.Barcode!
                : item.ProductId.ToString()[..6];

            var itemRequest = new FocusItemRequest
            {
                NumeroItem             = (index + 1).ToString(),
                CodigoProduto          = codigoProduto,
                Descricao              = item.ProductName,
                QuantidadeComercial    = item.Quantidade.ToString("F2", CultureInfo.InvariantCulture),
                ValorUnitarioComercial = item.ValorUnitario.ToString("F2", CultureInfo.InvariantCulture),
                ValorBruto             = (item.Quantidade * item.ValorUnitario).ToString("F2", CultureInfo.InvariantCulture),
                Cfop                   = item.Cfop,
                CodigoNcm              = ncmLimpo,
                IcmsSituacaoTributaria = string.IsNullOrWhiteSpace(produto?.CSOSN) ? "102" : produto!.CSOSN!.Split('-')[0].Trim(),
                IcmsOrigem             = "0",
                PisSituacaoTributaria     = "99",
                CofinsSituacaoTributaria  = "99",
            };

            // Achado de auditoria (06/08/2026): mesma correção do FiscalService
            // — ICMSSTCalculator existia mas não alimentava nenhuma emissão real.
            if (produto != null && produto.TemSubstituicaoTrib)
            {
                var csosnsComSt = new[] { "201", "202", "203" };
                if (!csosnsComSt.Contains(itemRequest.IcmsSituacaoTributaria))
                    itemRequest.IcmsSituacaoTributaria = "202";

                string ufDestinoItem = string.IsNullOrWhiteSpace(nota.DestinatarioUf) ? "PR" : nota.DestinatarioUf!;
                decimal aliqInterestadualItem = ufDestinoItem == "PR" ? 0m
                    : ERP.Infrastructure.Services.MotorFiscalBrasileiro.ObterAliquotaInterestadual("PR", ufDestinoItem);

                var stCalculator = new Domain.Services.Fiscal.ICMSSTCalculator();
                var st = stCalculator.CalcularDoProduto(produto, item.Quantidade * item.ValorUnitario, aliqInterestadualItem);

                if (st != null)
                {
                    itemRequest.IcmsModalidadeBaseCalculoSt = "4";
                    itemRequest.IcmsMargemValorAdicionadoSt = st.MVAUtilizado.ToString("F2", CultureInfo.InvariantCulture);
                    itemRequest.IcmsBaseCalculoSt            = st.BaseCalculoST.ToString("F2", CultureInfo.InvariantCulture);
                    itemRequest.IcmsAliquotaSt               = (produto.AliquotaInternaUFDest ?? 0m).ToString("F2", CultureInfo.InvariantCulture);
                    itemRequest.IcmsValorSt                  = st.ValorICMSST.ToString("F2", CultureInfo.InvariantCulture);
                }
            }

            itensRequest.Add(itemRequest);
            valorTotalItens += item.Quantidade * item.ValorUnitario;
        }

        string? docLimpo = docLimpoValidacao;
        string? cepLimpo = string.IsNullOrWhiteSpace(nota.DestinatarioCep)
            ? null : new string(nota.DestinatarioCep.Where(char.IsDigit).ToArray());

        // Achado da revisão de arquitetura (18/08) — pagamento real quando
        // a nota avulsa formaliza uma venda B2B de verdade (confirmado que
        // acontece na prática); "90 sem pagamento" continua sendo o default
        // seguro pra remessa/devolução/brinde sem cobrança real.
        var pagamentosRequest = nota.Pagamentos.Any()
            ? nota.Pagamentos.Select(p => new FocusPagamentoRequest
              {
                  FormaPagamento = p.FormaPagamento,
                  ValorPagamento = p.Valor.ToString("F2", CultureInfo.InvariantCulture)
              }).ToList()
            : new List<FocusPagamentoRequest>
              {
                  new() { FormaPagamento = "90", ValorPagamento = valorTotalItens.ToString("F2", CultureInfo.InvariantCulture) }
              };

        // Notas referenciadas — só a devolução exige (já bloqueado acima se
        // faltar), mas se algum dia outro cenário quiser referenciar sem ser
        // devolução, a mesma chave serve.
        List<NotaReferenciadaRequest>? notasReferenciadas = string.IsNullOrWhiteSpace(nota.RefNFe)
            ? new()
            : new() { new NotaReferenciadaRequest { ChaveNfe = new string(nota.RefNFe.Where(char.IsDigit).ToArray()) } };

        // Transporte — só preenche o que existir; "9" (sem frete) fica
        // sozinho quando a operação é retirada/entrega própria.
        // S27 correção (19/08) — mesmo erro do S19 (NotasReferenciadas nulo):
        // Focus espera um array (vazio que seja), nunca `null`, senão
        // rejeita com "erro_validacao_schema" antes de chegar na SEFAZ.
        List<FocusVolumeRequest> volumes = nota.QuantidadeVolumes.HasValue || nota.PesoBrutoKg.HasValue || nota.PesoLiquidoKg.HasValue
            ? new()
              {
                  new FocusVolumeRequest
                  {
                      Quantidade  = nota.QuantidadeVolumes?.ToString(CultureInfo.InvariantCulture),
                      Especie     = nota.EspecieVolumes,
                      PesoBruto   = nota.PesoBrutoKg?.ToString("F3", CultureInfo.InvariantCulture),
                      PesoLiquido = nota.PesoLiquidoKg?.ToString("F3", CultureInfo.InvariantCulture),
                  }
              }
            : new();

        // Transportadora — mesmo padrão de limpeza/decisão CNPJ-ou-CPF já
        // usado pro destinatário, uma vez só em vez de repetir Where().
        string? transportadoraDocLimpo = string.IsNullOrWhiteSpace(nota.TransportadoraDocumento)
            ? null : new string(nota.TransportadoraDocumento.Where(char.IsDigit).ToArray());

        var request = new FocusNfceRequest
        {
            // S21 FIX (aplicado aqui em 18/08) — DateTime.Now+"zzz" depende
            // do fuso AMBIENTE do servidor; já causou rejeição SEFAZ 703
            // (data de emissão no futuro) numa venda normal. Mesmo risco
            // aqui, nunca corrigido nessa tela até agora.
            DataEmissao            = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasilComOffset(),
            // Fix 1 (plano premium) — antes hardcoded "1" (saída), então toda
            // nota de entrada/devolução-de-venda saía errada na SEFAZ.
            TipoDocumento          = nota.TipoOperacaoEntradaSaida == "E" ? "0" : "1",
            NaturezaOperacao       = nota.NaturezaOperacao ?? "VENDA DE MERCADORIA",
            FinalidadeEmissao      = nota.Finalidade,
            CpfCnpj                = docLimpo,
            Nome                   = nota.DestinatarioNome,
            LogradouroDestinatario = string.IsNullOrWhiteSpace(nota.DestinatarioLogradouro) ? "Nao Informado" : nota.DestinatarioLogradouro,
            NumeroDestinatario     = string.IsNullOrWhiteSpace(nota.DestinatarioNumero) ? "S/N" : nota.DestinatarioNumero,
            BairroDestinatario     = string.IsNullOrWhiteSpace(nota.DestinatarioBairro) ? "Centro" : nota.DestinatarioBairro,
            MunicipioDestinatario  = string.IsNullOrWhiteSpace(nota.DestinatarioMunicipio) ? "Curitiba" : nota.DestinatarioMunicipio,
            UfDestinatario         = string.IsNullOrWhiteSpace(nota.DestinatarioUf) ? "PR" : nota.DestinatarioUf,
            CepDestinatario        = string.IsNullOrWhiteSpace(cepLimpo) ? "00000000" : cepLimpo,
            IeDestinatario         = nota.DestinatarioIe,
            // Fix 3 (plano premium) — confirmado na doc da Focus
            // (indicador_inscricao_estadual_destinatario); sem isso, NF-e
            // B2B toma rejeição clássica dependendo do destinatário.
            IndicadorIeDestinatario = nota.IndicadorIeDestinatario,
            Itens                  = itensRequest,
            // Fix 5 (plano premium), refeito em 18/08 — antes SEMPRE "90 sem
            // pagamento", mesmo em venda B2B com cobrança real.
            Pagamentos              = pagamentosRequest,
            NotasReferenciadas      = notasReferenciadas,
            ModalidadeFrete         = nota.ModalidadeFrete,
            CnpjTransportador       = transportadoraDocLimpo?.Length == 14 ? transportadoraDocLimpo : null,
            CpfTransportador        = transportadoraDocLimpo?.Length == 11 ? transportadoraDocLimpo : null,
            NomeTransportador       = nota.TransportadoraNome,
            InscricaoEstadualTransportador = nota.TransportadoraIe,
            EnderecoTransportador   = nota.TransportadoraEndereco,
            MunicipioTransportador  = nota.TransportadoraMunicipio,
            UfTransportador         = nota.TransportadoraUf,
            VeiculoPlaca            = nota.VeiculoPlaca,
            VeiculoUf               = nota.VeiculoUf,
            Volumes                 = volumes,
            InformacoesAdicionaisContribuinte = nota.InformacoesComplementares,
        };

        var referencia = $"avulsa-{nota.Id}";
        var (sucesso, mensagem, urlDanfe, urlXml, chave, numero) = await _nfeService.EmitirNfeA4Async(
            referencia, request, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
        {
            nota.Status      = "Autorizada";
            nota.Chave       = string.IsNullOrWhiteSpace(chave) ? null : chave;
            nota.Numero      = string.IsNullOrWhiteSpace(numero) ? null : numero;
            nota.UrlDanfe    = urlDanfe;
            nota.XmlUrl      = string.IsNullOrWhiteSpace(urlXml) ? null : urlXml;
            nota.Ambiente    = ambienteSefaz;
            nota.DataEmissao = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
            await _ctx.SaveChangesAsync();

            return new FiscalEmissionResult
            {
                Sucesso = true, Mensagem = mensagem, Status = "Autorizada",
                UrlDanfe = urlDanfe, Ambiente = ambienteSefaz
            };
        }

        // Achado testando de verdade (19-20/08) — Focus pode responder
        // "sucesso" sem ainda ter o DANFE pronto (SEFAZ processando de
        // verdade, não é rejeição). Antes disso caía direto no "Falha ao
        // emitir" e a nota ficava parada em Rascunho pra sempre — sem
        // rastro nenhum de que já tinha sido enviada de verdade pra SEFAZ,
        // arriscando reenvio duplicado ou perder o resultado real.
        if (sucesso)
        {
            nota.Status      = "Processando";
            nota.Ambiente    = ambienteSefaz;
            // Achado testando de verdade (20/08) — algumas combinações
            // (ex: destinatário sem IE + "Não contribuinte") parecem nunca
            // sair de "processando_autorizacao" na SEFAZ de homologação,
            // mesmo consultando várias vezes. Guardar quando entrou em
            // Processando é o que permite o sistema se defender sozinho
            // (avisar "travada" depois de um tempo) em vez de ficar
            // dependendo só do usuário lembrar de checar.
            nota.DataEmissao = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
            await _ctx.SaveChangesAsync();

            return new FiscalEmissionResult
            {
                Sucesso = true, Mensagem = mensagem, Status = "Processando", Ambiente = ambienteSefaz
            };
        }

        return new FiscalEmissionResult { Sucesso = false, Mensagem = mensagem, Status = "Falha", Ambiente = ambienteSefaz };
    }

    public async Task<FiscalEmissionResult> CancelarAsync(Guid id, string justificativa)
    {
        // Mesmo achado do EmitirAsync (S27, 20/08) — precisa de AsTracking()
        // pra Status/MotivoCancelamento gravarem de verdade.
        var nota = await _ctx.NotasFiscais.AsTracking().FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != "Autorizada")
            throw new InvalidOperationException(
                $"Só é possível cancelar uma nota Autorizada — essa está \"{nota.Status}\".");

        if (string.IsNullOrWhiteSpace(justificativa) || justificativa.Length < 15)
            throw new InvalidOperationException("Justificativa do cancelamento precisa ter no mínimo 15 caracteres.");

        var config = await _configProvider.ObterConfiguracaoAsync();
        var referencia = $"avulsa-{nota.Id}";

        var (sucesso, mensagem) = await _cancelService.CancelarNotaAsync(
            referencia, justificativa, config.TokenFocusNfe, config.UsarAmbienteProducao, "NFE");

        if (sucesso)
        {
            nota.Status             = "Cancelada";
            nota.MotivoCancelamento = justificativa;
            await _ctx.SaveChangesAsync();

            return new FiscalEmissionResult { Sucesso = true, Mensagem = mensagem, Status = "Cancelada" };
        }

        return new FiscalEmissionResult { Sucesso = false, Mensagem = mensagem, Status = "FalhaCancelamento" };
    }

    /// <summary>Achado testando em homologação (19-20/08) — quando a Focus
    /// responde "processando" (SEFAZ ainda não confirmou), a nota fica
    /// marcada "Processando" em vez de silenciosamente continuar
    /// "Rascunho". Esse método consulta o resultado real mais tarde,
    /// reaproveitando o INfeStatusService que a tela normal de Notas
    /// Fiscais já usa.</summary>
    public async Task<FiscalEmissionResult> ConsultarStatusAsync(Guid id)
    {
        // Mesmo achado do EmitirAsync (S27, 20/08).
        var nota = await _ctx.NotasFiscais.AsTracking().FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != "Processando")
            throw new InvalidOperationException(
                $"Só faz sentido consultar status de uma nota Processando — essa está \"{nota.Status}\".");

        var config = await _configProvider.ObterConfiguracaoAsync();
        var referencia = $"avulsa-{nota.Id}";

        var (sucesso, statusFocus, urlDanfe, _, _, _, _) = await _statusService.ConsultarStatusNotaAsync(
            referencia, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (!sucesso)
            return new FiscalEmissionResult { Sucesso = false, Mensagem = "Não foi possível consultar — tente de novo em instantes.", Status = "Processando" };

        if (statusFocus == "autorizado" && !string.IsNullOrWhiteSpace(urlDanfe))
        {
            nota.Status      = "Autorizada";
            nota.UrlDanfe    = urlDanfe;
            nota.DataEmissao = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
            await _ctx.SaveChangesAsync();
            return new FiscalEmissionResult { Sucesso = true, Mensagem = "NF-e autorizada.", Status = "Autorizada", UrlDanfe = urlDanfe };
        }

        if (statusFocus == "processando_autorizacao")
            return new FiscalEmissionResult { Sucesso = true, Mensagem = "Ainda processando na SEFAZ — tente de novo em instantes.", Status = "Processando" };

        // Qualquer outro status (erro_autorizacao, cancelado, etc.) — a
        // SEFAZ já decidiu, e não foi autorizar. Marca como rejeitada pra
        // não ficar "Processando" pra sempre.
        var motivo = await _statusService.ConsultarMotivoRejeicaoAsync(referencia, config.TokenFocusNfe, config.UsarAmbienteProducao);
        nota.Status = "Rejeitada";
        await _ctx.SaveChangesAsync();
        return new FiscalEmissionResult { Sucesso = false, Mensagem = $"Status: {statusFocus}. {motivo}", Status = "Rejeitada" };
    }

    public async Task<IReadOnlyList<NfeA4HistoricoDto>> ListarHistoricoAsync(
        DateTime? dataInicio = null, DateTime? dataFim = null, string? status = null)
    {
        var query = _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens)
            .Where(n => n.Tipo == "NFE"); // qualquer NF-e A4 — avulsa ou ligada a venda

        if (dataInicio.HasValue) query = query.Where(n => n.DataEmissao >= dataInicio.Value);
        if (dataFim.HasValue) query = query.Where(n => n.DataEmissao <= dataFim.Value.AddDays(1).AddTicks(-1));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(n => n.Status == status);

        var notas = await query.OrderByDescending(n => n.DataEmissao).ToListAsync();

        return notas.Select(n => new NfeA4HistoricoDto(
            n.Id, n.NaturezaOperacao ?? "", n.DestinatarioNome ?? "",
            n.Itens.Sum(i => i.Quantidade * i.ValorUnitario),
            n.Status, n.DataEmissao, n.VendaId == null, n.UrlDanfe)).ToList();
    }
}