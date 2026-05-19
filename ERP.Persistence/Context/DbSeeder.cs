using ERP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ERP.Persistence.Context;

public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        var tenantId = AppDbContext.GetGlobalTenantId();

        // ── 1. Roles ──────────────────────────────────────────────────────────
        if (!context.Roles.IgnoreQueryFilters().Any(r => r.TenantId == tenantId))
        {
            var adminRole = new Role
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Name = "Administrador",
                MaxDiscountPercentage = 100m, MaxSangriaValue = 999999m
            };
            var gerenteRole = new Role
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Name = "Gerente",
                MaxDiscountPercentage = 30m, MaxSangriaValue = 5000m
            };
            var supervisorRole = new Role
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Name = "Supervisor",
                MaxDiscountPercentage = 15m, MaxSangriaValue = 1000m
            };
            var vendedorRole = new Role
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                Name = "Vendedor",
                MaxDiscountPercentage = 5m, MaxSangriaValue = 0m
            };

            context.Roles.AddRange(adminRole, gerenteRole, supervisorRole, vendedorRole);
            context.SaveChanges();
        }
        else
        {
            // Garante que Supervisor existe mesmo em tenants antigos
            var roles = context.Roles.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId).ToList();

            if (!roles.Any(r => r.Name == "Supervisor"))
            {
                context.Roles.Add(new Role
                {
                    Id = Guid.NewGuid(), TenantId = tenantId,
                    Name = "Supervisor",
                    MaxDiscountPercentage = 15m, MaxSangriaValue = 1000m
                });
                context.SaveChanges();
            }
        }

        // ── 2. Permissões ─────────────────────────────────────────────────────
        if (!context.Permissions.IgnoreQueryFilters().Any(p => p.TenantId == tenantId))
        {
            // Todas as permissões do sistema
            var allPerms = new List<Permission>
            {
                // PDV / Vendas
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "sale.discount",      Description = "Conceder desconto em vendas" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "sale.cancel",        Description = "Cancelar vendas finalizadas" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "sale.return",        Description = "Devolução parcial de itens" },
                // Caixa
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "cash.sangria",       Description = "Realizar sangria e suprimento" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "cash.view.summary",  Description = "Ver resumo do caixa" },
                // Produtos / Estoque
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "product.edit",       Description = "Criar e editar produtos" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "product.edit.price", Description = "Alterar preço de produtos" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "stock.adjust",       Description = "Ajuste de estoque" },
                // Clientes / Haver
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "haver.edit",         Description = "Depositar e retirar Haver" },
                // Financeiro
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "report.financial",   Description = "Dashboard e indicadores" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "financeiro.view",    Description = "Contas a receber (F8)" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "despesas.view",      Description = "Contas a pagar / despesas (F9)" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "fluxocaixa.view",    Description = "Fluxo de Caixa e DRE" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "margem.view",        Description = "Tela de Margem" },
                // Relatórios / Outros
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "audit.view",         Description = "Auditoria (F10)" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "compras.view",       Description = "Módulo de Compras" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "inventario.view",    Description = "Inventário" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "notasfiscais.view",  Description = "Notas Fiscais" },
                // Admin
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "users.view",         Description = "Gestão de usuários (F7)" },
                new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "config.view",        Description = "Configurações do sistema" },
            };

            context.Permissions.AddRange(allPerms);
            context.SaveChanges();

            // Busca os cargos recém-criados
            var roles = context.Roles.IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId).ToList();

            var admin      = roles.FirstOrDefault(r => r.Name == "Administrador");
            var gerente    = roles.FirstOrDefault(r => r.Name == "Gerente");
            var supervisor = roles.FirstOrDefault(r => r.Name == "Supervisor");
            var vendedor   = roles.FirstOrDefault(r => r.Name == "Vendedor");

            // ── Administrador: tudo ───────────────────────────────────────────
            if (admin != null)
                foreach (var p in allPerms) { admin.Permissions.Add(p); p.Roles.Add(admin); }

            // ── Gerente: tudo exceto config (e agora pode ver users.view) ─────
            var gerentePerms = allPerms.Where(p => p.Code is not "config.view").ToList();
            if (gerente != null)
                foreach (var p in gerentePerms) { gerente.Permissions.Add(p); p.Roles.Add(gerente); }

            // ── Supervisor: PDV + caixa parcial + ver financeiro parcial ──────
            var supervisorCodes = new HashSet<string>
            {
                "sale.discount",     // desconto até limite
                "cash.view.summary", // ver resumo do caixa
                "report.financial",  // dashboard
                "margem.view",       // margem
                "inventario.view",   // inventário
            };
            var supervisorPerms = allPerms.Where(p => supervisorCodes.Contains(p.Code)).ToList();
            if (supervisor != null)
                foreach (var p in supervisorPerms) { supervisor.Permissions.Add(p); p.Roles.Add(supervisor); }

            // ── Vendedor: apenas desconto (com limite) ────────────────────────
            var vendedorPerms = allPerms.Where(p => p.Code == "sale.discount").ToList();
            if (vendedor != null)
                foreach (var p in vendedorPerms) { vendedor.Permissions.Add(p); p.Roles.Add(vendedor); }

            context.SaveChanges();
        }

        // ── 3. Garante usuário admin absoluto ──────────────────────────────────────────
        var roleAdmin = context.Roles.IgnoreQueryFilters()
            .FirstOrDefault(r => r.TenantId == tenantId && r.Name == "Administrador");

        if (roleAdmin != null)
        {
            var allUsers = context.Users.IgnoreQueryFilters()
                .Where(u => u.TenantId == tenantId).ToList();

            var adminUsers = allUsers.Where(u =>
                (u.Username != null && u.Username.ToLower() == "admin") ||
                (u.Username != null && u.Username.ToLower() == "mahenrique") ||
                (u.Name    != null && u.Name.ToLower().Contains("matheus")) ||
                (u.Name    != null && u.Name.ToLower().Contains("administrador"))).ToList();

            foreach (var user in adminUsers)
            {
                if (user.RoleId != roleAdmin.Id)
                {
                    user.RoleId = roleAdmin.Id;
                    context.Users.Update(user);
                }
            }
            context.SaveChanges();
        }
        else
        {
             // Se o cargo admin sumir por algum motivo cósmico, a gente injeta ele e o usuário mestre de novo (fallback extremo)
             var emergencyAdminRole = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Administrador", MaxDiscountPercentage = 100m, MaxSangriaValue = 999999m };
             context.Roles.Add(emergencyAdminRole);
             context.Users.Add(new User { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Administrador", Username = "admin", PasswordHash = "$2a$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi", IsActive = true, RoleId = emergencyAdminRole.Id });
             context.SaveChanges();
        }

       // ── 4. PATCH DE BANCOS EXISTENTES ──────────────────────────────
        // Garante que todos os cargos tenham MaxSangriaValue correto
        // e que novas permissões sejam atribuídas aos cargos correspondentes
        var dbRoles = context.Roles.IgnoreQueryFilters()
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId).ToList();

        var dbAdmin     = dbRoles.FirstOrDefault(r => r.Name == "Administrador");
        var dbGerente   = dbRoles.FirstOrDefault(r => r.Name == "Gerente");
        var dbSupervisor = dbRoles.FirstOrDefault(r => r.Name == "Supervisor");
        var dbVendedor  = dbRoles.FirstOrDefault(r => r.Name == "Vendedor");

        // Garante MaxDiscountPercentage correto (sem hardcode nos ViewModels)
        if (dbAdmin    != null) { dbAdmin.MaxDiscountPercentage    = 100m; dbAdmin.MaxSangriaValue    = 999999m; }
        if (dbGerente  != null) { dbGerente.MaxDiscountPercentage  = 30m;  dbGerente.MaxSangriaValue  = 5000m;   }
        if (dbSupervisor!=null) { dbSupervisor.MaxDiscountPercentage=15m;  dbSupervisor.MaxSangriaValue=1000m;   }
        if (dbVendedor != null) { dbVendedor.MaxDiscountPercentage  = 5m;   dbVendedor.MaxSangriaValue  = 0m;     }

        // ── Patch de permissões faltantes para tenants antigos ─────────────
        var todasPerms = context.Permissions.IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId).ToList();

        void AddPermSeFaltando(Role? role, string code)
        {
            if (role == null) return;
            if (!role.Permissions.Any(p => p.Code == code))
            {
                var perm = todasPerms.FirstOrDefault(p => p.Code == code);
                if (perm != null) role.Permissions.Add(perm);
            }
        }

        // Gerente: recebe users.view e todas as permissões exceto config.view
        foreach (var p in todasPerms.Where(p => p.Code != "config.view"))
            AddPermSeFaltando(dbGerente, p.Code);

        // Admin: recebe todas as permissões
        foreach (var p in todasPerms)
            AddPermSeFaltando(dbAdmin, p.Code);

        // Supervisor: garante suas permissões mínimas
        foreach (var code in new[] { "sale.discount", "cash.view.summary", "report.financial", "margem.view", "inventario.view" })
            AddPermSeFaltando(dbSupervisor, code);

        // Vendedor: garante sale.discount
        AddPermSeFaltando(dbVendedor, "sale.discount");

        context.SaveChanges();
    }
}