# Checklist de Regressão — Integração Mercado Livre

Roteiro manual pra validar que a integração continua funcionando depois de
qualquer mudança no módulo (`OrderProcessingService`, `MercadoLivreDispatcher`,
`MercadoLivreAuthService`, `MarketplaceController`, `OrderSyncRepository`).

Feito pra rodar em ~10-15 min sem precisar reconstruir a investigação do zero.
Todos os exemplos usam o tenant de teste (CNPJ `11222333000181`) e o canal do
vendedor de teste (`SalesChannelId = 003DF04F-4D03-4059-BC21-17E92D9CADFB`,
`user_id = 3554576101`) no banco `ERPTTSoftStaging`.

---

## 0. Pré-requisito: confirmar o banco certo

Antes de qualquer teste, confirma que a API está lendo o banco esperado —
essa integração já teve um histórico real de oscilar entre bancos.

```powershell
$loginBody = @{ Username = "admin"; Password = "SENHA_AQUI" } | ConvertTo-Json
$loginResp = Invoke-RestMethod `
    -Uri "https://erp-ttsoft-api-g8bde4f6aqcwb9aw.brazilsouth-01.azurewebsites.net/api/auth/login" `
    -Method Post -Headers @{ "X-Tenant-CNPJ" = "11222333000181" } `
    -ContentType "application/json" -Body $loginBody
$token = $loginResp.accessToken

Invoke-RestMethod `
    -Uri "https://erp-ttsoft-api-g8bde4f6aqcwb9aw.brazilsouth-01.azurewebsites.net/api/marketplace/status" `
    -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json -Depth 5
```

☐ `bancoConectadoAgora` = `ERPTTSoftStaging`
☐ `mercadoLivre.lojasConectadas` ≥ 2

---

## 1. Publicar um anúncio de teste novo

Sempre criar um anúncio **novo** — na prática, o anúncio finaliza sozinho
depois de uma compra (comportamento do próprio ambiente de teste do ML, não
bug nosso).

```powershell
$obj = @{
    family_name         = "Roda Forro Perfil U Neve 6m"
    category_id         = "MLB418817"
    price               = 34.50
    currency_id         = "BRL"
    available_quantity  = 40
    buying_mode         = "buy_it_now"
    listing_type_id     = "free"
    condition           = "new"
    seller_custom_field = "RODA-FORRO-U-NEVE-6M"
    attributes = @(
        @{ id = "BRAND"; value_name = "Generica" },
        @{ id = "MODEL"; value_name = "Perfil U" },
        @{ id = "SALES_UNIT"; value_id = "1359391" },
        @{ id = "MOULDINGS_YIELD_OF_SALES_UNIT"; value_name = "6 m" }
    )
    pictures = @( @{ source = "https://picsum.photos/600/600.jpg" } )
}
$bodyBytes = [System.Text.Encoding]::UTF8.GetBytes(($obj | ConvertTo-Json -Depth 10))

$novoItem = Invoke-RestMethod -Method POST -Uri "https://api.mercadolibre.com/items" `
    -Headers @{ Authorization = "Bearer TOKEN_DO_VENDEDOR_TESTE" } `
    -ContentType "application/json; charset=utf-8" -Body $bodyBytes
