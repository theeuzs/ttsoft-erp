using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeContingencyWorker
{
    private readonly IServiceProvider _serviceProvider;

    public NfeContingencyWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    // 🟢 Agora ele recebe uma função anônima para buscar o Token sem conhecer o WPF!
    public async Task IniciarTrabalhoEmBackgroundAsync(Func<(string Token, bool IsProducao)> getConfig)
    {
        while (true)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var contingencyService = scope.ServiceProvider.GetRequiredService<INfeContingencyService>();
                
                var pendentes = await contingencyService.ObterNotasPendentesAsync();
                
                if (System.Linq.Enumerable.Any(pendentes) && await contingencyService.VerificarConexaoSefazAsync())
                {
                    var nfceService = scope.ServiceProvider.GetRequiredService<INfceEmissionService>();
                    var nfeService = scope.ServiceProvider.GetRequiredService<INfeEmissionService>();
                    var saleService = scope.ServiceProvider.GetRequiredService<ISaleService>();
                    
                    // 👇 Executa a função que o App.xaml.cs vai passar 👇
                    var config = getConfig();
                    string ambienteSefaz = config.IsProducao ? "Produção" : "Homologação";

                    foreach (var nota in pendentes)
                    {
                        try
                        {
                            var request = Newtonsoft.Json.JsonConvert.DeserializeObject<FocusNfceRequest>(nota.PayloadJson);
                            bool sucesso = false; string mensagem = ""; string urlDanfe = "";

                            if (nota.TipoNota == "NFCE")
                            {
                                var result = await nfceService.EmitirNfceAsync(nota.Referencia, request, config.Token, config.IsProducao);
                                sucesso = result.Sucesso; mensagem = result.Mensagem; urlDanfe = result.UrlDanfe;
                            }
                            else
                            {
                                var result = await nfeService.EmitirNfeA4Async(nota.Referencia, request, config.Token, config.IsProducao);
                                sucesso = result.Sucesso; mensagem = result.Mensagem; urlDanfe = result.UrlDanfe;
                            }

                            if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
    {
        // Uhuul! Aprovou silenciosamente! 
        await saleService.AtualizarDadosNfceAsync(nota.VendaId, urlDanfe, "Autorizada", ambienteSefaz, nota.Referencia);
        await contingencyService.RemoverNotaPendenteAsync(nota.Id);
    }
    // 🟢 NOVA CONDIÇÃO AQUI: Se for erro de validação (422) OU não for erro de comunicação, é Rejeição!
    else if (mensagem.Contains("UnprocessableEntity") || mensagem.Contains("erro_validacao_schema") || !mensagem.Contains("Erro de Comunicação"))
    {
        // Sefaz REJEITOU por erro de validação (ex: NCM errado). 
        await saleService.AtualizarDadosNfceAsync(nota.VendaId, "", "Rejeitada: " + mensagem, ambienteSefaz, nota.Referencia);
        await contingencyService.RemoverNotaPendenteAsync(nota.Id);
    }
    else
    {
        // A internet caiu mesmo. Conta +1 tentativa e deixa na fila.
        await contingencyService.RegistrarFalhaTentativaAsync(nota.Id, mensagem);
    }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("UnprocessableEntity") || ex.Message.Contains("erro_validacao_schema"))
                            {
                                await saleService.AtualizarDadosNfceAsync(nota.VendaId, "", "Rejeitada: " + ex.Message, ambienteSefaz, nota.Referencia);
                                
                                // 👇 A MUDANÇA É AQUI! Em vez de apagar a nota da fila, o robô VAI SALVAR O ERRO no banco!
                                await contingencyService.RegistrarFalhaTentativaAsync(nota.Id, $"ERRO SEFAZ OFFLINE: {ex.Message}");
                                
                                // Comentamos a linha que deletava a prova do crime
                                // await contingencyService.RemoverNotaPendenteAsync(nota.Id); 
                            }
                            else
                            {
                                await contingencyService.RegistrarFalhaTentativaAsync(nota.Id, ex.Message);
                            }
                        }
                }
            }}
            catch { }

            await Task.Delay(TimeSpan.FromMinutes(2));
        }
    }
}