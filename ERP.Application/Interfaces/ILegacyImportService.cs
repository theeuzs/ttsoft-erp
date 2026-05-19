using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface ILegacyImportService
{
    // O método que vai fazer a mágica acontecer!
    Task<string> ImportFromFolderAsync(string folderPath);
}