using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class QuickCustomerViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;

    // Evento para avisar a tela para fechar (mandando 'true' se salvou com sucesso)
    public event EventHandler<bool> OnRequestClose;

    // Guarda o nome do cliente salvo para o sistema selecionar ele automaticamente depois
    public string NomeSalvo { get; private set; } = string.Empty;

    public QuickCustomerViewModel(ICustomerService customerService)
    {
        _customerService = customerService;
        SalvarCommand = new AsyncRelayCommand(_ => SalvarClienteAsync(), _ => !string.IsNullOrWhiteSpace(Nome));
    }

    // --- CAMPOS DO FORMULÁRIO RÁPIDO ---
    private string _nome = string.Empty;
    public string Nome 
    { 
        get => _nome; 
        set 
        { 
            SetProperty(ref _nome, value); 
            
            // Trocamos o RaiseCanExecuteChanged por essa linha mágica do WPF:
            CommandManager.InvalidateRequerySuggested(); 
        } 
    }

    private string _documento = string.Empty;
    public string Documento { get => _documento; set => SetProperty(ref _documento, value); }

    private string _telefone = string.Empty;
    public string Telefone { get => _telefone; set => SetProperty(ref _telefone, value); }

    private string _rua = string.Empty;
    public string Rua { get => _rua; set => SetProperty(ref _rua, value); }

    private string _numero = string.Empty;
    public string Numero { get => _numero; set => SetProperty(ref _numero, value); }

    private string _bairro = string.Empty;
    public string Bairro { get => _bairro; set => SetProperty(ref _bairro, value); }

    // --- COMANDOS ---
    public ICommand CancelarCommand => new RelayCommand(_ => OnRequestClose?.Invoke(this, false));
    public ICommand SalvarCommand { get; }

    private async Task SalvarClienteAsync()
    {
        IsBusy = true;
        try
        {
            var dto = new CreateCustomerDto
            {
                Name = this.Nome,
                Document = this.Documento,
                Phone = this.Telefone,
                Street = this.Rua,
                Number = this.Numero,
                Neighborhood = this.Bairro
            };

            await _customerService.CreateAsync(dto);
            
            NomeSalvo = this.Nome; // Guarda o nome para buscar na lista depois
            
            OnRequestClose?.Invoke(this, true); // Fecha a tela com Sucesso
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar cliente: {ex.Message}", "Erro");
        }
        finally
        {
            IsBusy = false;
        }
    }
}