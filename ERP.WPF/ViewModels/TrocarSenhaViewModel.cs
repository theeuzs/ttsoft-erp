using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// ViewModel para troca obrigatória de senha (2.5 — MustChangePassword no WPF).
/// Aberta como modal bloqueante a partir do LoginViewModel quando
/// LoginResultDto.MustChangePassword == true. O usuário não consegue fechar
/// sem trocar a senha com sucesso (ver TrocarSenhaView.xaml.cs — sem botão
/// "Cancelar" e sem permitir fechar pelo X sem confirmar a troca).
/// </summary>
public class TrocarSenhaViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly Guid         _userId;

    /// <summary>Disparado ao concluir: true = senha trocada, false = usuário cancelou/erro fatal.</summary>
    public event EventHandler<bool>? OnTrocaResult;

    public TrocarSenhaViewModel(IAuthService authService, Guid userId)
    {
        _authService = authService;
        _userId      = userId;

        ConfirmarCommand = new AsyncRelayCommand(_ => ConfirmarAsync(),
            _ => !string.IsNullOrWhiteSpace(SenhaAtual)
              && !string.IsNullOrWhiteSpace(NovaSenha)
              && !string.IsNullOrWhiteSpace(ConfirmarNovaSenha));
    }

    // Senhas vêm do code-behind via PasswordChanged — PasswordBox não suporta
    // Binding direto por segurança (mesmo padrão do LoginViewModel.Senha).
    public string SenhaAtual         { get; set; } = string.Empty;
    public string NovaSenha          { get; set; } = string.Empty;
    public string ConfirmarNovaSenha { get; set; } = string.Empty;

    private string _mensagemErro = string.Empty;
    public string MensagemErro
    {
        get => _mensagemErro;
        set { SetProperty(ref _mensagemErro, value); OnPropertyChanged(nameof(TemErro)); }
    }
    public bool TemErro => !string.IsNullOrEmpty(MensagemErro);

    public ICommand ConfirmarCommand { get; }

    private async Task ConfirmarAsync()
    {
        IsBusy = true;
        MensagemErro = string.Empty;

        try
        {
            if (NovaSenha != ConfirmarNovaSenha)
            {
                MensagemErro = "A confirmação não corresponde à nova senha.";
                return;
            }

            if (NovaSenha.Length < 8)
            {
                MensagemErro = "A nova senha deve ter no mínimo 8 caracteres.";
                return;
            }

            await _authService.ChangePasswordAsync(_userId, SenhaAtual, NovaSenha);

            OnTrocaResult?.Invoke(this, true);
        }
        catch (InvalidOperationException ex)
        {
            // Senha atual incorreta, muito curta, ou igual à atual — mensagens do AuthService
            MensagemErro = ex.Message;
        }
        catch (KeyNotFoundException)
        {
            MensagemErro = "Usuário não encontrado. Faça login novamente.";
            OnTrocaResult?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            MensagemErro = $"Erro ao trocar senha: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
