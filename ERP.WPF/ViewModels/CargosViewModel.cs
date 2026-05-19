using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

// ── Itens de apresentação ────────────────────────────────────────────────────

public class PermissionItemVm : BaseViewModel
{
    public Guid   Id          { get; set; }
    public string Code        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}

public class PermissionGroupVm : BaseViewModel
{
    public string GroupName { get; set; } = string.Empty;
    public ObservableCollection<PermissionItemVm> Permissions { get; } = new();
}

// ── ViewModel principal ──────────────────────────────────────────────────────

public class CargosViewModel : BaseViewModel
{
    private readonly IRoleService _roleService;

    // ── Listas ──────────────────────────────────────────────────────────────
    public ObservableCollection<RoleDto>         Roles           { get; } = new();
    public ObservableCollection<PermissionGroupVm> PermissionGroups { get; } = new();

    // ── Cargo selecionado ────────────────────────────────────────────────────
    private RoleDto? _selectedRole;
    public RoleDto? SelectedRole
    {
        get => _selectedRole;
        set
        {
            SetProperty(ref _selectedRole, value);
            if (value != null) CarregarPermissoesDosCargo(value);
            OnPropertyChanged(nameof(TemCargoSelecionado));
            OnPropertyChanged(nameof(PodeExcluir));
            OnPropertyChanged(nameof(IsCargoEditavel));
        }
    }

    public bool TemCargoSelecionado => SelectedRole != null;
    public bool PodeExcluir         => SelectedRole != null && !SelectedRole.IsProtected && SelectedRole.UserCount == 0;
    public bool IsCargoEditavel      => SelectedRole != null && !SelectedRole.IsProtected;

    // ── Campos editáveis do cargo selecionado ────────────────────────────────
    private decimal _maxDiscount;
    public decimal MaxDiscount
    {
        get => _maxDiscount;
        set => SetProperty(ref _maxDiscount, value);
    }

    private decimal _maxSangria;
    public decimal MaxSangria
    {
        get => _maxSangria;
        set => SetProperty(ref _maxSangria, value);
    }

    // Sprint E: Percentual de comissão configurável por cargo
    private decimal _percentualComissao;
    public decimal PercentualComissao
    {
        get => _percentualComissao;
        set => SetProperty(ref _percentualComissao, value);
    }

    // ── Novo cargo ───────────────────────────────────────────────────────────
    private string _novoCargoNome = string.Empty;
    public string NovoCargoNome
    {
        get => _novoCargoNome;
        set { SetProperty(ref _novoCargoNome, value); OnPropertyChanged(nameof(PodeCriar)); }
    }

    public bool PodeCriar => !string.IsNullOrWhiteSpace(NovoCargoNome);

    // ── Comandos ─────────────────────────────────────────────────────────────
    public ICommand SalvarCommand  { get; }
    public ICommand CriarCommand   { get; }
    public ICommand ExcluirCommand { get; }

    public CargosViewModel(IRoleService roleService)
    {
        _roleService = roleService;

        SalvarCommand  = new AsyncRelayCommand(_ => SalvarAsync(),  _ => TemCargoSelecionado && !IsBusy);
        CriarCommand   = new AsyncRelayCommand(_ => CriarAsync(),   _ => PodeCriar && !IsBusy);
        ExcluirCommand = new AsyncRelayCommand(_ => ExcluirAsync(), _ => PodeExcluir && !IsBusy);

        _ = CarregarAsync();
    }

    // ── Carregamento inicial ──────────────────────────────────────────────────
    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var roles = (await _roleService.GetAllAsync()).ToList();
            var perms = (await _roleService.GetAllPermissionsAsync()).ToList();

            Roles.Clear();
            foreach (var r in roles) Roles.Add(r);

