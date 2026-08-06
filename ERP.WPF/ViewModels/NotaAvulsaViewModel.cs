// ERP.WPF/ViewModels/NotaAvulsaViewModel.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>Item 1.2 do plano premium — antes era classe "burra" (propriedades
/// auto). Editar Qtd/Valor/CFOP direto na grade não atualizava nem a própria
/// célula Total nem o total geral da nota. Agora notifica.</summary>
/// <summary>Item 1.3 do plano premium — combobox de operações prontas estilo
/// VHSYS. Preenche natureza + E/S + finalidade + CFOP padrão dos itens de
/// uma vez. IMPORTANTE: os CFOPs abaixo são um ponto de partida comum pra
/// matcon — confirme com o contador da Vila Verde antes de confiar de olhos
/// fechados em produção; CFOP errado é responsabilidade fiscal de verdade,
/// não só um bug de sistema.</summary>
public record OperacaoFiscalPreset(
    string Descricao, string NaturezaOperacao, string EntradaSaida, string Finalidade,
    string CfopDentroUf, string CfopForaUf, string PagamentoPadrao = "90");

public static class OperacoesFiscaisPresets
{
    public static readonly OperacaoFiscalPreset[] Lista =
    {
        new("Venda de mercadoria",                    "VENDA DE MERCADORIA",              "S", "1", "5102", "6102"),
        new("Venda sujeita a ST",                      "VENDA DE MERCADORIA SUJEITA A ST",  "S", "1", "5405", "6404"),
        new("Remessa em bonificação/brinde",           "REMESSA EM BONIFICACAO",            "S", "1", "5910", "6910"),
        new("Amostra grátis",                          "AMOSTRA GRATIS",                    "S", "1", "5911", "6911"),
        new("Remessa para demonstração",               "REMESSA PARA DEMONSTRACAO",         "S", "1", "5912", "6912"),
        new("Retorno de demonstração",                 "RETORNO DE DEMONSTRACAO",           "E", "1", "5913", "6913"),
        new("Remessa para conserto",                   "REMESSA PARA CONSERTO",             "S", "1", "5915", "6915"),
        new("Retorno de conserto",                     "RETORNO DE CONSERTO",               "E", "1", "5916", "6916"),
        new("Transferência entre filiais",             "TRANSFERENCIA ENTRE FILIAIS",       "S", "1", "5152", "6152"),
        new("Devolução de compra",                     "DEVOLUCAO DE COMPRA",               "S", "1", "5202", "6202"),
        new("Devolução de venda (entrada)",            "DEVOLUCAO DE VENDA",                "E", "4", "1202", "2202"),
        new("Simples remessa",                         "SIMPLES REMESSA",                   "S", "1", "5949", "6949"),
    };
}
/// <summary>Backlog premium — validações inline. Dígito verificador de
/// verdade (não só contagem de dígitos) — cada validação aqui é uma
/// rejeição da SEFAZ que o cliente nunca chega a ver.</summary>
public static class ValidadorDocumento
{
    public static bool CnpjValido(string cnpj)
    {
        var s = new string((cnpj ?? "").Where(char.IsDigit).ToArray());
        if (s.Length != 14 || s.Distinct().Count() == 1) return false;

        int[] mult1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] mult2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        int soma = 0;
        for (int i = 0; i < 12; i++) soma += (s[i] - '0') * mult1[i];
        int resto = soma % 11;
        int dv1 = resto < 2 ? 0 : 11 - resto;
        if (dv1 != s[12] - '0') return false;

        soma = 0;
        for (int i = 0; i < 13; i++) soma += (s[i] - '0') * mult2[i];
        resto = soma % 11;
        int dv2 = resto < 2 ? 0 : 11 - resto;
        return dv2 == s[13] - '0';
    }

    public static bool CpfValido(string cpf)
    {
        var s = new string((cpf ?? "").Where(char.IsDigit).ToArray());
        if (s.Length != 11 || s.Distinct().Count() == 1) return false;

        int soma = 0;
        for (int i = 0; i < 9; i++) soma += (s[i] - '0') * (10 - i);
        int resto = soma % 11;
        int dv1 = resto < 2 ? 0 : 11 - resto;
        if (dv1 != s[9] - '0') return false;

        soma = 0;
        for (int i = 0; i < 10; i++) soma += (s[i] - '0') * (11 - i);
        resto = soma % 11;
        int dv2 = resto < 2 ? 0 : 11 - resto;
        return dv2 == s[10] - '0';
    }

    /// <summary>Aceita CNPJ (14) ou CPF (11) — nota avulsa pode ter qualquer um.</summary>
    public static bool DocumentoValido(string doc)
    {
        var s = new string((doc ?? "").Where(char.IsDigit).ToArray());
        if (s.Length == 14) return CnpjValido(s);
        if (s.Length == 11) return CpfValido(s);
        return false;
    }

    public static bool CepValido(string? cep)
    {
        var s = new string((cep ?? "").Where(char.IsDigit).ToArray());
        return s.Length == 8;
    }
}

