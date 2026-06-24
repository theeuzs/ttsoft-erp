using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services
{
    public class NfeImportService : INfeImportService
    {
        private readonly IUnitOfWork _uow;

        public NfeImportService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<NfeImportDto> LerXmlNfeAsync(string caminhoArquivo)
        {
            // S9 FIX: validações de segurança antes de parsear o XML.
            // Antes: XDocument.Load(caminho) sem DTD prohibition, sem limite de tamanho,
            //        sem XSD — atacante com permissão de importar NF-e podia subir XML forjado.

            // 1. Limite de tamanho de arquivo (5 MB) — antes de abrir o XmlReader
            var fileInfo = new FileInfo(caminhoArquivo);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("Arquivo NF-e não encontrado.", caminhoArquivo);
            if (fileInfo.Length > 5_242_880) // 5 MB
                throw new InvalidOperationException(
                    $"Arquivo NF-e muito grande ({fileInfo.Length / 1024 / 1024} MB). Máximo: 5 MB.");

            var dto = await Task.Run(() =>
            {
                // 2. XmlReaderSettings defensivo
                var settings = new XmlReaderSettings
                {
                    DtdProcessing             = DtdProcessing.Prohibit, // XXE: proíbe DTD externo/interno
                    XmlResolver               = null,                    // XXE: bloqueia resolução de entidades externas
                    MaxCharactersFromEntities = 1_000_000,               // bomb: limita expansão de entidades
                    MaxCharactersInDocument   = 5_000_000,               // bomb: ~5MB descomprimido
                    ValidationType            = ValidationType.None,     // default: sem XSD ainda
                };

                // 3. XSD opcional — valida estrutura se o schema estiver presente.
                //    Colocar procNFe_v4.00.xsd em ERP.Api/Schemas/ e marcar como "Copy if newer".
                //    Download: https://www.nfe.fazenda.gov.br/portal/ → Schemas Atuais XML
                var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "procNFe_v4.00.xsd");
                if (File.Exists(schemaPath))
                {
                    settings.ValidationType = ValidationType.Schema;
                    settings.Schemas.Add(null, schemaPath);
                    settings.ValidationEventHandler += (_, e) =>
                    {
                        if (e.Severity == XmlSeverityType.Error)
                            throw new InvalidOperationException(
                                $"XML não conforme NF-e v4.00: {e.Message}");
                    };
                }

                XDocument doc;
                using (var reader = XmlReader.Create(caminhoArquivo, settings))
                    doc = XDocument.Load(reader);

                var infNFe = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "infNFe")
                    ?? throw new Exception("Arquivo inválido. Isso não parece ser um XML de NF-e.");

                // 4. Limite de itens — impede NF-e sintética com 100k <det> consumindo toda a RAM
                var detElements = infNFe.Elements().Where(x => x.Name.LocalName == "det").ToList();
                if (detElements.Count > 1000)
                    throw new InvalidOperationException(
                        $"NF-e com {detElements.Count} itens excede o limite de 1000.");

                var ide = infNFe.Elements().FirstOrDefault(x => x.Name.LocalName == "ide");
                var emit = infNFe.Elements().FirstOrDefault(x => x.Name.LocalName == "emit");
                var total = infNFe.Elements().FirstOrDefault(x => x.Name.LocalName == "total");
                
                var vNF = total?.Descendants().FirstOrDefault(x => x.Name.LocalName == "vNF")?.Value;

                var nfeDto = new NfeImportDto
                {
                    NumeroNota = ide?.Elements().FirstOrDefault(x => x.Name.LocalName == "nNF")?.Value ?? "",
                    ChaveAcesso = infNFe.Attribute("Id")?.Value.Replace("NFe", "") ?? "",
                    FornecedorNome = emit?.Elements().FirstOrDefault(x => x.Name.LocalName == "xNome")?.Value ?? "",
                    FornecedorCnpj = emit?.Elements().FirstOrDefault(x => x.Name.LocalName == "CNPJ")?.Value ?? "",
                    
                    DataEmissao = DateTime.TryParse(ide?.Elements().FirstOrDefault(x => x.Name.LocalName == "dhEmi")?.Value, out DateTime data) 
                        ? data : DateTime.Now,
                        
                    ValorTotal = decimal.TryParse(vNF, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v) ? v : 0,
                    Itens = new List<NfeItemImportDto>(),
                    Duplicatas = new List<NfeDuplicataDto>()
                };

                // 🔥 LENDO AS PARCELAS DO BOLETO (<cobr> e <dup>) 🔥
                var cobr = infNFe.Elements().FirstOrDefault(x => x.Name.LocalName == "cobr");
                if (cobr != null)
                {
                    var duplicatasXml = cobr.Descendants().Where(x => x.Name.LocalName == "dup");
                    foreach (var dup in duplicatasXml)
                    {
                        var nDup = dup.Elements().FirstOrDefault(x => x.Name.LocalName == "nDup")?.Value ?? "";
                        var dVenc = dup.Elements().FirstOrDefault(x => x.Name.LocalName == "dVenc")?.Value;
                        var vDup = dup.Elements().FirstOrDefault(x => x.Name.LocalName == "vDup")?.Value;

                        nfeDto.Duplicatas.Add(new NfeDuplicataDto
                        {
                            Numero = nDup,
                            DataVencimento = DateTime.TryParse(dVenc, out DateTime venc) ? venc : DateTime.Now,
                            Valor = decimal.TryParse(vDup, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vlrDup) ? vlrDup : 0
                        });
                    }
                }

                foreach (var det in detElements)
                {
                    var prod = det.Elements().FirstOrDefault(x => x.Name.LocalName == "prod");
                    if (prod != null)
                    {
                        string qComStr = prod.Elements().FirstOrDefault(x => x.Name.LocalName == "qCom")?.Value ?? "0";
                        string vUnComStr = prod.Elements().FirstOrDefault(x => x.Name.LocalName == "vUnCom")?.Value ?? "0";

                        nfeDto.Itens.Add(new NfeItemImportDto
                        {
                            CodigoBarrasFornecedor = prod.Elements().FirstOrDefault(x => x.Name.LocalName == "cEAN")?.Value ?? "",
                            NomeProdutoFornecedor = prod.Elements().FirstOrDefault(x => x.Name.LocalName == "xProd")?.Value ?? "",
                            UnidadeMedida = prod.Elements().FirstOrDefault(x => x.Name.LocalName == "uCom")?.Value ?? "",
                            QuantidadeComprada = decimal.TryParse(qComStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal q) ? q : 0,
                            ValorCustoUnitario = decimal.TryParse(vUnComStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal vu) ? vu : 0
                        });
                    }
                }

                return nfeDto;
            });

            var produtosVilaVerde = await _uow.Products.GetAllAsync();

            foreach (var itemXml in dto.Itens)
            {
                if (string.IsNullOrWhiteSpace(itemXml.CodigoBarrasFornecedor) || itemXml.CodigoBarrasFornecedor == "SEM GTIN")
                    continue;

                var produtoEncontrado = produtosVilaVerde.FirstOrDefault(p => p.Barcode == itemXml.CodigoBarrasFornecedor);

                if (produtoEncontrado != null)
                {
                    itemXml.ProdutoJaCadastrado = true;
                    itemXml.ProdutoIdNoNossoSistema = produtoEncontrado.Id;
                }
                else
                {
                    itemXml.ProdutoJaCadastrado = false;
                    itemXml.ProdutoIdNoNossoSistema = null;
                }
            }

            return dto;
        }

        public async Task<(int Novos, int Atualizados, Guid PedidoCompraId)> SalvarEAtualizarEstoqueAsync(NfeImportDto notaFiscal)
        {
            int novosCadastrados = 0;
            int produtosAtualizados = 0;

            // 1. Cria/localiza o fornecedor
            var fornecedores = await _uow.Suppliers.GetAllAsync();
            var fornecedor = fornecedores.FirstOrDefault(f => f.Name == notaFiscal.FornecedorNome);

            if (fornecedor == null)
            {
                fornecedor = new ERP.Domain.Entities.Supplier { Name = notaFiscal.FornecedorNome };
                await _uow.Suppliers.AddAsync(fornecedor);
            }

            // 2. Cria o PedidoCompra vinculado a esta NF-e
            var numero = $"NF-{notaFiscal.NumeroNota}-{DateTime.Now:yyyyMMddHHmm}";
            var pedido = new ERP.Domain.Entities.PedidoCompra
            {
                Numero         = numero,
                SupplierId     = fornecedor.Id,
                FornecedorNome = notaFiscal.FornecedorNome,
                DataPedido     = notaFiscal.DataEmissao,
                DataRecebimento = DateTime.Now,
                Status         = ERP.Domain.Enums.StatusPedidoCompra.Recebido,
                Observacoes    = $"Importado automaticamente do XML NF-e {notaFiscal.NumeroNota} " +
                                 $"(Chave: {notaFiscal.ChaveAcesso})",
                Itens          = new List<ERP.Domain.Entities.PedidoCompraItem>()
            };

            // 3. Atualiza estoque e adiciona itens ao pedido
            foreach (var item in notaFiscal.Itens)
            {
                if (item.ProdutoJaCadastrado && item.ProdutoIdNoNossoSistema.HasValue)
                {
                    var produtoExistente = await _uow.Products.GetByIdAsync(item.ProdutoIdNoNossoSistema.Value);
                    if (produtoExistente != null)
                    {
                        produtoExistente.Stock       += item.QuantidadeComprada;
                        produtoExistente.OriginalCost = item.ValorCustoUnitario;
                        produtoExistente.CostPrice    = item.ValorCustoUnitario;
                        _uow.Products.Update(produtoExistente);
                        produtosAtualizados++;

                        pedido.Itens.Add(new ERP.Domain.Entities.PedidoCompraItem
                        {
                            ProductId      = produtoExistente.Id,
                            ProductName    = produtoExistente.Name,
                            Quantidade     = item.QuantidadeComprada,
                            PrecoUnitario  = item.ValorCustoUnitario
                        });
                    }
                }
                else if (!item.ProdutoJaCadastrado)
                {
                    var novoProduto = new ERP.Domain.Entities.Product
                    {
                        Name                = item.NomeProdutoFornecedor,
                        Barcode             = item.CodigoBarrasFornecedor,
                        Unit                = item.UnidadeMedida,
                        IsActive            = true,
                        SupplierId          = fornecedor.Id,
                        OriginalCost        = item.ValorCustoUnitario,
                        CostPrice           = item.ValorCustoUnitario,
                        SalePrice           = item.ValorCustoUnitario * 1.50m,
                        DesiredMarginPercent = 50,
                        Stock               = item.QuantidadeComprada
                    };
                    await _uow.Products.AddAsync(novoProduto);
                    novosCadastrados++;

                    pedido.Itens.Add(new ERP.Domain.Entities.PedidoCompraItem
                    {
                        ProductId     = novoProduto.Id,
                        ProductName   = novoProduto.Name,
                        Quantidade    = item.QuantidadeComprada,
                        PrecoUnitario = item.ValorCustoUnitario
                    });
                }
            }

            await _uow.PedidosCompra.AddAsync(pedido);

            // 4. Integração financeira — duplicatas ou parcela única
            if (notaFiscal.Duplicatas.Any())
            {
                foreach (var dup in notaFiscal.Duplicatas)
                {
                    await _uow.ContasPagar.AddAsync(new ERP.Domain.Entities.ContaPagar
                    {
                        Descricao      = $"NF {notaFiscal.NumeroNota} - Parc {dup.Numero} - {notaFiscal.FornecedorNome}",
                        Categoria      = "Fornecedores",
                        Valor          = dup.Valor,
                        DataVencimento = dup.DataVencimento,
                        DataEmissao    = notaFiscal.DataEmissao
                    });
                }
            }
            else
            {
                await _uow.ContasPagar.AddAsync(new ERP.Domain.Entities.ContaPagar
                {
                    Descricao      = $"NF {notaFiscal.NumeroNota} - {notaFiscal.FornecedorNome}",
                    Categoria      = "Fornecedores",
                    Valor          = notaFiscal.ValorTotal,
                    DataVencimento = DateTime.Now.AddDays(30),
                    DataEmissao    = notaFiscal.DataEmissao
                });
            }

            await _uow.CommitAsync();

            return (novosCadastrados, produtosAtualizados, pedido.Id);
        }
    } 
}