$novoItem | Select-Object id, status, permalink | Format-List
```

☐ `seller_custom_field` já mapeado no `SkuMapping` (senão o pedido para em
  `ConflitoAberto` por SKU não mapeado — não é bug, é o comportamento certo)
☐ Confirmar `status: active` antes de comprar (se ficar preso em
  `picture_download_pending`, sobe a foto manualmente logado como vendedor de
  teste no próprio site do ML)

---

## 2. Comprar como o comprador de teste

Aba anônima → login `TESTUSER6295580150925238846` → comprar o item.
CEP `01001-000`, cartão `5067 7667 8388 8311`, titular **`APRO`** (aprova o
pagamento), validade futura, CVV `123`, CPF matematicamente válido (gerar em
`4devs.com.br/gerador_de_cpf` se pedir).

☐ Compra concluída, `purchaseId`/`order_id` anotado

---

## 3. Confirmar o webhook (caminho normal)

Olha o Fluxo de Registo do `erp-ttsoft-api` — deve aparecer
`HTTP POST /api/marketplace/ml/webhook responded 200` em segundos.

```sql
SELECT Id, ExternalOrderId, InternalStatus, VendaId, UpdatedAt
FROM ExternalOrders
ORDER BY UpdatedAt DESC;
```

☐ `InternalStatus = 4` (`VendaGerada`)
☐ `VendaId` preenchido (GUID, não NULL)

```sql
SELECT DataHora, Tipo, Descricao FROM OrderEvents
WHERE ExternalOrderId = 'COLE_O_ID_AQUI' ORDER BY DataHora;
```

☐ Exatamente **1** linha com `Tipo = 4` (`VendaGerada`) — mais de uma indica
  venda duplicada (regressão do bug de concorrência já corrigido)

```sql
SELECT * FROM ContasReceber WHERE VendaId = 'VENDAID_DE_CIMA';
```

☐ Exatamente **1** Conta a Receber pra essa venda

```sql
SELECT Stock FROM Products WHERE Id = 'ID_DO_PRODUTO';
```

☐ Estoque baixou a quantidade correta

---

## 4. Confirmar que retries do ML não duplicam

O Mercado Livre reenvia o mesmo webhook várias vezes em segundos — isso é
esperado, não é erro. Espera ~1-2 min e olha o log de novo.

☐ Reenvios aparecem com `responded 200` rápido (poucos ms — sinal de que
  bateu na guarda `InternalStatus == VendaGerada` e saiu cedo)
☐ Rodar a query do passo 3 de novo — continua **exatamente 1** evento Tipo=4

---

## 5. Confirmar o polling (caminho alternativo, sem depender de webhook)

```powershell
Invoke-RestMethod `
    -Uri "https://erp-ttsoft-api-g8bde4f6aqcwb9aw.brazilsouth-01.azurewebsites.net/api/marketplace/ml/sincronizar/003DF04F-4D03-4059-BC21-17E92D9CADFB?desde=2026-01-01" `
    -Method Post -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json -Depth 5
```

☐ `status: Concluido`, `totalErros: 0`
☐ `totalPedidosProcessados: 0` é o esperado se o pedido já foi processado via
  webhook (idempotência correta) — só espera `1` se for um pedido genuinamente
  novo que o webhook nunca chegou a processar

---

## 6. Forçar e confirmar renovação de token

```sql
UPDATE SalesChannels SET TokenExpiraEm = GETUTCDATE()
WHERE Id = '003DF04F-4D03-4059-BC21-17E92D9CADFB';

-- Guarda o valor atual pra comparar depois
SELECT AccessToken, RefreshToken, TokenExpiraEm FROM SalesChannels
WHERE Id = '003DF04F-4D03-4059-BC21-17E92D9CADFB';
```

Dispara qualquer chamada que use o dispatcher (o `sincronizar` do passo 5
serve) e roda a mesma consulta de novo.

☐ `AccessToken` mudou (valor diferente do anotado antes)
☐ `RefreshToken` mudou
☐ `TokenExpiraEm` está ~6h à frente do momento do teste, não mais no passado

---

## Histórico de bugs que este checklist já pegou

Pra referência — cada um desses já aconteceu de verdade nesta integração:

- Filtro de tenant (`HasQueryFilter`) não aplicado no callback OAuth anônimo
- `QueryTrackingBehavior.NoTracking` global fazendo `SaveChanges` virar no-op
  silencioso em vários pontos (token OAuth, `ExternalOrder`, reservas)
- Ambiguidade de banco (`ERPTTSoft` vs `ERPTTSoftStaging`) — resolvida ao
  apagar a entrada conflitante em "Cadeias de ligação" no Azure
- Validação HMAC do webhook baseada no esquema errado (Mercado Pago, não
  Mercado Livre) — ML não assina esse tipo de notificação
- Nome de tópico errado (`orders` em vez de `orders_v2`) e tipos de campo
  errados no `MLWebhookDto` (`ApplicationId`/`UserId` são número, não string;
  `Sent`/`Received` são string ISO, não número)
- Idempotência tratando "pedido já existe" como "concluído", sem distinguir
  de "preso em conflito não resolvido"
- `Customer.Document` estourando `nvarchar(18)`
- Corrida entre webhooks quase simultâneos do mesmo pedido (duplo `INSERT`,
  cliente de repasse duplicado, e depois vendas duplicadas mesmo com lock —
  causa raiz final: `ChangeTracker.Clear()` do projeto limpando o
  rastreamento do EF Core no meio do método sempre que outro serviço salvava
  antes)
- Mesmo bug de `ChangeTracker.Clear()` na renovação de token, assimétrico
  entre webhook (funcionava) e polling (não persistia)
