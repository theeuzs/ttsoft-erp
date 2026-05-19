using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class LegacyImportService : ILegacyImportService
{
    private readonly IUnitOfWork _uow;

    public LegacyImportService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<string> ImportFromFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return "A pasta informada não existe! Verifique o caminho.";

        var files = Directory.GetFiles(folderPath, "vendas*", SearchOption.AllDirectories);
        if (files.Length == 0)
            return "Nenhum arquivo de vendas encontrado na pasta!";

        var allProducts = await _uow.Products.GetAllAsync();
        var fallbackProduct = allProducts.FirstOrDefault();
        
        if (fallbackProduct == null)
            return "Erro: Você precisa ter pelo menos 1 produto cadastrado no sistema!";

        Guid fallbackProductId = fallbackProduct.Id;

        // 👇 Criamos a regra do Brasil uma vez só para usar em todo o arquivo!
        var culturaBR = new CultureInfo("pt-BR");

        int cuponsImportados = 0;
        int erros = 0;

        foreach (var file in files)
        {
            var lines = await File.ReadAllLinesAsync(file);
            Sale? currentSale = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.StartsWith("Data/Hora:"))
                {
                    if (currentSale != null)
                    {
                        await FinalizarESalvarVenda(currentSale);
                    }

                    var dateStr = line.Replace("Data/Hora:", "").Trim();
                    
                    // 👇 Se falhar, joga para o ano 2000 para não estragar o caixa do dia!
                    DateTime saleDate = new DateTime(2000, 1, 1); 
                    
                    if (DateTime.TryParse(dateStr, culturaBR, DateTimeStyles.None, out var parsedDate))
                    {
                        saleDate = parsedDate;
                    }

                    currentSale = new Sale
                    {
                        SaleDate = saleDate,
                        CreatedAt = saleDate,
                        Status = SaleStatus.SemNota,
                        SellerName = "Sistema Antigo",
                        Notes = "",
                        Items = new List<SaleItem>(),
                        Payments = new List<SalePayment>()
                    };
                    continue;
                }

                if (currentSale == null) continue;

                if (line.Contains("x") && line.Contains("=") && line.Any(char.IsDigit))
                {
                    var parts = line.Split(new[] { 'x', '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 3 && i > 0)
                    {
                        var qtdStr = new string(parts[0].Where(c => char.IsDigit(c) || c == ',').ToArray());
                        var valStr = new string(parts[1].Where(c => char.IsDigit(c) || c == ',').ToArray());

                        // 👇 Dinheiro e Quantidade blindados com a cultura do Brasil!
                        if (decimal.TryParse(qtdStr, NumberStyles.Any, culturaBR, out var qtd) && 
                            decimal.TryParse(valStr, NumberStyles.Any, culturaBR, out var valorUnitario))
                        {
                            string productName = lines[i - 1].Trim();
                            if (productName.StartsWith("-")) 
                                productName = productName.Substring(1).Trim(); 

                            currentSale.Items.Add(new SaleItem
                            {
                                ProductId = fallbackProductId,
                                ProductName = productName,
                                Quantity = qtd,
                                UnitPrice = valorUnitario
                            });
                        }
                    }
                }

                if (line.StartsWith("TOTAL:"))
                {
                    var totalStr = new string(line.Replace("TOTAL:", "").Where(c => char.IsDigit(c) || c == ',').ToArray());
                    if (decimal.TryParse(totalStr, NumberStyles.Any, culturaBR, out var totalNota))
                    {
                        currentSale.Total = totalNota;
                        currentSale.Subtotal = totalNota;
                    }
                }

                if (line.StartsWith("Cliente:"))
                {
                    var nomeCliente = line.Replace("Cliente:", "").Trim();
                    currentSale.Notes = "Cliente Antigo: " + nomeCliente;
                }

                if (line.StartsWith("Caixa:"))
                {
                    var caixaStr = line.Replace("Caixa:", "").Trim();
                    
                    if (caixaStr.Contains("- Cupom:"))
                    {
                        var parts = caixaStr.Split(new string[] { "- Cupom:" }, StringSplitOptions.None);
                        currentSale.SellerName = parts[0].Trim();
                        currentSale.SaleNumber = parts[1].Trim();
                    }
                    else
                    {
                        currentSale.SellerName = caixaStr;
                    }
                }

                if (line.StartsWith("Cartao:") || line.StartsWith("Dinheiro:") || line.StartsWith("Pix:"))
                {
                    var tipoStr = line.Split(':')[0].Trim().ToLower();
                    var valorStr = new string(line.Split(':')[1].Where(c => char.IsDigit(c) || c == ',').ToArray());
                    
                    if (decimal.TryParse(valorStr, NumberStyles.Any, culturaBR, out var valorPago) && valorPago > 0)
                    {
                        var paymentMethod = PaymentMethod.Dinheiro;
                        if (tipoStr.Contains("cartao")) paymentMethod = PaymentMethod.CartaoDebito;
                        if (tipoStr.Contains("pix")) paymentMethod = PaymentMethod.Pix;

                        currentSale.Payments.Add(new SalePayment
                        {
                            PaymentMethod = paymentMethod,
                            Amount = valorPago,
                            CreatedAt = currentSale.SaleDate
                        });
                    }
                }
            }

            if (currentSale != null)
            {
                await FinalizarESalvarVenda(currentSale);
            }
        }

        await _uow.CommitAsync();

        return $"Importação concluída! Sucesso: {cuponsImportados} vendas. Erros: {erros}.";

        async Task FinalizarESalvarVenda(Sale sale)
        {
            if (sale.Payments.Count == 0 && sale.Total > 0)
            {
                sale.Payments.Add(new SalePayment { PaymentMethod = PaymentMethod.Dinheiro, Amount = sale.Total, CreatedAt = sale.SaleDate });
            }

            if (string.IsNullOrEmpty(sale.SaleNumber))
            {
                sale.SaleNumber = "S/N";
            }

            try
            {
                await _uow.Sales.AddAsync(sale);
                cuponsImportados++;

                if (cuponsImportados % 500 == 0)
                {
                    await _uow.CommitAsync();
                }
            }
            catch
            {
                erros++;
            }
        }
    }
}