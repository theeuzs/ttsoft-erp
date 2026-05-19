# ✅ Checklist de Deploy — TTSoft ERP v1.1.0

## 1. Banco Azure SQL — Rodar UMA VEZ
- [ ] Abrir SSMS conectado ao Azure SQL (erp-ttsoft-server.database.windows.net)
- [ ] Executar `AzureSetup_MultiTenancy.sql` inteiro
- [ ] Calcular TenantId da Vila Verde rodando `CalcularTenantId.csx` no LinqPad
- [ ] Executar o bloco de UPDATE comentado no final do script, com o Guid gerado

## 2. licenca.json — Em CADA máquina instalada
- [ ] Abrir `licenca.json` na pasta do ERP
- [ ] Substituir `"00000000000000"` pelo CNPJ real da loja (só números)
  - Vila Verde: `"12820608000141"`

## 3. Azure Blob — Acesso Público
- [ ] Portal Azure → Storage Account ttsoftupdates → Containers → releases
- [ ] Clicar em "Alterar nível de acesso" → selecionar "Blob (acesso de leitura anônimo)"
- [ ] Isso permite que o ERP baixe o versao.json e o .exe SEM autenticação

## 4. Publicar nova versão (v1.1.0)
- [ ] Build em Release:
      dotnet publish ERP.WPF -c Release -r win-x64 --self-contained true \
        -p:PublishSingleFile=true -o C:\Publish\ERP
- [ ] Upload do `ERP.WPF.exe` gerado para o Blob (substituir o anterior)
- [ ] Upload do `Scripts/versao.json` para o Blob (substituir o anterior)
- [ ] Testar abrindo o ERP numa máquina com versão anterior

## 5. Verificação final
- [ ] ERP abre e exibe tela de update (se versão local < 1.1.0)
- [ ] Clicar "Atualizar Agora" → barra de progresso aparece → ERP reinicia
- [ ] Após reinício, logar e verificar que dados aparecem normalmente
- [ ] Criar um produto/cliente novo e confirmar que TenantId é preenchido no banco

## Versões
| Componente        | Versão  |
|-------------------|---------|
| AssemblyVersion   | 1.1.0.0 |
| versao.json       | 1.1.0   |
| ERP.WPF.csproj    | 1.1.0   |
