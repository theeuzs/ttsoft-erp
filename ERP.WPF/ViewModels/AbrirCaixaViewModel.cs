using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces; // 🟢 NOVO PARA BUSCAR O CAIXA NO BANCO
using Microsoft.Extensions.DependencyInjection; // 🟢 NOVO PARA INJEÇÃO DE SERVIÇO
using ERP.WPF.Commands;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class AbrirCaixaViewModel : BaseViewModel
{
    private readonly ICaixaService _caixaService;
    private readonly Guid _usuarioId;        
    private readonly string _usuarioNome;    

    public Action<decimal> OnCaixaAberto { get; set; }
    public Action OnFechar { get; set; }

    private decimal _valorAbertura;
    public decimal ValorAbertura
    {
        get => _valorAbertura;
        set => SetProperty(ref _valorAbertura, value);
    }

    public ICommand AbrirCommand { get; }

    public AbrirCaixaViewModel(ICaixaService caixaService, Guid usuarioId, string usuarioNome)
    {
        _caixaService = caixaService;
        _usuarioId = usuarioId;        
        _usuarioNome = usuarioNome;    
        
        ValorAbertura = 0; 
        AbrirCommand = new RelayCommand(async _ => await ConfirmarAberturaAsync(), _ => ValorAbertura >= 0);
    }

    private async Task ConfirmarAberturaAsync()
    {
        try
        {
            var dto = new AbrirCaixaDto
            {
                UsuarioId = _usuarioId,        
                OperadorNome = _usuarioNome,    
                ValorAbertura = ValorAbertura
            };

            // 👇 1. Manda o serviço abrir o caixa (sem esperar que ele devolva nada) 👇
            await _caixaService.AbrirCaixaAsync(dto);

            // 👇 2. Pesquisa no banco qual é o caixa que acabou de ser aberto 👇
            var uow = ERP.WPF.App.Services.GetRequiredService<IUnitOfWork>();
            var caixaAberto = await uow.Caixas.GetCaixaAbertoAsync();

            // 👇 3. Salva a gaveta na sessão do usuário! 👇
            if (caixaAberto != null)
            {
                ERP.WPF.State.AppSession.CaixaId = caixaAberto.Id;
            }

            OnCaixaAberto?.Invoke(ValorAbertura);
            OnFechar?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Caixa já aberto", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar no banco de dados:\n{ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}