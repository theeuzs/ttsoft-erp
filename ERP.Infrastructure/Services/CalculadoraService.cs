// ── ERP.Infrastructure/Services/CalculadoraService.cs ────────────────────────
// I.2: toda a lógica de 605 linhas extraída do CalculadoraController.
// O controller ficou com ~40 linhas (só roteamento HTTP).
// Os templates, cálculos e integrações com estoque/orçamento vivem aqui.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Services;

public class CalculadoraService : ICalculadoraService
{
    private readonly IServiceProvider  _sp;
    private readonly IRequestTenant    _tenant;
    private readonly IOrcamentoService _orcService;

    public CalculadoraService(
        IServiceProvider  sp,
        IRequestTenant    tenant,
        IOrcamentoService orcService)
    {
        _sp         = sp;
        _tenant     = tenant;
        _orcService = orcService;
    }

    // ── Templates (movidos do controller) ────────────────────────────────────

    private static readonly Dictionary<string, TemplateObra> _templates = new()
    {
        ["piso_ceramico"] = new TemplateObra(
            Nome:      "Piso Cerâmico",
            Descricao: "Assentamento de piso cerâmico ou porcelanato com argamassa colante",
            Icone:     "🏠",
            Parametros: [new("area_m2", "Área total (m²)", "m²", 1, 10000)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var perda = 1.10m;
                return [
                    new("Cerâmica / Porcelanato",   area * perda,                        "m²",        "+10% de perda e quebra"),
                    new("Argamassa Colante AC-II",  Math.Ceiling(area / 3m),             "saco 20kg", "1 saco a cada 3m² — dupla colagem"),
                    new("Rejunte",                  Math.Ceiling(area * 0.5m),           "kg",        "0,5kg/m² — válido para juntas de 3mm"),
                    new("Espaçador de Piso",        Math.Ceiling(area * 16 / 100),       "pct 100un", "16 unidades por m²"),
                    new("Impermeabilizante Rodapé", Math.Ceiling(area * 0.12m),          "litro",     "Somente para áreas molhadas"),
                ];
            }),

        ["reboco_interno"] = new TemplateObra(
            Nome:      "Reboco Interno",
            Descricao: "Chapisco + emboço + massa corrida para paredes internas",
            Icone:     "🧱",
            Parametros: [new("area_m2", "Área de parede (m²)", "m²", 1, 5000)],
            CalcularMateriais: p => {
                var area = p["area_m2"];
                return [
                    new("Argamassa p/ Chapisco",  Math.Ceiling(area * 1.5m / 20),  "saco 20kg", "1,5kg/m² — traço 1:3 cimento:areia"),
                    new("Argamassa p/ Emboço",    Math.Ceiling(area * 12 / 20),    "saco 20kg", "12kg/m² — argamassa industrializada"),
                    new("Massa Corrida PVA",       Math.Ceiling(area / 3m / 18),   "galão 18L", "1 galão cobre ~54m² (2 demãos)"),
                    new("Lixa para Massa #120",    Math.Ceiling(area / 5),         "folha",     "1 folha por 5m²"),
                    new("Selador Acrílico",        Math.Ceiling(area * 0.12m / 18),"galão 18L", "0,12L/m² — fixação da massa"),
                ];
            }),

        ["pintura_latex"] = new TemplateObra(
            Nome:      "Pintura Látex PVA",
            Descricao: "Pintura com tinta látex PVA — parede interna",
            Icone:     "🎨",
            Parametros: [new("area_m2", "Área de parede (m²)", "m²", 1, 10000), new("demaos", "Número de demãos", "demãos", 1, 4)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var maos = p.GetValueOrDefault("demaos", 2); var litros = area * maos / 10m;
                return [
                    new("Tinta Látex PVA",     Math.Ceiling(litros / 18),             "galão 18L", $"{maos} demãos — rendimento 10m²/L/demão"),
                    new("Selador Acrílico",    Math.Ceiling(area * 0.08m / 18),       "galão 18L", "1ª mão — 1L/12m²"),
                    new("Lixa d'água #180",    Math.Ceiling(area / 8),                "folha",     "Entre demãos — 1 folha/8m²"),
                    new("Rolo de Lã 23cm",     Math.Max(2, Math.Ceiling(area / 60m)), "un",        "1 rolo por 60m² — mínimo 2"),
                    new("Pincel 2 pol.",        2,                                     "un",        "Cantos e rodapés"),
                    new("Fita Crepe 50mm",      Math.Ceiling(area / 40m),             "rolo 50m",  "Proteção de rodapés e teto"),
                ];
            }),

        ["pintura_acrilica"] = new TemplateObra(
            Nome:      "Pintura Acrílica — Fachada",
            Descricao: "Pintura externa com tinta acrílica premium sobre reboco",
            Icone:     "🏢",
            Parametros: [new("area_m2", "Área de fachada (m²)", "m²", 5, 5000), new("demaos", "Número de demãos", "demãos", 2, 3)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var maos = p.GetValueOrDefault("demaos", 2); var litros = area * maos / 8m;
                return [
                    new("Tinta Acrílica Premium",   Math.Ceiling(litros / 18),             "galão 18L", $"{maos} demãos — rendimento 8m²/L/demão"),
                    new("Selador Fachada",           Math.Ceiling(area * 0.12m / 18),       "galão 18L", "1L/8m² — fundamental para fixação"),
                    new("Massa Acrílica Fachada",    Math.Ceiling(area / 3m / 18),          "galão 18L", "Correção de imperfeições"),
                    new("Fita Crepe 50mm",           Math.Ceiling(area / 40m),              "rolo 50m",  "Proteção de esquadrias e frisos"),
                    new("Lixa Grossa #80",           Math.Ceiling(area / 10m),              "folha",     "Para preparo da superfície"),
                    new("Rolo Fachada 23cm",         Math.Max(2, Math.Ceiling(area / 40m)), "un",        "Rolo texturizado — 1 por 40m²"),
                ];
            }),

        ["alvenaria"] = new TemplateObra(
            Nome:      "Alvenaria de Tijolos",
            Descricao: "Parede de tijolos cerâmicos 8 furos com argamassa",
            Icone:     "🧱",
            Parametros: [new("area_m2", "Área de parede (m²)", "m²", 1, 2000)],
            CalcularMateriais: p => {
                var area = p["area_m2"];
                return [
                    new("Tijolo Cerâmico 8 Furos",  Math.Ceiling(area * 36 * 1.05m / 100) * 100, "un",       "36un/m² + 5% quebra"),
                    new("Cimento CP-II",             Math.Ceiling(area * 10 / 50),                "saco 50kg","10kg/m² de argamassa de assentamento"),
                    new("Areia Grossa",              Math.Ceiling(area * 0.03m),                  "m³",       "0,03m³/m² — traço 1:4 cimento:areia"),
                    new("Arame Recozido 18",         Math.Ceiling(area * 0.2m),                   "kg",       "Amarração das fiadas e vergas"),
                ];
            }),

        ["contrapiso"] = new TemplateObra(
            Nome:      "Contrapiso / Lastro",
            Descricao: "Contrapiso de concreto para regularização — com tela de reforço",
            Icone:     "🏗️",
            Parametros: [new("area_m2", "Área (m²)", "m²", 1, 5000), new("espessura_cm", "Espessura (cm)", "cm", 3, 10)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var esp = p.GetValueOrDefault("espessura_cm", 5m) / 100m; var vol = area * esp;
                return [
                    new("Cimento CP-II",           Math.Ceiling(vol * 350 / 50), "saco 50kg", "350kg/m³ de concreto — traço 1:2:3"),
                    new("Areia Média",             Math.Ceiling(vol * 0.60m),    "m³",        "0,60m³/m³ de concreto"),
                    new("Brita 0/1",               Math.Ceiling(vol * 0.75m),    "m³",        "0,75m³/m³ de concreto"),
                    new("Tela Soldada Q-92",       Math.Ceiling(area * 1.05m),   "m²",        "+5% sobreposição — reforço estrutural"),
                    new("Tábua de Forma 2,5x30cm", Math.Ceiling(area * 0.4m),   "m",         "Guias perimetrais de nível"),
                ];
            }),

        ["impermeabilizacao"] = new TemplateObra(
            Nome:      "Impermeabilização",
            Descricao: "Impermeabilização de laje, banheiro ou caixa d'água",
            Icone:     "💧",
            Parametros: [new("area_m2", "Área (m²)", "m²", 1, 2000), new("demaos", "Nº de demãos", "demãos", 2, 4)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var maos = p.GetValueOrDefault("demaos", 3m);
                return [
                    new("Impermeabilizante Acrílico", Math.Ceiling(area * maos * 0.4m / 18), "galão 18L", "0,4L/m²/demão"),
                    new("Tela de Poliéster 100g",     Math.Ceiling(area * 1.05m),             "m²",        "Reforço de juntas e cantos — +5% sobreposição"),
                    new("Primer / Preparador",        Math.Ceiling(area * 0.10m),             "litro",     "Preparação da superfície"),
                    new("Rolo Especial p/ Imperm.",   Math.Max(2, Math.Ceiling(area / 30m)),  "un",        "1 rolo por 30m²"),
                    new("Espátula / Trincha 3 pol.",  2,                                      "un",        "Cantos e remates"),
                ];
            }),

        ["telhado_ceramica"] = new TemplateObra(
            Nome:      "Telhado — Telha Cerâmica",
            Descricao: "Cobertura com telhas cerâmicas tipo francesa ou romana",
            Icone:     "🏡",
            Parametros: [new("area_m2", "Área do telhado (m²)", "m²", 10, 5000), new("inclinacao", "Inclinação (%)", "%", 15, 45)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var incl = p.GetValueOrDefault("inclinacao", 30m);
                var fatorI = 1m + (incl / 100m) * 0.10m; var areaReal = area * fatorI;
                return [
                    new("Telha Cerâmica Francesa",  Math.Ceiling(areaReal * 16 * 1.05m), "un",  "16un/m² + 5% quebra"),
                    new("Cumeeira Cerâmica",        Math.Ceiling(area * 0.08m),          "un",  "Linear da cumeeira (~8% da área em planta)"),
                    new("Caibro 6x6cm",             Math.Ceiling(areaReal * 1.5m),       "m",   "Espaçamento 50cm — estrutura principal"),
                    new("Ripa 2x5cm",               Math.Ceiling(areaReal * 3.5m),       "m",   "Espaçamento ~33cm → 3m/m² + 10%"),
                    new("Prego 17x27",              Math.Ceiling(areaReal * 0.20m),      "kg",  "Fixação das ripas nos caibros"),
                    new("Manta Subcobertura",       Math.Ceiling(areaReal * 1.05m),      "m²",  "Proteção térmica e impermeabilizante"),
                ];
            }),

        ["telhado_fibrocimento"] = new TemplateObra(
            Nome:      "Telhado — Fibrocimento",
            Descricao: "Cobertura com telhas onduladas de fibrocimento (Brasilit/similar)",
            Icone:     "🏭",
            Parametros: [new("area_m2", "Área do telhado (m²)", "m²", 10, 5000), new("inclinacao", "Inclinação (%)", "%", 10, 35)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var incl = p.GetValueOrDefault("inclinacao", 15m);
                var fatorI = 1m + (incl / 100m) * 0.08m; var areaReal = area * fatorI;
                var numTelhas = Math.Ceiling(areaReal / 1.10m);
                return [
                    new("Telha Ondulada Fibrocimento 2,44m", numTelhas,                    "un",  "Área útil ~1,10m² por telha"),
                    new("Parafuso Especial p/ Telha",        numTelhas * 4,                "un",  "4 parafusos por telha"),
                    new("Cumeeira Fibrocimento",             Math.Ceiling(area * 0.10m),   "m",   "Comprimento da cumeeira + 10%"),
                    new("Caibro 6x6cm",                     Math.Ceiling(areaReal * 1.0m), "m",   "Espaçamento 1,0-1,25m"),
                    new("Terça Madeira 6x12cm",             Math.Ceiling(areaReal * 0.85m),"m",   "Espaçamento 1,25m — apoio direto das telhas"),
                    new("Prego 17x27",                      Math.Ceiling(areaReal * 0.15m),"kg",  "Fixação da estrutura"),
                ];
            }),

        ["eletrica_residencial"] = new TemplateObra(
            Nome:      "Instalação Elétrica Residencial",
            Descricao: "Fiação e eletrodutos para residência padrão — ABNT NBR 5410",
            Icone:     "⚡",
            Parametros: [new("area_m2", "Área da casa (m²)", "m²", 30, 1000)],
            CalcularMateriais: p => {
                var area = p["area_m2"];
                return [
                    new("Fio Flexível 1,5mm² — Iluminação",  Math.Ceiling(area * 2.5m),  "m",  "2,5m/m² — circuitos de iluminação"),
                    new("Fio Flexível 2,5mm² — Tomadas",     Math.Ceiling(area * 3.0m),  "m",  "3,0m/m² — tomadas de uso geral"),
                    new("Fio Flexível 6mm² — Chuveiro/AC",   Math.Ceiling(area * 0.5m),  "m",  "Pontos de alta potência"),
                    new("Eletroduto PVC 3/4 pol.",            Math.Ceiling(area * 1.5m),  "m",  "Para embutir fiação nas paredes"),
                    new("Caixa de Luz 4x2 pol.",              Math.Ceiling(area * 0.5m),  "un", "Tomadas e interruptores — 1 por 2m²"),
                    new("Caixa Octogonal 4x4 pol.",           Math.Ceiling(area * 0.10m), "un", "Pontos de luminária — 1 por 10m²"),
                    new("Disjuntor Unipolar 16A",             Math.Ceiling(area * 0.10m), "un", "1 por circuito"),
                    new("Quadro de Distribuição 12 Disj.",    Math.Ceiling(area / 100m),  "un", "1 quadro por 100m²"),
                    new("Tomada 2P+T (padrão brasileiro)",    Math.Ceiling(area * 0.3m),  "un", "NBR: mínimo 1 tomada a cada 3,5m de parede"),
                    new("Interruptor Simples",                Math.Ceiling(area * 0.08m), "un", "~1 por cômodo"),
                ];
            }),

        ["hidraulica_residencial"] = new TemplateObra(
            Nome:      "Instalação Hidráulica Residencial",
            Descricao: "Tubulação de água fria e quente para casa residencial",
            Icone:     "🚿",
            Parametros: [new("area_m2", "Área da casa (m²)", "m²", 30, 1000)],
            CalcularMateriais: p => {
                var area = p["area_m2"];
                return [
                    new("Tubo PVC Água Fria 25mm",     Math.Ceiling(area * 1.2m),  "m",        "Alimentação principal — 1,2m/m²"),
                    new("Tubo PVC Água Fria 20mm",     Math.Ceiling(area * 2.0m),  "m",        "Distribuição por cômodo"),
                    new("Tubo CPVC Água Quente 22mm",  Math.Ceiling(area * 0.8m),  "m",        "Se houver aquecimento central ou solar"),
                    new("Registro de Gaveta 3/4 pol.", Math.Ceiling(area * 0.05m), "un",       "1 por ponto de consumo principal"),
                    new("Joelho 90° 25mm",             Math.Ceiling(area * 0.6m),  "un",       "Mudança de direção"),
                    new("Tê 25mm",                     Math.Ceiling(area * 0.25m), "un",       "Derivações"),
                    new("Tubo PVC Esgoto 100mm",       Math.Ceiling(area * 0.5m),  "m",        "Coleta sanitária principal (WC)"),
                    new("Tubo PVC Esgoto 50mm",        Math.Ceiling(area * 0.8m),  "m",        "Pias, ralos e lavatórios"),
                    new("Cola para PVC",               Math.Ceiling(area / 80m),   "lata 175g","1 lata por 80m²"),
                    new("Caixa Sifonada 15x15",        Math.Ceiling(area * 0.04m), "un",       "1 por banheiro/área de serviço"),
                    new("Caixa d'Água 1000L",          Math.Ceiling(area / 150m),  "un",       "NBR: 150L/pessoa — ~4 pessoas/100m²"),
                ];
            }),

        ["forro_pvc"] = new TemplateObra(
            Nome:      "Forro de PVC",
            Descricao: "Forro em réguas de PVC com estrutura em alumínio",
            Icone:     "🏠",
            Parametros: [new("area_m2", "Área do teto (m²)", "m²", 5, 2000)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var perda = 1.10m;
                var perimetro = (decimal)Math.Sqrt((double)area) * 4 * 1.10m;
                return [
                    new("Régua PVC 10cm",                    Math.Ceiling(area * perda),    "m²",  "+10% de perda nas juntas e cortes"),
                    new("Perfil U Alumínio — Parede",        Math.Ceiling(perimetro),        "m",   "Perímetro do ambiente +10%"),
                    new("Mata-Junta / Perfil T Interm.",     Math.Ceiling(area * 2.0m),      "m",   "Suporte intermediário — espaç. 60cm"),
                    new("Pendural Aramado",                  Math.Ceiling(area * 0.8m),      "un",  "1 a cada 1,25m²"),
                    new("Arame Galvanizado 18",              Math.Ceiling(area * 0.05m),     "kg",  "Amarração e ajuste de nível"),
                    new("Parafuso Bucha 6x30mm",             Math.Ceiling(perimetro * 2),    "un",  "Fixação do perfil U na parede"),
                    new("Parafuso Cabeça Panela 3,5x25mm",  Math.Ceiling(area * 4),         "un",  "Fixação da mata-junta no pendural"),
                ];
            }),

        ["drywall"] = new TemplateObra(
            Nome:      "Drywall — Parede Gesso Acartonado",
            Descricao: "Parede simples de gesso acartonado em estrutura metálica",
            Icone:     "🗂️",
            Parametros: [new("area_m2", "Área de parede (m²)", "m²", 1, 2000), new("altura_m", "Altura do pé-direito (m)", "m", 2.4m, 4)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var altura = p.GetValueOrDefault("altura_m", 2.8m); var perda = 1.10m;
                var compLinear = area / altura; var numMontantes = Math.Ceiling(compLinear / 0.60m) + 1;
                return [
                    new("Chapa Drywall Standard 1,20x2,40m", Math.Ceiling(area * perda / 2.88m),        "un",       "Área chapa 2,88m² — +10% corte"),
                    new("Guia 70mm",                          Math.Ceiling(compLinear * 2 * 1.05m),      "m",        "Piso + teto — 2 × comprimento linear +5%"),
                    new("Montante 70mm",                      numMontantes * Math.Ceiling(altura),        "m",        "Espaçamento 60cm"),
                    new("Parafuso Drywall 3,5x25mm",          Math.Ceiling(area * 15),                   "un",       "~15 parafusos por chapa"),
                    new("Parafuso Flangeado 3,5x9,5mm",       Math.Ceiling(numMontantes * 4),            "un",       "Fixação guia↔montante"),
                    new("Fita Papel Microperforado",          Math.Ceiling(compLinear * 2.5m),           "m",        "Juntas entre chapas"),
                    new("Massa para Drywall",                 Math.Ceiling(area / 40m),                  "lata 20kg","1 lata por 40m²"),
                    new("Lixa #120",                          Math.Ceiling(area / 10m),                  "folha",    "Acabamento das juntas"),
                ];
            }),

        ["reboco_externo"] = new TemplateObra(
            Nome:      "Reboco Externo — Fachada",
            Descricao: "Chapisco + emboço com argamassa para paredes externas",
            Icone:     "🏗️",
            Parametros: [new("area_m2", "Área de fachada (m²)", "m²", 5, 5000)],
            CalcularMateriais: p => {
                var area = p["area_m2"];
                return [
                    new("Cimento CP-II — Chapisco",  Math.Ceiling(area * 2.0m / 50),  "saco 50kg","Chapisco traço 1:3 — 2kg cimento/m²"),
                    new("Areia Grossa",              Math.Ceiling(area * 0.04m),      "m³",       "Para chapisco e emboço"),
                    new("Argamassa Industrializada", Math.Ceiling(area * 18 / 20),    "saco 20kg","18kg/m² — emboço 15mm"),
                    new("Tela Argamassada 25x25mm",  Math.Ceiling(area * 1.05m),      "m²",       "Reforço em junções de materiais diferentes"),
                    new("Selador Externo",           Math.Ceiling(area * 0.10m / 18), "galão 18L","Preparação antes de pintar"),
                ];
            }),

        ["laje"] = new TemplateObra(
            Nome:      "Laje de Concreto",
            Descricao: "Laje maciça ou com tavela — concretagem + forma + armação",
            Icone:     "🏛️",
            Parametros: [new("area_m2", "Área da laje (m²)", "m²", 5, 3000), new("espessura_cm", "Espessura (cm)", "cm", 8, 20)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var esp = p.GetValueOrDefault("espessura_cm", 12m) / 100m; var vol = area * esp;
                return [
                    new("Cimento CP-II",               Math.Ceiling(vol * 350 / 50), "saco 50kg","350kg/m³ — fck 20MPa"),
                    new("Areia Média",                 Math.Ceiling(vol * 0.60m),    "m³",       "Traço 1:2:3"),
                    new("Brita 1",                     Math.Ceiling(vol * 0.75m),    "m³",       "Traço 1:2:3"),
                    new("Ferro CA-50 10mm (vergalhão)", Math.Ceiling(area * 1.5m),   "kg",       "~1,5kg/m² — armação principal"),
                    new("Ferro CA-60 4,2mm (frei)",    Math.Ceiling(area * 0.5m),    "kg",       "Estribos e amarração"),
                    new("Arame Recozido 18",           Math.Ceiling(area * 0.3m),    "kg",       "Amarração da armadura"),
                    new("Tábua de Pinho 2,5x30cm",     Math.Ceiling(area * 3m),      "m",        "Forma de madeira — ~3m/m²"),
                    new("Pontalete 3x3cm",             Math.Ceiling(area * 1.2m),    "m",        "Escoramento da forma"),
                    new("Pregos 18x27",                Math.Ceiling(area * 0.3m),    "kg",       "Montagem da forma"),
                    new("Aditivo Plastificante",       Math.Ceiling(vol * 1.5m),     "litro",    "1,5L/m³ — melhora trabalhabilidade"),
                ];
            }),

        ["calcada"] = new TemplateObra(
            Nome:      "Calçada / Passeio",
            Descricao: "Calçada em concreto ou revestida com piso intertravado",
            Icone:     "🚶",
            Parametros: [new("area_m2", "Área (m²)", "m²", 5, 2000), new("espessura_cm", "Espessura do concreto (cm)", "cm", 6, 12)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var esp = p.GetValueOrDefault("espessura_cm", 8m) / 100m; var vol = area * esp;
                return [
                    new("Cimento CP-II",            Math.Ceiling(vol * 300 / 50),   "saco 50kg","300kg/m³ — concreto calçada fck 15MPa"),
                    new("Areia Grossa",             Math.Ceiling(vol * 0.70m),      "m³",       "Traço 1:2,5:3,5"),
                    new("Brita 0",                  Math.Ceiling(vol * 0.80m),      "m³",       "Brita miúda para calçada"),
                    new("Tela Soldada Q-75",         Math.Ceiling(area * 1.05m),    "m²",       "+5% sobreposição — tela de reforço"),
                    new("Tábua de Forma 2,5x20cm",  Math.Ceiling((decimal)Math.Sqrt((double)area) * 4 * 1.1m), "m", "Guias perimetrais"),
                    new("Prego 17x27",              Math.Ceiling(area * 0.05m),     "kg",       "Montagem das formas"),
                    new("Desforma (desmoldante)",    Math.Ceiling(area * 0.05m),    "litro",    "Aplicar na forma antes de concretar"),
                ];
            }),

        ["piscina_revestimento"] = new TemplateObra(
            Nome:      "Piscina — Revestimento e Impermeabilização",
            Descricao: "Impermeabilização + revestimento de piscina em pastilha ou porcelanato",
            Icone:     "🏊",
            Parametros: [new("area_m2", "Área total interna (m²)", "m²", 10, 500), new("tipo_revest", "Tipo: 1=Pastilha 2=Porcelanato", "", 1, 2)],
            CalcularMateriais: p => {
                var area = p["area_m2"]; var tipo = (int)p.GetValueOrDefault("tipo_revest", 1m);
                var perdaRevest = tipo == 1 ? 1.15m : 1.10m;
                return [
                    new("Impermeabilizante Bicomponente", Math.Ceiling(area * 1.5m / 20),  "balde 20kg","1,5kg/m² — 2 demãos — específico piscina"),
                    new("Tela de Poliéster 50g",          Math.Ceiling(area * 1.05m),       "m²",        "Reforço em toda a superfície"),
                    new(tipo == 1 ? "Pastilha Cerâmica 5x5cm" : "Porcelanato Externo 30x30cm",
                        Math.Ceiling(area * perdaRevest), "m²", tipo == 1 ? "+15% corte de pastilha" : "+10% porcelanato"),
                    new("Argamassa Colante AC-III",        Math.Ceiling(area * 7 / 20),      "saco 20kg", "7kg/m² — AC-III resistente à imersão"),
                    new("Rejunte para Piscina",            Math.Ceiling(area * 0.8m),        "kg",        "0,8kg/m² — rejunte específico p/ piscina"),
                    new("Cola de Fundo (Primer Piscina)",  Math.Ceiling(area * 0.15m),       "litro",     "Aderência entre concreto e imperm."),
                ];
            }),
    };

