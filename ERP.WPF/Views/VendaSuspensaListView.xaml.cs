using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.ViewModels;
using System;
using System.Windows;

namespace ERP.WPF.Views;

public partial class VendaSuspensaListView : Window
{
    /// <summary>Preenchido se o usuário retomou uma venda suspensa com sucesso.</summary>
    public VendaSuspensaDetalheDto? VendaRetomada { get; private set; }

    public VendaSuspensaListView(IVendaSuspensaService service, Guid usuarioId, string nomeUsuario)
    {
        InitializeComponent();

        var vm = new VendaSuspensaListViewModel(service, usuarioId, nomeUsuario);
        vm.OnRetomarConfirmado = detalhe =>
        {
            VendaRetomada = detalhe;
            DialogResult = true;
            Close();
        };
        vm.OnFechar = () =>
        {
            DialogResult = false;
            Close();
        };

        DataContext = vm;
    }
}
