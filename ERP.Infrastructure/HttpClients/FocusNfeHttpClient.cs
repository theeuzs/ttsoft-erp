using ERP.Application.Interfaces; 
using FluentResults;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Infrastructure.HttpClients;

public class FocusNfeHttpClient : IFocusNfeHttpClient
{
    private readonly HttpClient _httpClient; 

    public FocusNfeHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void SetApiToken(string token)
    {
        var authBytes = Encoding.ASCII.GetBytes($"{token}:");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    }

    public async Task<Result<string>> GetAsync(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            if (response.IsSuccessStatusCode) return Result.Ok(await response.Content.ReadAsStringAsync());
            return Result.Fail($"Erro na requisição: {response.StatusCode}");
        }
        catch (Exception ex) { return Result.Fail($"Falha de comunicação: {ex.Message}"); }
    }

    public async Task<Result<string>> PostAsync(string endpoint, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            if (response.IsSuccessStatusCode) return Result.Ok(await response.Content.ReadAsStringAsync());
            return Result.Fail($"Erro: {response.StatusCode}. {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) { return Result.Fail($"Falha de comunicação: {ex.Message}"); }
    }

    public async Task<Result<string>> DeleteAsync(string endpoint)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            if (response.IsSuccessStatusCode) return Result.Ok("Excluído com sucesso.");
            return Result.Fail($"Erro ao excluir: {response.StatusCode}");
        }
        catch (Exception ex) { return Result.Fail($"Falha ao excluir: {ex.Message}"); }
    }

    // 🟢 NOVO MÉTODO: Envia requisição DELETE com corpo (Body) em JSON
    public async Task<Result<string>> DeleteWithBodyAsync(string endpoint, object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            
            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode) return Result.Ok(await response.Content.ReadAsStringAsync());
            return Result.Fail($"Erro: {response.StatusCode}. {await response.Content.ReadAsStringAsync()}");
        }
        catch (Exception ex) { return Result.Fail($"Falha de comunicação: {ex.Message}"); }
    }
}