    // ── Implementação da interface ─────────────────────────────────────────────

    public IReadOnlyList<TemplateObraInfo> GetTemplates()
    {
        return _templates.Select(t => new TemplateObraInfo(
            t.Key,
            t.Value.Nome,
            t.Value.Descricao,
            t.Value.Icone,
            t.Value.Parametros.Select(p => new ParametroInfo(p.Nome, p.Label, p.Unidade, p.Min, p.Max)).ToList()
        )).ToList();
    }

    public CalcResultado Calcular(string template, Dictionary<string, decimal> parametros)
    {
        if (!_templates.TryGetValue(template, out var t))
            throw new KeyNotFoundException($"Template '{template}' não encontrado.");

        var materiais = t.CalcularMateriais(parametros)
            .Select(m => new Application.Interfaces.MaterialItem(m.Nome, m.Quantidade, m.Unidade, m.Observacao))
            .ToList();

        return new CalcResultado(template, t.Nome, materiais, materiais.Count, DateTime.Now);
    }

    public async Task<CalcComEstoqueResultado> CalcularComEstoqueAsync(
        string template, Dictionary<string, decimal> parametros, CancellationToken ct = default)
    {
        if (!_templates.TryGetValue(template, out var t))
            throw new KeyNotFoundException($"Template '{template}' não encontrado.");

        var materiais = t.CalcularMateriais(parametros);

        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var produtos = await ctx.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Stock > 0)
            .ToListAsync(ct);

