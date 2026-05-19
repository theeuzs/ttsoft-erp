using System.Collections.Generic;
using System.Threading.Tasks;
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces
{
    public interface INfeImportService
    {
        Task<NfeImportDto> LerXmlNfeAsync(string caminhoArquivo);
        
        // 👇 A NOVA FUNÇÃO QUE DEVOLVE DOIS NÚMEROS
        Task<(int Novos, int Atualizados, Guid PedidoCompraId)> SalvarEAtualizarEstoqueAsync(NfeImportDto notaFiscal);
    }
}