/// <summary>Backlog premium — tradutor de rejeições. Baseado nos códigos
/// mais comuns e bem documentados da SEFAZ (539/204 duplicidade, 225 erro
/// de schema, 230/301 IE do emitente, 889 GTIN, 694 grupo ICMS, 805 IE do
/// destinatário) — casa por PADRÃO no texto da mensagem, não por código
/// numérico exato (não temos certeza do formato exato que a Focus usa pra
/// embutir o código). Ponto de partida — alimente com as rejeições reais
/// que a Vila Verde for tomando; padrão desconhecido só mostra o texto cru.</summary>
public static class TradutorRejeicoes
{
    private static readonly (string Padrao, string MensagemAmigavel, string AcaoSugerida)[] Padroes =
    {
        ("duplicidade", "Já existe uma nota autorizada com esse mesmo número/série.",
            "Geralmente é sincronização — tenta de novo em alguns minutos. Se persistir, confira se essa nota não foi emitida duas vezes."),
        ("ncm", "O código NCM de algum produto não é válido ou está desatualizado.",
            "Confira o NCM no cadastro do produto — precisa ter 8 dígitos e constar na tabela vigente."),
        ("inscri", "Tem um problema com a Inscrição Estadual — sua ou do destinatário.",
            "Confira a IE cadastrada da empresa, ou o indicador de IE do destinatário (contribuinte/isento/não contribuinte)."),
        ("cep", "O CEP informado não bate com o município do destinatário.",
            "Confira se o CEP e o município realmente correspondem um ao outro."),
        ("schema", "O XML tem erro de formatação — campo obrigatório vazio ou caractere não permitido.",
            "Revise se todos os campos obrigatórios foram preenchidos, sem caracteres especiais."),
        ("gtin", "Falta o código de barras (GTIN) de algum produto.",
            "Cadastra o código de barras do produto — ou confirma que ele realmente não tem GTIN."),
        ("cfop", "O CFOP não é compatível com essa operação ou com o estado do destinatário.",
            "Confere se o CFOP bate com dentro/fora do estado — os presets de operação já calculam isso sozinhos."),
        ("não habilitado", "A empresa não está habilitada pra emitir NF-e na SEFAZ.",
            "Verifica com o contador se o cadastro fiscal da empresa está regular."),
        ("grupo de icms", "Falta informação de ICMS pra UF de destino.",
            "Confere se o produto tem CSOSN/situação tributária configurada certo."),
    };

    public static (string MensagemAmigavel, string AcaoSugerida)? Traduzir(string? mensagemOriginal)
    {
        if (string.IsNullOrWhiteSpace(mensagemOriginal)) return null;
        var lower = mensagemOriginal.ToLowerInvariant();
        foreach (var (padrao, amigavel, acao) in Padroes)
            if (lower.Contains(padrao))
                return (amigavel, acao);
        return null;
    }
}

public class ItemNotaAvulsa : BaseViewModel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    private decimal _quantidade;
    public decimal Quantidade
    {
        get => _quantidade;
        set { SetProperty(ref _quantidade, value); OnPropertyChanged(nameof(Total)); }
    }

    private decimal _valorUnitario;
    public decimal ValorUnitario
    {
        get => _valorUnitario;
        set { SetProperty(ref _valorUnitario, value); OnPropertyChanged(nameof(Total)); }
    }

    private string _cfop = "5102";
    public string Cfop { get => _cfop; set => SetProperty(ref _cfop, value); }

    public decimal Total => Quantidade * ValorUnitario;
}