            // Monta grupos de permissão uma vez só
            PermissionGroups.Clear();
            foreach (var grupo in perms.GroupBy(p => p.Group).OrderBy(g => g.Key))
            {
                var groupVm = new PermissionGroupVm { GroupName = grupo.Key };
                foreach (var p in grupo.OrderBy(p => p.Description))
                {
                    groupVm.Permissions.Add(new PermissionItemVm
                    {
                        Id          = p.Id,
                        Code        = p.Code,
                        Description = p.Description,
                        IsChecked   = false
                    });
                }
                PermissionGroups.Add(groupVm);
            }

            // Seleciona o primeiro cargo automaticamente
            if (Roles.Count > 0) SelectedRole = Roles[0];
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar cargos: {ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    // ── Preenche checkboxes com as permissões do cargo selecionado ─────────
    private void CarregarPermissoesDosCargo(RoleDto role)
    {
        MaxDiscount        = role.MaxDiscountPercentage;
        MaxSangria         = role.MaxSangriaValue;
        PercentualComissao = role.PercentualComissao;

        var codes = new HashSet<string>(role.PermissionCodes, StringComparer.OrdinalIgnoreCase);
        foreach (var group in PermissionGroups)
            foreach (var perm in group.Permissions)
                perm.IsChecked = codes.Contains(perm.Code);
    }

    // ── Salvar alterações no cargo selecionado ────────────────────────────
    private async Task SalvarAsync()
    {
        if (SelectedRole == null) return;
        if (SelectedRole.IsProtected)
        {
            MessageBox.Show("O cargo Administrador não pode ser editado.", "Protegido",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var permIds = PermissionGroups
                .SelectMany(g => g.Permissions)
                .Where(p => p.IsChecked)
                .Select(p => p.Id)
                .ToList();

            await _roleService.UpdateAsync(new UpdateRoleDto
            {
                Id                   = SelectedRole.Id,
                MaxDiscountPercentage = MaxDiscount,
                MaxSangriaValue      = MaxSangria,
                PermissionIds        = permIds,
                PercentualComissao   = PercentualComissao
            });

            // Atualiza o item na lista local
            SelectedRole.MaxDiscountPercentage = MaxDiscount;
            SelectedRole.MaxSangriaValue       = MaxSangria;
            SelectedRole.PercentualComissao    = PercentualComissao;
            SelectedRole.PermissionCodes       = PermissionGroups
                .SelectMany(g => g.Permissions)
                .Where(p => p.IsChecked)
                .Select(p => p.Code)
                .ToList();

            MessageBox.Show($"✅ Cargo \"{SelectedRole.Name}\" salvo com sucesso!", "TTSoft ERP",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    // ── Criar novo cargo ──────────────────────────────────────────────────
    private async Task CriarAsync()
    {
        if (string.IsNullOrWhiteSpace(NovoCargoNome)) return;

        if (Roles.Any(r => r.Name.Equals(NovoCargoNome.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Já existe um cargo com esse nome.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            var novo = await _roleService.CreateAsync(new CreateRoleDto
            {
                Name                 = NovoCargoNome.Trim(),
                MaxDiscountPercentage = 0m,
                MaxSangriaValue      = 0m,
                PermissionIds        = new()
            });

            Roles.Add(novo);
            SelectedRole = novo;
            NovoCargoNome = string.Empty;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao criar cargo: {ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    // ── Excluir cargo ─────────────────────────────────────────────────────
    private async Task ExcluirAsync()
    {
        if (SelectedRole == null) return;

        var confirm = MessageBox.Show(
            $"Excluir o cargo \"{SelectedRole.Name}\"?\nEssa ação não pode ser desfeita.",
            "Confirmar exclusão",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var ok = await _roleService.DeleteAsync(SelectedRole.Id);
            if (ok)
            {
                var removed = SelectedRole;
                SelectedRole = null;
                Roles.Remove(removed);
                if (Roles.Count > 0) SelectedRole = Roles[0];
            }
            else
            {
                MessageBox.Show(
                    "Não foi possível excluir o cargo.\nVerifique se há usuários vinculados.",
                    "Não permitido", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao excluir: {ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}