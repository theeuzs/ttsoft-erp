// ── ERP.Infrastructure/Services/ImportacaoService.cs ────────────────────────
using System.Globalization;
using System.Text;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Importador de dados de sistema anterior (CSV de produtos/clientes,
/// incluindo saldo de fiado) — apontado como prioridade nº1 de feature nova
/// em várias auditorias/roadmaps: sem isso, cada implantação de cliente novo
/// exige digitar o catálogo inteiro na mão.
///
/// Decisão de design: sem mapeador de coluna arrastar-e-soltar (isso seria
/// uma tela própria, bem mais trabalho). Em vez disso, casa os cabeçalhos do
/// arquivo contra uma lista generosa de sinônimos em português (com e sem
/// acento, maiúscula/minúscula) — cobre a grande maioria dos exports reais de
/// planilha/sistema antigo sem exigir nenhuma configuração da pessoa. Se uma
/// coluna obrigatória não for reconhecida, a prévia avisa exatamente qual
/// nome de coluna esperar, e a pessoa só renomeia no Excel.
/// </summary>
public class ImportacaoService : IImportacaoService
{
    private readonly IUnitOfWork _uow;
    private readonly IContaReceberService _contaReceberService;

    public ImportacaoService(IUnitOfWork uow, IContaReceberService contaReceberService)
    {
        _uow = uow;
        _contaReceberService = contaReceberService;
    }

    // Campo do sistema -> lista de nomes de coluna aceitos (já normalizados:
    // minúsculo, sem acento, sem espaço extra — a comparação normaliza os
    // dois lados igual).
    private static readonly Dictionary<string, string[]> SinonimosProdutos = new()
    {
        ["Nome"]         = ["nome", "produto", "descricao", "nomeproduto", "descricaoproduto"],
        ["CodigoBarras"] = ["codigobarras", "codbarras", "barcode", "ean", "gtin"],
        ["SKU"]          = ["sku", "codigo", "codigointerno", "referencia", "ref"],
        ["PrecoVenda"]   = ["precovenda", "preco", "valorvenda", "precodevenda", "valor"],
        ["PrecoCusto"]   = ["precocusto", "custo", "valorcusto", "precodecusto"],
        ["Estoque"]      = ["estoque", "quantidade", "qtd", "qtdestoque", "saldo", "estoqueatual"],
        ["Unidade"]      = ["unidade", "un", "unid", "unidademedida"],
    };

    private static readonly Dictionary<string, string[]> SinonimosClientes = new()
    {
        ["Nome"]        = ["nome", "cliente", "nomecliente", "razaosocial"],
        ["Document"]    = ["documento", "cpf", "cnpj", "cpfcnpj", "documentocliente"],
        ["Phone"]       = ["telefone", "celular", "fone", "whatsapp", "contato"],
        ["Email"]       = ["email", "e-mail"],
        ["Street"]      = ["endereco", "rua", "logradouro"],
        ["Number"]      = ["numero", "num"],
        ["Neighborhood"]= ["bairro"],
        ["City"]        = ["cidade", "municipio"],
        ["State"]       = ["estado", "uf"],
        ["SaldoFiado"]  = ["saldofiado", "fiado", "saldodevedor", "divida", "debito", "saldoaprazo"],
        ["Haver"]       = ["haver", "credito", "saldocredito", "saldohaver"],
    };

    private static readonly string[] CamposObrigatoriosProdutos = ["Nome", "PrecoVenda"];
    private static readonly string[] CamposObrigatoriosClientes = ["Nome"];

    public async Task<PreviaImportacaoDto> PreVisualizarAsync(byte[] conteudoArquivo, TipoImportacao tipo)
    {
        var (linhasCru, colunas) = LerArquivo(conteudoArquivo);
        var sinonimos = tipo == TipoImportacao.Produtos ? SinonimosProdutos : SinonimosClientes;
        var obrigatorios = tipo == TipoImportacao.Produtos ? CamposObrigatoriosProdutos : CamposObrigatoriosClientes;

        var mapeamento = DetectarMapeamento(colunas, sinonimos);
        var faltando = obrigatorios.Where(c => mapeamento.GetValueOrDefault(c) == null).ToList();

        var previa = new PreviaImportacaoDto
        {
            TipoImportacao = tipo,
            ColunasDetectadasNoArquivo = colunas,
            MapeamentoDetectado = mapeamento,
            CamposObrigatoriosFaltando = faltando
        };

        if (faltando.Count > 0) return previa; // sem campo obrigatório, nem vale processar linha

        var numero = 1; // linha 1 = cabeçalho; primeira linha de dado já é 2
        foreach (var linhaCru in linhasCru)
        {
            numero++;
            var linha = new LinhaImportacao { NumeroLinha = numero, ValoresBrutos = linhaCru };

            if (tipo == TipoImportacao.Produtos)
                await ValidarLinhaProdutoAsync(linha, mapeamento);
            else
                await ValidarLinhaClienteAsync(linha, mapeamento);

            previa.Linhas.Add(linha);
        }

        return previa;
    }

    public async Task<ResultadoImportacaoDto> ConfirmarImportacaoAsync(PreviaImportacaoDto previa)
    {
        var resultado = new ResultadoImportacaoDto();

        foreach (var linha in previa.Linhas.Where(l => !l.TemErro))
        {
            try
            {
                if (previa.TipoImportacao == TipoImportacao.Produtos)
                    await GravarProdutoAsync(linha, previa.MapeamentoDetectado, resultado);
                else
                    await GravarClienteAsync(linha, previa.MapeamentoDetectado, resultado);
            }
            catch (Exception ex)
            {
                resultado.ComErro++;
                resultado.MensagensErro.Add($"Linha {linha.NumeroLinha}: {ex.Message}");
            }
        }

        return resultado;
    }

    // ── Leitura do arquivo ──────────────────────────────────────────────────

    /// <summary>Detecta codificação (UTF-8 vs Latin1/ISO-8859-1 — comum em
    /// export de planilha antiga) e separador (";" vs "," — planilha em
    /// pt-BR usa vírgula como separador decimal, então exporta CSV com
    /// ponto-e-vírgula) sozinho, sem pedir nada pra pessoa.</summary>
    private static (List<Dictionary<string, string>> linhas, List<string> colunas) LerArquivo(byte[] conteudo)
    {
        var texto = DecodificarTexto(conteudo);
        var todasLinhas = texto.Replace("\r\n", "\n").Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (todasLinhas.Length == 0) return ([], []);

        var separador = todasLinhas[0].Count(c => c == ';') > todasLinhas[0].Count(c => c == ',') ? ';' : ',';

        var cabecalho = DividirLinhaCsv(todasLinhas[0], separador)
            .Select(NormalizarCabecalho).ToList();

        var linhas = new List<Dictionary<string, string>>();
        for (var i = 1; i < todasLinhas.Length; i++)
        {
            var campos = DividirLinhaCsv(todasLinhas[i], separador);
            var linha = new Dictionary<string, string>();
            for (var c = 0; c < cabecalho.Count && c < campos.Count; c++)
                linha[cabecalho[c]] = campos[c].Trim();
            linhas.Add(linha);
        }

        return (linhas, cabecalho);
    }

    private static string DecodificarTexto(byte[] conteudo)
    {
        // UTF-8 com caractere de substituição (�) indica que não era UTF-8 de
        // verdade — comum em export de sistema antigo/Excel salvo como
        // "CSV (separado por vírgulas)" no Windows em português, que sai em
        // ISO-8859-1 (Latin1), não UTF-8.
        var comoUtf8 = Encoding.UTF8.GetString(conteudo);
        if (!comoUtf8.Contains('\uFFFD')) return comoUtf8;
        return Encoding.GetEncoding("ISO-8859-1").GetString(conteudo);
    }

    /// <summary>Split simples respeitando campos entre aspas (onde o próprio
    /// separador pode aparecer dentro do texto, ex: endereço com vírgula).</summary>
    private static List<string> DividirLinhaCsv(string linha, char separador)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        foreach (var ch in linha)
        {
            if (ch == '"') { dentroDeAspas = !dentroDeAspas; continue; }
            if (ch == separador && !dentroDeAspas) { campos.Add(atual.ToString()); atual.Clear(); continue; }
            atual.Append(ch);
        }
        campos.Add(atual.ToString());
        return campos;
    }

    private static string NormalizarCabecalho(string valor)
    {
        var semAcento = RemoverAcentos(valor.Trim().ToLowerInvariant());
        return new string(semAcento.Where(c => char.IsLetterOrDigit(c)).ToArray());
    }

    private static string RemoverAcentos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizado)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    private static Dictionary<string, string?> DetectarMapeamento(List<string> colunasArquivo, Dictionary<string, string[]> sinonimos)
    {
        var mapa = new Dictionary<string, string?>();
        foreach (var (campoSistema, aliases) in sinonimos)
            mapa[campoSistema] = colunasArquivo.FirstOrDefault(col => aliases.Contains(col));
        return mapa;
    }

    // ── Validação — Produtos ────────────────────────────────────────────────

    private async Task ValidarLinhaProdutoAsync(LinhaImportacao linha, Dictionary<string, string?> mapa)
    {
        var nome = ValorDe(linha, mapa, "Nome");
        if (string.IsNullOrWhiteSpace(nome)) linha.Erros.Add("Nome vazio.");

        var precoTexto = ValorDe(linha, mapa, "PrecoVenda");
        if (!TentarParseDecimal(precoTexto, out var preco) || preco <= 0)
            linha.Erros.Add($"Preço de venda inválido: \"{precoTexto}\".");

        var barcode = ValorDe(linha, mapa, "CodigoBarras");
        var sku = ValorDe(linha, mapa, "SKU");

        if (!string.IsNullOrWhiteSpace(barcode))
            linha.JaExiste = await _uow.Products.GetByBarcodeAsync(barcode) != null;
        else if (!string.IsNullOrWhiteSpace(sku))
            linha.JaExiste = await _uow.Products.GetBySkuAsync(sku) != null;
    }

    private async Task GravarProdutoAsync(LinhaImportacao linha, Dictionary<string, string?> mapa, ResultadoImportacaoDto resultado)
    {
        var barcode = ValorDe(linha, mapa, "CodigoBarras");
        var sku = ValorDe(linha, mapa, "SKU");

        Product? existente = null;
        if (!string.IsNullOrWhiteSpace(barcode)) existente = await _uow.Products.GetByBarcodeAsync(barcode);
        else if (!string.IsNullOrWhiteSpace(sku)) existente = await _uow.Products.GetBySkuAsync(sku);

        TentarParseDecimal(ValorDe(linha, mapa, "PrecoVenda"), out var precoVenda);
        TentarParseDecimal(ValorDe(linha, mapa, "PrecoCusto"), out var precoCusto);
        TentarParseDecimal(ValorDe(linha, mapa, "Estoque"), out var estoque);
        var unidade = ValorDe(linha, mapa, "Unidade");

        if (existente != null)
        {
            existente.Name = ValorDe(linha, mapa, "Nome") ?? existente.Name;
            existente.SalePrice = precoVenda;
            if (precoCusto > 0) existente.OriginalCost = precoCusto;
            if (!string.IsNullOrWhiteSpace(unidade)) existente.Unit = unidade;
            _uow.Products.Update(existente);
            resultado.Atualizados++;
        }
        else
        {
            await _uow.Products.AddAsync(new Product
            {
                Name = ValorDe(linha, mapa, "Nome") ?? "",
                Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode,
                SKU = string.IsNullOrWhiteSpace(sku) ? null : sku,
                SalePrice = precoVenda,
                OriginalCost = precoCusto,
                Stock = estoque,
                Unit = string.IsNullOrWhiteSpace(unidade) ? "UN" : unidade,
                IsActive = true
            });
            resultado.Criados++;
        }

        await _uow.CommitAsync();
    }

    // ── Validação — Clientes ────────────────────────────────────────────────

    private async Task ValidarLinhaClienteAsync(LinhaImportacao linha, Dictionary<string, string?> mapa)
    {
        var nome = ValorDe(linha, mapa, "Nome");
        if (string.IsNullOrWhiteSpace(nome)) linha.Erros.Add("Nome vazio.");

        var documento = ValorDe(linha, mapa, "Document");
        if (!string.IsNullOrWhiteSpace(documento))
            linha.JaExiste = await _uow.Customers.GetByDocumentAsync(documento) != null;

        var saldoFiadoTexto = ValorDe(linha, mapa, "SaldoFiado");
        if (!string.IsNullOrWhiteSpace(saldoFiadoTexto) && !TentarParseDecimal(saldoFiadoTexto, out _))
            linha.Erros.Add($"Saldo de fiado inválido: \"{saldoFiadoTexto}\".");

        var haverTexto = ValorDe(linha, mapa, "Haver");
        if (!string.IsNullOrWhiteSpace(haverTexto) && !TentarParseDecimal(haverTexto, out _))
            linha.Erros.Add($"Saldo de haver inválido: \"{haverTexto}\".");
    }

    private async Task GravarClienteAsync(LinhaImportacao linha, Dictionary<string, string?> mapa, ResultadoImportacaoDto resultado)
    {
        var documento = ValorDe(linha, mapa, "Document");
        var existente = !string.IsNullOrWhiteSpace(documento)
            ? await _uow.Customers.GetByDocumentAsync(documento)
            : null;

        TentarParseDecimal(ValorDe(linha, mapa, "Haver"), out var haver);

        Customer cliente;
        if (existente != null)
        {
            existente.Name = ValorDe(linha, mapa, "Nome") ?? existente.Name;
            existente.Phone = ValorDe(linha, mapa, "Phone") ?? existente.Phone;
            existente.Email = ValorDe(linha, mapa, "Email") ?? existente.Email;
            existente.Street = ValorDe(linha, mapa, "Street") ?? existente.Street;
            existente.Number = ValorDe(linha, mapa, "Number") ?? existente.Number;
            existente.Neighborhood = ValorDe(linha, mapa, "Neighborhood") ?? existente.Neighborhood;
            existente.City = ValorDe(linha, mapa, "City") ?? existente.City;
            existente.State = ValorDe(linha, mapa, "State") ?? existente.State;
            _uow.Customers.Update(existente);
            cliente = existente;
            resultado.Atualizados++;
        }
        else
        {
            cliente = new Customer
            {
                Name = ValorDe(linha, mapa, "Nome") ?? "",
                Document = documento ?? "",
                Phone = ValorDe(linha, mapa, "Phone"),
                Email = ValorDe(linha, mapa, "Email"),
                Street = ValorDe(linha, mapa, "Street"),
                Number = ValorDe(linha, mapa, "Number"),
                Neighborhood = ValorDe(linha, mapa, "Neighborhood"),
                City = ValorDe(linha, mapa, "City"),
                State = ValorDe(linha, mapa, "State"),
                // Achado (roadmap/auditorias) — Haver é campo direto no
                // cliente (não um "movimento" a registrar), correto seedar
                // direto aqui: é o ESTADO INICIAL, não uma transação.
                HaverBalance = haver
            };
            await _uow.Customers.AddAsync(cliente);
            resultado.Criados++;
        }

        await _uow.CommitAsync();

        // Achado (roadmap/auditorias, item mais sensível da migração) —
        // saldo de fiado do sistema anterior vira uma ContaReceber de
        // verdade, reaproveitando GerarContaAPrazoAsync (já testado), em vez
        // de um número solto — assim aparece certo em cobrança/relatório,
        // igual qualquer outra conta a prazo.
        TentarParseDecimal(ValorDe(linha, mapa, "SaldoFiado"), out var saldoFiado);
        if (saldoFiado > 0)
        {
            await _contaReceberService.GerarContaAPrazoAsync(
                cliente.Id, null, saldoFiado, "Saldo migrado do sistema anterior");
        }
    }

    // ── Utilidades compartilhadas ───────────────────────────────────────────

    private static string? ValorDe(LinhaImportacao linha, Dictionary<string, string?> mapa, string campoSistema)
    {
        var coluna = mapa.GetValueOrDefault(campoSistema);
        if (coluna == null) return null;
        return linha.ValoresBrutos.GetValueOrDefault(coluna)?.Trim();
    }

    /// <summary>Aceita tanto "1234.56" quanto "1234,56" (planilha BR usa
    /// vírgula decimal) e ignora separador de milhar comum ("1.234,56").</summary>
    private static bool TentarParseDecimal(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpo = texto.Trim();
        // Se tem vírgula E ponto, o último a aparecer é o decimal de verdade.
        if (limpo.Contains(',') && limpo.Contains('.'))
        {
            limpo = limpo.LastIndexOf(',') > limpo.LastIndexOf('.')
                ? limpo.Replace(".", "").Replace(",", ".")
                : limpo.Replace(",", "");
        }
        else if (limpo.Contains(','))
        {
            limpo = limpo.Replace(",", ".");
        }

        return decimal.TryParse(limpo, NumberStyles.Any, CultureInfo.InvariantCulture, out valor);
    }
}