/// <summary>
/// Item 9 do roadmap fiscal — editor de NF-e desacoplada de venda, com
/// rascunho ("salvar sem emitir") e conferência de impostos.
/// </summary>
public class NotaAvulsaViewModel : BaseViewModel
{
    private readonly IProductService _productService;
    private readonly ICustomerService _customerService;
    private Guid? _notaId;

    // ── Cabeçalho ────────────────────────────────────────────────────────
    public string NaturezaOperacao { get; set; } = "VENDA DE MERCADORIA";
    public string[] TiposOperacao { get; } = { "S", "E" };

    // ── Item 1.3 do plano premium: presets de operação ─────────────────────
    public OperacaoFiscalPreset[] PresetsDisponiveis { get; } = OperacoesFiscaisPresets.Lista;

    private OperacaoFiscalPreset? _presetSelecionado;
    public OperacaoFiscalPreset? PresetSelecionado
    {
        get => _presetSelecionado;
        set { SetProperty(ref _presetSelecionado, value); AplicarPreset(value); }
    }

    private void AplicarPreset(OperacaoFiscalPreset? preset)
    {
        if (preset == null) return;

        NaturezaOperacao = preset.NaturezaOperacao;
        TipoOperacaoEntradaSaida = preset.EntradaSaida;
        Finalidade = preset.Finalidade;
        OnPropertyChanged(nameof(NaturezaOperacao));
        OnPropertyChanged(nameof(TipoOperacaoEntradaSaida));
        OnPropertyChanged(nameof(Finalidade));

        AplicarCfopDoPresetSeAtivo();

        bool foraUf = !string.IsNullOrWhiteSpace(DestinatarioUf) && !string.Equals(DestinatarioUf, "PR", StringComparison.OrdinalIgnoreCase);
        CfopItem = foraUf ? preset.CfopForaUf : preset.CfopDentroUf;
        OnPropertyChanged(nameof(CfopItem));
    }

    /// <summary>CFOP dinâmico por UF — quando a UF do destinatário mudar
    /// (inclusive via busca de CNPJ), troca 5xxx↔6xxx sozinho nos itens que
    /// ainda estão no CFOP do preset (não mexe em CFOP que o usuário já
    /// customizou manualmente pra algo fora do preset).</summary>
    private void AplicarCfopDoPresetSeAtivo()
    {
        if (PresetSelecionado == null) return;
        var preset = PresetSelecionado;

        bool destinatarioForaUf = !string.IsNullOrWhiteSpace(DestinatarioUf)
            && !string.Equals(DestinatarioUf, "PR", StringComparison.OrdinalIgnoreCase);
        string cfopAlvo = destinatarioForaUf ? preset.CfopForaUf : preset.CfopDentroUf;

        foreach (var item in Itens)
            if (item.Cfop == preset.CfopDentroUf || item.Cfop == preset.CfopForaUf)
                item.Cfop = cfopAlvo;
    }

    public string TipoOperacaoEntradaSaida { get; set; } = "S";
    public string Finalidade { get; set; } = "1";

    // ── Destinatário ─────────────────────────────────────────────────────
    public string DestinatarioNome { get; set; } = string.Empty;

    private string? _destinatarioDocumento;
    public string? DestinatarioDocumento
    {
        get => _destinatarioDocumento;
        set
        {
            SetProperty(ref _destinatarioDocumento, value);
            var limpo = new string((value ?? "").Where(char.IsDigit).ToArray());
            if (limpo.Length == 14) _ = BuscarCnpjAsync(limpo);
        }
    }

    private string? _situacaoCadastral;
    /// <summary>"ATIVA" (verde) ou outra coisa (amarelo) — vender pra CNPJ
    /// baixado é dor de cabeça que dá pra evitar de graça.</summary>
    public string? SituacaoCadastral { get => _situacaoCadastral; set => SetProperty(ref _situacaoCadastral, value); }
    public string? DestinatarioLogradouro { get; set; }
    public string? DestinatarioNumero { get; set; }
    public string? DestinatarioBairro { get; set; }
    public string? DestinatarioMunicipio { get; set; }
    private string? _destinatarioUf;
    public string? DestinatarioUf
    {
        get => _destinatarioUf;
        set { SetProperty(ref _destinatarioUf, value); AplicarCfopDoPresetSeAtivo(); }
    }
    public string? DestinatarioCep { get; set; }
    public string? DestinatarioIe { get; set; }

