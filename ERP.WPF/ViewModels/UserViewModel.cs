using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.Domain.Entities;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.WPF.ViewModels;

public class UserViewModel : BaseViewModel
{
    private readonly IUserService _userService;

    public UserViewModel(IUserService userService)
    {
        _userService = userService;
        
        ExcluirUsuarioCommand = new AsyncRelayCommand(async u => await ExcluirUsuario(u as UserDto));
        SaveCommand = new AsyncRelayCommand(_ => SalvarAsync(), _ => PodeSalvar());
        
        // Inicia o carregamento de dados
        _ = InicializarDadosAsync();
    }

    // LISTAS PARA A TELA
    public ObservableCollection<UserDto> Usuarios { get; } = new();
    public ObservableCollection<Role> Roles { get; } = new(); // Para o ComboBox

    // PROPRIEDADES DO FORMULÁRIO
    private string _nome = string.Empty;
    public string Nome { get => _nome; set { SetProperty(ref _nome, value); AtualizarBotoes(); } }

    private string _username = string.Empty;
    public string Username { get => _username; set { SetProperty(ref _username, value); AtualizarBotoes(); } }

    private string _senha = string.Empty;
    public string Senha { get => _senha; set { SetProperty(ref _senha, value); AtualizarBotoes(); } }

    private Guid _selectedRoleId;
    public Guid SelectedRoleId { get => _selectedRoleId; set { SetProperty(ref _selectedRoleId, value); } }

    // COMANDOS
    public ICommand SaveCommand { get; }
    public ICommand ExcluirUsuarioCommand { get; }

    private void AtualizarBotoes() => CommandManager.InvalidateRequerySuggested();
    private bool PodeSalvar() => !string.IsNullOrWhiteSpace(Nome) && !string.IsNullOrWhiteSpace(Username) && SelectedRoleId != Guid.Empty;

    private async Task InicializarDadosAsync()
    {
        await CarregarRolesAsync();
        await CarregarUsuariosAsync();
    }

    private async Task CarregarRolesAsync()
    {
        try 
        {
            Roles.Clear();
            using (var scope = ERP.WPF.App.Services.CreateScope())
            {
                var userSvc = scope.ServiceProvider.GetRequiredService<IUserService>();
                var lista   = await userSvc.GetRolesAsync();
                foreach (var role in lista) Roles.Add(role);
                SelectedRoleId = Roles.FirstOrDefault(r => r.Name.Contains("Vendedor"))?.Id ?? Guid.Empty;
            }
        }
        catch { /* Erro ignorado visualmente para não travar a tela */ }
    }

    private async Task CarregarUsuariosAsync()
    {
        Usuarios.Clear();
        var data = await _userService.GetAllAsync();
        foreach (var item in data) Usuarios.Add(item);
    }

    private async Task SalvarAsync()
    {
        if (string.IsNullOrWhiteSpace(Senha)) 
        {
            MessageBox.Show("A senha é obrigatória para novos usuários.", "Vila Verde");
            return;
        }

        try 
        {
            await _userService.CreateAsync(new CreateUserDto
            {
                Name = this.Nome,
                Username = this.Username,
                Password = this.Senha,
                RoleId = this.SelectedRoleId // Enviando o ID real da Role para o Banco!
            });

            MessageBox.Show("Usuário criado com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);

            // Limpa os campos
            Nome = string.Empty;
            Username = string.Empty;
            Senha = string.Empty;
            
            await CarregarUsuariosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExcluirUsuario(UserDto usuario)
    {
        if (usuario == null) return;

        // Bloqueio de segurança: Não deixar excluir o admin
        if (usuario.Username.ToLower() == "admin" || usuario.Name.ToUpper().Contains("MATHEUS"))
        {
            MessageBox.Show("Este usuário administrativo não pode ser excluído!", "Acesso Negado", MessageBoxButton.OK, MessageBoxImage.Stop);
            return;
        }

        var resposta = MessageBox.Show($"Deseja excluir permanentemente o usuário '{usuario.Name}'?", 
                                       "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (resposta == MessageBoxResult.Yes)
        {
            try
            {
                await _userService.DeleteAsync(usuario.Id); 
                Usuarios.Remove(usuario); 
                MessageBox.Show("Excluído com sucesso!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}");
            }
        }
    }
}