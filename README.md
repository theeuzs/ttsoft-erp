# ERP Materiais de Construção — .NET 8 + WPF

## Arquitetura

```
ERP.Domain          → Entidades, Enums, Interfaces (zero dependências externas)
ERP.Application     → DTOs, Services, Validators, AutoMapper (lógica de negócio pura)
ERP.Infrastructure  → Repositórios concretos, UnitOfWork
ERP.Persistence     → DbContext, Configurações EF Core, Migrations
ERP.WPF             → UI WPF, ViewModels, Views, Commands (MVVM)
```

## Setup Rápido

### 1. Pré-requisitos
- .NET 8 SDK
- SQL Server (local ou Express)
- Visual Studio 2022+

### 2. Configurar Connection String
Editar em `ERP.WPF/App.xaml.cs`:
```csharp
options.UseSqlServer("Server=localhost;Database=ERPMateriais;Trusted_Connection=True;TrustServerCertificate=True;");
```

### 3. Criar Migrations
```bash
# Na raiz da solução:
dotnet ef migrations add InitialCreate --project ERP.Persistence --startup-project ERP.WPF
dotnet ef database update --project ERP.Persistence --startup-project ERP.WPF
```

### 4. Rodar
```bash
dotnet run --project ERP.WPF
```

## Estrutura de Arquivos

```
ERP/
├── ERP.sln
├── ERP.Domain/
│   ├── Common/BaseEntity.cs
│   ├── Entities/Product.cs, Customer.cs, Sale.cs
│   ├── Enums/Enums.cs
│   └── Interfaces/IRepository.cs, ISpecificRepositories.cs
├── ERP.Application/
│   ├── DTOs/Dtos.cs
│   ├── Interfaces/IServices.cs
│   ├── Mappings/MappingProfile.cs
│   ├── Services/ProductService.cs, SaleService.cs, CustomerService.cs, DashboardService.cs
│   └── Validators/Validators.cs
├── ERP.Infrastructure/
│   ├── Repositories/Repository.cs, SpecificRepositories.cs
│   └── UnitOfWork/UnitOfWork.cs
├── ERP.Persistence/
│   ├── Context/AppDbContext.cs
│   └── Configurations/EntityConfigurations.cs
└── ERP.WPF/
    ├── App.xaml.cs         ← DI Registration aqui
    ├── MainWindow.xaml     ← Sidebar + navegação
    ├── Commands/RelayCommand.cs
    ├── ViewModels/BaseViewModel.cs, ProductViewModel.cs, PdvViewModel.cs, DashboardViewModel.cs
    └── Views/ProductView.xaml, PdvView.xaml
```

## Padrões Utilizados

| Padrão | Onde |
|--------|------|
| Repository | `ERP.Infrastructure/Repositories` |
| Unit of Work | `ERP.Infrastructure/UnitOfWork` |
| MVVM | `ERP.WPF/ViewModels` + `Views` |
| SOLID | Separação de responsabilidades em camadas |
| DTOs | `ERP.Application/DTOs` |
| AutoMapper | `ERP.Application/Mappings` |
| FluentValidation | `ERP.Application/Validators` |
| Clean Architecture | Dependências apontam sempre para o Domain |
| DI (IoC) | `App.xaml.cs` via `Microsoft.Extensions.DependencyInjection` |

## Próximos Passos

- [ ] Telas de Cliente e Histórico de Vendas
- [ ] Dashboard com gráficos (LiveCharts2)
- [ ] Impressão de cupom (QuestPDF)
- [ ] Módulo NFC-e (DFe.NET ou ACBr.NET)
- [ ] Relatórios PDF
- [ ] Login / controle de usuários
 