    public string[] IndicadoresIe { get; } = { "1", "2", "9" };
    public string IndicadorIeDestinatario { get; set; } = "9";

    // ── Item picker (mesmo padrão da tela de Compras) ──────────────────────
    private string _buscaProduto = string.Empty;
    public string BuscaProduto
    {
        get => _buscaProduto;
        set { SetProperty(ref _buscaProduto, value); _ = BuscarProdutosAsync(value); }
    }

    private ProductDto? _produtoSelecionado;
    public ProductDto? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set { SetProperty(ref _produtoSelecionado, value); if (value != null) ValorUnitarioItem = value.SalePrice; }
    }

    public ObservableCollection<ProductDto> ProdutosSugestao { get; } = new();

    public decimal QuantidadeItem { get; set; } = 1;
    public decimal ValorUnitarioItem { get; set; }
    public string CfopItem { get; set; } = "5102";

    public ObservableCollection<ItemNotaAvulsa> Itens { get; } = new();
    public decimal Total => Itens.Sum(i => i.Total);

    // ── Rascunhos existentes ────────────────────────────────────────────
    public ObservableCollection<NotaFiscalAvulsaResumoDto> Rascunhos { get; } = new();

    private string _statusTexto = string.Empty;
    public string StatusTexto { get => _statusTexto; set => SetProperty(ref _statusTexto, value); }

    // ── Backlog premium: autosave. Como a maioria dos campos são
    // propriedades "burras" (sem notificação por tecla), em vez de refazer
    // o formulário inteiro pra ser reativo, um timer compara periodicamente
    // um retrato do estado atual — funciona igual pro usuário (salva sozinho
    // uns segundos depois de parar de digitar) sem o refactor grande.
    private System.Windows.Threading.DispatcherTimer? _autosaveTimer;
    private string _ultimoSnapshotVisto = "";
    private string _ultimoSnapshotSalvo = "";
    private int _tiquesEstavel;

    private string _autosaveStatusTexto = string.Empty;
    public string AutosaveStatusTexto { get => _autosaveStatusTexto; set => SetProperty(ref _autosaveStatusTexto, value); }

    public ICommand AdicionarItemCommand { get; }
    public ICommand BuscarCnpjCommand { get; }
    public ICommand RemoverItemCommand { get; }
    public ICommand SalvarRascunhoCommand { get; }
    public ICommand ConferirCommand { get; }
    public ICommand GerarPdfEspelhoCommand { get; }
    public ICommand EmitirCommand { get; }
    public ICommand NovaNotaCommand { get; }
    public ICommand CarregarRascunhoCommand { get; }
    public ICommand CopiarNotaCommand { get; }
    public ICommand ExcluirRascunhoCommand { get; }
    public ICommand AtualizarRascunhosCommand { get; }

    public NotaAvulsaViewModel(IProductService productService, ICustomerService customerService)
    {
        _productService = productService;
        _customerService = customerService;

        AdicionarItemCommand = new RelayCommand(_ => AdicionarItem(), _ => ProdutoSelecionado != null && QuantidadeItem > 0);
        BuscarCnpjCommand = new AsyncRelayCommand(async _ =>
        {
            var limpo = new string((DestinatarioDocumento ?? "").Where(char.IsDigit).ToArray());
            if (limpo.Length != 14) { MessageBox.Show("Digite um CNPJ válido (14 dígitos).", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            await BuscarCnpjAsync(limpo);
        });
        RemoverItemCommand   = new RelayCommand(item => { if (item is ItemNotaAvulsa i) { Itens.Remove(i); OnPropertyChanged(nameof(Total)); } });
        SalvarRascunhoCommand = new AsyncRelayCommand(async _ => await SalvarRascunhoAsync());
        ConferirCommand        = new AsyncRelayCommand(async _ => await ConferirAsync());
        GerarPdfEspelhoCommand = new AsyncRelayCommand(async _ => await GerarPdfEspelhoAsync());
        EmitirCommand           = new AsyncRelayCommand(async _ => await EmitirAsync());
        NovaNotaCommand         = new RelayCommand(_ => LimparFormulario());
        CarregarRascunhoCommand = new AsyncRelayCommand(async item => { if (item is NotaFiscalAvulsaResumoDto r) await CarregarRascunhoAsync(r.Id); });
        CopiarNotaCommand = new AsyncRelayCommand(async item => { if (item is NotaFiscalAvulsaResumoDto r) await CopiarNotaAsync(r.Id); });
        ExcluirRascunhoCommand  = new AsyncRelayCommand(async item => { if (item is NotaFiscalAvulsaResumoDto r) await ExcluirRascunhoAsync(r.Id); });
        AtualizarRascunhosCommand = new AsyncRelayCommand(async _ => await CarregarRascunhosAsync());

        _ = CarregarRascunhosAsync();
        IniciarAutosave();
    }

    private void IniciarAutosave()
    {
        _autosaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autosaveTimer.Tick += async (_, _) => await VerificarAutosaveAsync();
        _autosaveTimer.Start();
    }

    private string CalcularSnapshot() =>
        $"{NaturezaOperacao}|{TipoOperacaoEntradaSaida}|{DestinatarioNome}|{DestinatarioDocumento}|{DestinatarioLogradouro}|" +
        $"{DestinatarioNumero}|{DestinatarioBairro}|{DestinatarioMunicipio}|{DestinatarioUf}|{DestinatarioCep}|{DestinatarioIe}|" +
        string.Join(",", Itens.Select(i => $"{i.ProductId}:{i.Quantidade}:{i.ValorUnitario}:{i.Cfop}"));

    /// <summary>Chamado a cada segundo. Salva sozinho depois de ~3s sem
    /// mudança no formulário — nunca interrompe o usuário, nunca mostra
    /// diálogo, só atualiza o indicador discreto.</summary>
    private async Task VerificarAutosaveAsync()
    {
        var atual = CalcularSnapshot();

        if (atual == _ultimoSnapshotVisto)
        {
            _tiquesEstavel++;
        }
        else
        {
            _ultimoSnapshotVisto = atual;
            _tiquesEstavel = 0;
        }

        bool temConteudoRelevante = !string.IsNullOrWhiteSpace(DestinatarioNome) && Itens.Any();

        if (_tiquesEstavel == 3 && atual != _ultimoSnapshotSalvo && temConteudoRelevante)
        {
            try
            {
                var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
                _notaId = await service.SalvarRascunhoAsync(MontarDto());
                _ultimoSnapshotSalvo = atual;
                AutosaveStatusTexto = $"💾 Salvo automaticamente às {DateTime.Now:HH:mm}";
                await CarregarRascunhosAsync();
            }
            catch { /* autosave é best-effort — nunca deve incomodar o usuário com erro */ }
        }
    }

    /// <summary>Reseta o rastreamento do autosave — chamado depois de um
    /// salvamento manual (evita autosave redundante logo em seguida) e ao
    /// trocar de nota (evita comparar contra o retrato da nota anterior).</summary>
    private void ResetarRastreamentoAutosave()
    {
        _ultimoSnapshotSalvo = CalcularSnapshot();
        _ultimoSnapshotVisto = _ultimoSnapshotSalvo;
        _tiquesEstavel = 0;
    }

    private async Task BuscarProdutosAsync(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2) { ProdutosSugestao.Clear(); return; }
        try
        {
            var resultado = await _productService.SearchAsync(termo);
            ProdutosSugestao.Clear();
            foreach (var p in resultado.Take(8)) ProdutosSugestao.Add(p);
        }
        catch { ProdutosSugestao.Clear(); }
    }

    private void AdicionarItemComListener(ItemNotaAvulsa item)
    {
        item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ItemNotaAvulsa.Total)) OnPropertyChanged(nameof(Total)); };
        Itens.Add(item);
    }

    private void AdicionarItem()
    {
        if (ProdutoSelecionado == null) return;

        AdicionarItemComListener(new ItemNotaAvulsa
        {
            ProductId     = ProdutoSelecionado.Id,
            ProductName   = ProdutoSelecionado.Name,
            Quantidade    = QuantidadeItem,
            ValorUnitario = ValorUnitarioItem,
            Cfop          = CfopItem,
        });
        OnPropertyChanged(nameof(Total));

        BuscaProduto = string.Empty;
        ProdutoSelecionado = null;
        QuantidadeItem = 1;
        ValorUnitarioItem = 0;
        CfopItem = "5102";
    }

    private async Task BuscarCnpjAsync(string cnpjLimpo)
    {
        StatusTexto = "Buscando CNPJ...";
        try
        {
            // Prioridade 1: já é cliente cadastrado? Puxa do cadastro (tem IE, telefone, tudo).
            var clientes = await _customerService.SearchAsync(cnpjLimpo);
            var clienteExistente = clientes.FirstOrDefault(c =>
                new string(c.Document.Where(char.IsDigit).ToArray()) == cnpjLimpo);

            if (clienteExistente != null)
            {
                DestinatarioNome = clienteExistente.Name;
                DestinatarioLogradouro = clienteExistente.Street;
                DestinatarioNumero = clienteExistente.Number;
                DestinatarioBairro = clienteExistente.Neighborhood;
                DestinatarioMunicipio = clienteExistente.City;
                DestinatarioUf = clienteExistente.State;
                DestinatarioCep = clienteExistente.ZipCode;
                DestinatarioIe = clienteExistente.Ie;
                IndicadorIeDestinatario = string.IsNullOrWhiteSpace(clienteExistente.Ie) ? "9" : "1";
                SituacaoCadastral = null; // já é cliente confiável, não precisa do selo
                OnPropertyChanged(string.Empty);
                StatusTexto = "Preenchido a partir do cadastro de clientes.";
                return;
            }

            // Prioridade 2: consulta a Receita via BrasilAPI (S11, já existia, só não era usado aqui)
            var brasilApi = App.Services.GetRequiredService<ERP.Infrastructure.Services.BrasilApiService>();
            var resultado = await brasilApi.ConsultarCnpjAsync(cnpjLimpo);

            if (resultado == null)
            {
                StatusTexto = brasilApi.CircuitAberto
                    ? "Consulta de CNPJ temporariamente indisponível — preencha manualmente."
                    : "CNPJ não encontrado — preencha manualmente.";
                return;
            }

            DestinatarioNome = string.IsNullOrWhiteSpace(resultado.NomeFantasia) ? resultado.RazaoSocial : resultado.NomeFantasia;
            DestinatarioLogradouro = resultado.Logradouro;
            DestinatarioNumero = resultado.Numero;
            DestinatarioBairro = resultado.Bairro;
            DestinatarioMunicipio = resultado.Municipio;
            DestinatarioUf = resultado.Uf;
            DestinatarioCep = resultado.Cep;
            DestinatarioIe = null; // IE é estadual, BrasilAPI não traz — fica manual
            IndicadorIeDestinatario = "9";
            SituacaoCadastral = resultado.DescricaoSituacaoCadastral;
            OnPropertyChanged(string.Empty);
            StatusTexto = "Preenchido pela Receita Federal.";

            if (!string.Equals(resultado.DescricaoSituacaoCadastral, "ATIVA", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    $"⚠️ Atenção: a situação cadastral desse CNPJ é \"{resultado.DescricaoSituacaoCadastral}\", não ATIVA. Confirme antes de emitir nota pra essa empresa.",
                    "Situação Cadastral", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var salvar = MessageBox.Show(
                $"Quer salvar \"{DestinatarioNome}\" como cliente cadastrado, pra próxima vez vir automático?",
                "Salvar Cliente", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (salvar == MessageBoxResult.Yes)
            {
                try
                {
                    await _customerService.CreateAsync(new CreateCustomerDto
                    {
                        Document     = cnpjLimpo,
                        Name         = DestinatarioNome,
                        Street       = DestinatarioLogradouro,
                        Number       = DestinatarioNumero,
                        Neighborhood = DestinatarioBairro,
                        City         = DestinatarioMunicipio,
                        State        = DestinatarioUf,
                        ZipCode      = DestinatarioCep,
                        Email        = resultado.Email,
                        Phone        = resultado.DddTelefone1,
                    });
                }
                catch (Exception exCliente)
                {
                    MessageBox.Show($"Não foi possível salvar como cliente: {exCliente.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            StatusTexto = string.Empty;
            MessageBox.Show($"Erro ao buscar CNPJ: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { if (StatusTexto.StartsWith("Buscando")) StatusTexto = string.Empty; }
    }

    private SalvarNotaFiscalAvulsaDto MontarDto() => new()
    {
        Id                        = _notaId,
        NaturezaOperacao          = NaturezaOperacao,
        TipoOperacaoEntradaSaida  = TipoOperacaoEntradaSaida,
        Finalidade                = Finalidade,
        DestinatarioNome          = DestinatarioNome,
        DestinatarioDocumento     = DestinatarioDocumento,
        DestinatarioLogradouro    = DestinatarioLogradouro,
        DestinatarioNumero        = DestinatarioNumero,
        DestinatarioBairro        = DestinatarioBairro,
        DestinatarioMunicipio     = DestinatarioMunicipio,
        DestinatarioUf            = DestinatarioUf,
        DestinatarioCep           = DestinatarioCep,
        DestinatarioIe            = DestinatarioIe,
        IndicadorIeDestinatario   = IndicadorIeDestinatario,
        Itens = Itens.Select(i => new NotaFiscalAvulsaItemDto
        {
            ProductId = i.ProductId, ProductName = i.ProductName,
            Quantidade = i.Quantidade, ValorUnitario = i.ValorUnitario, Cfop = i.Cfop,
        }).ToList(),
    };

    private async Task SalvarRascunhoAsync()
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            _notaId = await service.SalvarRascunhoAsync(MontarDto());
            ResetarRastreamentoAutosave();
            MessageBox.Show("✅ Rascunho salvo!", "Nota Avulsa", MessageBoxButton.OK, MessageBoxImage.Information);
            await CarregarRascunhosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ConferirAsync()
    {
        try
        {
            // Precisa ter salvo antes — a conferência lê do banco.
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            _notaId = await service.SalvarRascunhoAsync(MontarDto());

            var conferencia = await service.ConferirAsync(_notaId.Value);

            var texto = string.Join("\n", conferencia.Itens.Select(i =>
                $"{i.ProductName} — {i.Quantidade}x R$ {i.ValorUnitario:N2} = R$ {i.ValorTotal:N2} | ICMS: R$ {i.Tributos.ValorIcms:N2} | ICMS-ST: R$ {i.Tributos.ValorIcmsSt:N2}"));

            MessageBox.Show(
                $"CONFERÊNCIA DE IMPOSTOS\n\n{texto}\n\n" +
                $"Total produtos: R$ {conferencia.ValorTotalProdutos:N2}\nTotal impostos (ICMS+ST): R$ {conferencia.ValorTotalImpostos:N2}\n\n" +
                "⚠️ Isso não é o DANFE — a SEFAZ só gera o documento oficial depois de autorizar. Confira os valores antes de emitir.",
                "Conferência Fiscal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao conferir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task GerarPdfEspelhoAsync()
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            _notaId = await service.SalvarRascunhoAsync(MontarDto());

            var conferencia = await service.ConferirAsync(_notaId.Value);
            var nota = await service.ObterAsync(_notaId.Value);
            if (nota == null) return;

            var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
            var relatorio = new ERP.WPF.Reports.NotaAvulsaEspelhoPdfReport(nota, conferencia, config);
            ERP.WPF.Reports.PdfReportBase.SalvarEAbrir(relatorio, $"Espelho_NotaAvulsa_{DestinatarioNome.Replace(" ", "_")}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar PDF: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<string> ValidarFormulario()
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(DestinatarioNome))
            erros.Add("Nome do destinatário é obrigatório.");

        if (!string.IsNullOrWhiteSpace(DestinatarioDocumento) && !ValidadorDocumento.DocumentoValido(DestinatarioDocumento))
            erros.Add("CPF/CNPJ do destinatário é inválido (dígito verificador não confere).");

        if (!string.IsNullOrWhiteSpace(DestinatarioCep) && !ValidadorDocumento.CepValido(DestinatarioCep))
            erros.Add("CEP precisa ter 8 dígitos.");

        if (!Itens.Any())
            erros.Add("Adicione pelo menos um item.");

        foreach (var item in Itens)
        {
            if (item.Quantidade <= 0)
                erros.Add($"Item \"{item.ProductName}\": quantidade precisa ser maior que zero.");
            if (item.ValorUnitario <= 0)
                erros.Add($"Item \"{item.ProductName}\": valor unitário precisa ser maior que zero.");
        }

        return erros;
    }

    private async Task EmitirAsync()
    {
        var erros = ValidarFormulario();
        if (erros.Any())
        {
            MessageBox.Show(
                "Corrija antes de emitir:\n\n• " + string.Join("\n• ", erros),
                "Formulário incompleto", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmacao = MessageBox.Show(
            $"Emitir NF-e avulsa pra {DestinatarioNome} — total R$ {Total:N2}?\n\nIsso transmite pra SEFAZ de verdade.",
            "Confirmar Emissão", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            StatusTexto = "Salvando e emitindo...";
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            _notaId = await service.SalvarRascunhoAsync(MontarDto());

            var resultado = await service.EmitirAsync(_notaId.Value);

            if (resultado.Sucesso)
            {
                MessageBox.Show($"✅ {resultado.Mensagem}", "Nota Emitida", MessageBoxButton.OK, MessageBoxImage.Information);
                LimparFormulario();
                await CarregarRascunhosAsync();
            }
            else
            {
                var traducao = TradutorRejeicoes.Traduzir(resultado.Mensagem);
                var mensagemFinal = traducao == null
                    ? $"❌ Falha ao emitir:\n{resultado.Mensagem}"
                    : $"❌ {traducao.Value.MensagemAmigavel}\n\n💡 {traducao.Value.AcaoSugerida}\n\nMensagem original da SEFAZ:\n{resultado.Mensagem}";
                MessageBox.Show(mensagemFinal, "Erro Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao emitir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { StatusTexto = string.Empty; }
    }

    private async Task CarregarRascunhosAsync()
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            var lista = await service.ListarAsync();
            Rascunhos.Clear();
            foreach (var r in lista) Rascunhos.Add(r);
        }
        catch { /* best-effort */ }
    }

    private async Task CarregarRascunhoAsync(Guid id)
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            var nota = await service.ObterAsync(id);
            if (nota == null) return;

            if (nota.Status != "Rascunho")
            {
                MessageBox.Show("Essa nota já foi emitida — não é possível editar, só consultar na tela de Notas Fiscais.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _notaId = nota.Id;
            NaturezaOperacao = nota.NaturezaOperacao;
            TipoOperacaoEntradaSaida = nota.TipoOperacaoEntradaSaida;
            Finalidade = nota.Finalidade;
            DestinatarioNome = nota.DestinatarioNome;
            DestinatarioDocumento = nota.DestinatarioDocumento;
            DestinatarioLogradouro = nota.DestinatarioLogradouro;
            DestinatarioNumero = nota.DestinatarioNumero;
            DestinatarioBairro = nota.DestinatarioBairro;
            DestinatarioMunicipio = nota.DestinatarioMunicipio;
            DestinatarioUf = nota.DestinatarioUf;
            DestinatarioCep = nota.DestinatarioCep;
            DestinatarioIe = nota.DestinatarioIe;
            IndicadorIeDestinatario = nota.IndicadorIeDestinatario;

            Itens.Clear();
            foreach (var i in nota.Itens)
                AdicionarItemComListener(new ItemNotaAvulsa
                {
                    ProductId = i.ProductId, ProductName = i.ProductName,
                    Quantidade = i.Quantidade, ValorUnitario = i.ValorUnitario, Cfop = i.Cfop,
                });

            OnPropertyChanged(string.Empty); // atualiza todo o formulário de uma vez
            ResetarRastreamentoAutosave();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar rascunho: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CopiarNotaAsync(Guid idOrigem)
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            var novoId = await service.CopiarComoRascunhoAsync(idOrigem);
            await CarregarRascunhoAsync(novoId);
            await CarregarRascunhosAsync();
            StatusTexto = "Nota copiada — revise e emita quando quiser.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao copiar nota: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExcluirRascunhoAsync(Guid id)
    {
        var confirmacao = MessageBox.Show("Excluir esse rascunho? Não dá pra desfazer.", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            await service.ExcluirRascunhoAsync(id);
            await CarregarRascunhosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao excluir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LimparFormulario()
    {
        _notaId = null;
        _presetSelecionado = null; // sem disparar AplicarPreset de novo
        NaturezaOperacao = "VENDA DE MERCADORIA";
        Finalidade = "1";
        TipoOperacaoEntradaSaida = "S";
        DestinatarioNome = string.Empty;
        DestinatarioDocumento = null;
        DestinatarioLogradouro = null;
        DestinatarioNumero = null;
        DestinatarioBairro = null;
        DestinatarioMunicipio = null;
        DestinatarioUf = null;
        DestinatarioCep = null;
        DestinatarioIe = null;
        Itens.Clear();
        OnPropertyChanged(string.Empty);
        AutosaveStatusTexto = string.Empty;
        ResetarRastreamentoAutosave();
    }
}