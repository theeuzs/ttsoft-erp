namespace ERP.Application.DTOs;

public class DreDto
{
    public decimal ReceitaBruta { get; set; } // Faturamento
    public decimal CustoMercadorias { get; set; } // Custo dos materiais (CMV)
    
    // O C# calcula o lucro e a margem sozinho!
    public decimal LucroBruto => ReceitaBruta - CustoMercadorias;
    public decimal MargemLucro => ReceitaBruta > 0 ? (LucroBruto / ReceitaBruta) * 100 : 0;
}