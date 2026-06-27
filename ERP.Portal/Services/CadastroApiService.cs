using ERP.Portal.Models;
using System.Net.Http.Json;

namespace ERP.Portal.Services;

public class CadastroApiService
{
    private readonly HttpClient _http;

    public CadastroApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Sucesso, string Mensagem, string? LoginUrl)> CadastrarAsync(CadastroRequestDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/cadastro", dto);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CadastroResponseDto>();
                return (true, result?.MensagemSucesso ?? "Cadastro realizado!", result?.LoginUrl);
            }

            var erro = await response.Content.ReadAsStringAsync();
            return (false, erro.Trim('"'), null);
        }
        catch (Exception ex)
        {
            return (false, $"Erro de conexão: {ex.Message}", null);
        }
    }
}
