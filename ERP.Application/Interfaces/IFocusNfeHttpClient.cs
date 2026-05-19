using System.Threading.Tasks;
using FluentResults;

namespace ERP.Application.Interfaces; // 🟢 O Namespace correto agora!

public interface IFocusNfeHttpClient
{
    void SetApiToken(string token); // 🟢 O Fim da gambiarra do Token!
    Task<Result<string>> GetAsync(string endpoint);
    Task<Result<string>> PostAsync(string endpoint, object data);
    Task<Result<string>> DeleteAsync(string endpoint);
    Task<Result<string>> DeleteWithBodyAsync(string endpoint, object data);
}