        var resultado = materiais.Select(m =>
        {
            var prod = produtos.FirstOrDefault(p =>
                p.Name.Contains(m.Nome.Split(' ')[0], StringComparison.OrdinalIgnoreCase));

            return new MaterialComEstoque(
                m.Nome, m.Quantidade, m.Unidade, m.Observacao,
                prod == null ? null : new EstoqueMatch(
                    prod.Name, prod.Stock, prod.SalePrice,
                    prod.SalePrice * m.Quantidade,
                    prod.Stock >= m.Quantidade));
        }).ToList();

        var totalEstimado = resultado
            .Where(r => r.ProdutoEstoque != null)
            .Sum(r => r.ProdutoEstoque!.TotalEstimado);

        return new CalcComEstoqueResultado(t.Nome, resultado, totalEstimado, DateTime.Now);
    }

    public async Task<OrcamentoGeradoResultado> GerarOrcamentoAsync(
        string template, Dictionary<string, decimal> parametros,
        string? clienteNome, Guid? clienteId, CancellationToken ct = default)
    {
        if (!_templates.TryGetValue(template, out var t))
            throw new KeyNotFoundException($"Template '{template}' não encontrado.");

        var materiais = t.CalcularMateriais(parametros);

        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var produtos = await ctx.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        var itens = materiais
            .Select(m =>
            {
                var prod = produtos.FirstOrDefault(p =>
                    p.Name.Contains(m.Nome.Split(' ')[0], StringComparison.OrdinalIgnoreCase));

                return new OrcamentoItemDto
                {
                    ProductId       = prod?.Id ?? Guid.Empty,
                    ProductName     = prod?.Name ?? m.Nome,
                    Quantity        = m.Quantidade,
                    UnitPrice       = prod?.SalePrice ?? 0,
                    DiscountPercent = 0
                };
            })
            .Where(i => i.ProductId != Guid.Empty)
            .ToList();

        if (!itens.Any())
            throw new InvalidOperationException(
                "Nenhum material encontrado no estoque. Cadastre os produtos primeiro.");

        var dto = new CreateOrcamentoDto
        {
            CustomerName = clienteNome ?? "Calculadora de Materiais",
            CustomerId   = clienteId,
            SellerName   = _tenant.UserName,
            UsuarioId    = _tenant.UserId,
            Itens        = itens
        };

        var orcamento = await _orcService.SalvarOrcamentoAsync(dto);
        return new OrcamentoGeradoResultado(orcamento.Id, orcamento.Numero, "Orçamento gerado com sucesso.");
    }

    // ── Tipos internos aninhados ───────────────────────────────────────────────
    private record TemplateObra(
        string                                                       Nome,
        string                                                       Descricao,
        string                                                       Icone,
        List<ParametroTemplate>                                      Parametros,
        Func<Dictionary<string, decimal>, List<MaterialItemInterno>> CalcularMateriais);

    private record ParametroTemplate(string Nome, string Label, string Unidade, decimal Min, decimal Max);
    private record MaterialItemInterno(string Nome, decimal Quantidade, string Unidade, string Observacao);
}