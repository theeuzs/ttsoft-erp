using ERP.Application.DTOs;
using ERP.WPF.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Windows; 
using System;

namespace ERP.WPF.Views;

public partial class PdvView : UserControl
{
    public PdvView()
    {
        InitializeComponent();
        
        // Garante que o UserControl pode receber o foco para o "Estado Neutro" funcionar
        this.Focusable = true; 
        
        var app = System.Windows.Application.Current;
        var property = app.GetType().GetProperty("ServiceProvider");
        if (property != null)
        {
            var provider = property.GetValue(app) as IServiceProvider;
            var vm = provider?.GetRequiredService<PdvViewModel>();
            this.DataContext = vm;
        }

        // A MÁGICA COMEÇA AQUI: Criamos a blindagem ao abrir e removemos ao fechar a tela
        this.Loaded += PdvView_Loaded;
        this.Unloaded += PdvView_Unloaded;

        TxtBuscaProduto.PreviewKeyDown += TxtBuscaProduto_PreviewKeyDown;
        GridPesquisa.PreviewKeyDown += GridPesquisa_PreviewKeyDown;
        TxtBuscaCliente.PreviewKeyDown += TxtBuscaCliente_PreviewKeyDown;
    }

    private void PdvView_Loaded(object sender, RoutedEventArgs e)
    {
        FocarNaBusca();
        
        // O ESCUDO ABSOLUTO: Pega a MainWindow direto da memória central do Windows
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.RemoveHandler(UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(Janela_PreviewKeyDown_Escudo));
            // handledEventsToo=true garante que capturamos o ESC mesmo quando
            // o DataGrid já o marcou como Handled internamente
            mainWindow.AddHandler(UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(Janela_PreviewKeyDown_Escudo), handledEventsToo: true);
        }
    }

    private void PdvView_Unloaded(object sender, RoutedEventArgs e)
    {
        // Abaixa o escudo quando você mudar de aba (pro Dashboard, Produtos, etc)
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.RemoveHandler(UIElement.PreviewKeyDownEvent,
                new KeyEventHandler(Janela_PreviewKeyDown_Escudo));
        }
    }

    // =====================================================================
    // O ESCUDO E O ESTADO NEUTRO (A Mente do PDV)
    // =====================================================================
    private void Janela_PreviewKeyDown_Escudo(object sender, KeyEventArgs e)
    {
        // 1. ESC: Limpa tudo e deixa o caixa em estado "Neutro"
        if (e.Key == Key.Escape)
        {
            e.Handled = true; 
            
            TxtBuscaProduto.Text = string.Empty;
            TxtBuscaProduto.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            
            EntrarEmEstadoNeutro(); 
            return;
        }

        // 2. A TRAVA DA BARRA DE PESQUISA: Se o cursor estiver piscando em qualquer TextBox, 
        // a gente não aciona atalho nenhum, deixamos o usuário digitar normalmente!
        if (e.OriginalSource is TextBox) 
            return;

        // =================================================================
        // ATALHOS GLOBAIS (Só chegam aqui se o caixa estiver "Neutro")
        // =================================================================
        
        // I: Cancelar/Limpar Carrinho
        if (e.Key == Key.I)
        {
            e.Handled = true; // Pega o evento para o 'I' não ir parar no limbo
            
            if (DataContext is PdvViewModel vm)
            {
                // Dispara a pergunta de segurança
                var resposta = MessageBox.Show(
                    "Deseja realmente CANCELAR todos os itens do carrinho?", 
                    "Aviso de Cancelamento", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);

                if (resposta == MessageBoxResult.Yes)
                {
                    if (vm.ClearCartCommand != null && vm.ClearCartCommand.CanExecute(null))
                    {
                        vm.ClearCartCommand.Execute(null);
                    }
                    else if (vm.CartItems != null)
                    {
                        vm.CartItems.Clear(); // Limpeza bruta na lista se não houver o comando
                    }
                    
                    FocarNaBusca(); // Após cancelar, já joga o cursor de volta pra busca pra próxima venda
                }
            }
            return;
        }

        // ESPAÇO: Puxa o foco de volta para a barra de pesquisa
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            FocarNaBusca();
            return;
        }

        // M: Abre a Consulta de Preço
        if (e.Key == Key.M)
        {
            e.Handled = true;
            AbrirTelaConsultaPreco();
            return;
        }

        // V: Finalizar Venda
        if (e.Key == Key.V)
        {
            if (DataContext is PdvViewModel vm && vm.FinalizeSaleCommand.CanExecute(null))
            {
                e.Handled = true;
                vm.FinalizeSaleCommand.Execute(null);
            }
            return;
        }

        // ── Sprint 5: atalhos de função ────────────────────────────────────────

        // F9: Suspender venda atual
        if (e.Key == Key.F9)
        {
            e.Handled = true;
            if (DataContext is PdvViewModel f9Vm && f9Vm.SuspenderVendaCommand.CanExecute(null))
                f9Vm.SuspenderVendaCommand.Execute(null);
            return;
        }

        // F10: Abrir lista de vendas suspensas
        if (e.Key == Key.F10)
        {
            e.Handled = true;
            if (DataContext is PdvViewModel f10Vm && f10Vm.AbrirVendasSuspensasCommand.CanExecute(null))
                f10Vm.AbrirVendasSuspensasCommand.Execute(null);
            return;
        }

        // F11: Alternar modo tela cheia
        if (e.Key == Key.F11)
        {
            e.Handled = true;
            var main = Window.GetWindow(this);
            if (main != null)
            {
                if (main.WindowState == WindowState.Maximized && main.WindowStyle == WindowStyle.None)
                {
                    main.WindowStyle = WindowStyle.SingleBorderWindow;
                    main.WindowState = WindowState.Normal;
                }
                else
                {
                    main.WindowStyle = WindowStyle.None;
                    main.WindowState = WindowState.Maximized;
                }
            }
            return;
        }

        // F12: Finalizar venda com pagamento Dinheiro direto (atalho de balcão)
        if (e.Key == Key.F12)
        {
            e.Handled = true;
            if (DataContext is PdvViewModel f12Vm && f12Vm.FinalizeSaleCommand.CanExecute(null))
                f12Vm.FinalizeSaleCommand.Execute(null);
            return;
        }
    }

    private void AbrirTelaConsultaPreco()
    {
        // 1. Pega o "cérebro" do PDV para podermos mandar o produto pro carrinho depois
        var pdvVm = this.DataContext as ERP.WPF.ViewModels.PdvViewModel;

        // 👇 2. A CORREÇÃO ESTÁ AQUI 👇
        // Em vez de pedir a ViewModel inteira, pedimos só o serviço de produtos e montamos ela!
        var productService = ERP.WPF.App.Services.GetRequiredService<ERP.Application.Interfaces.IProductService>();
        var consultaVm = new ERP.WPF.ViewModels.ConsultaPrecoViewModel(productService);

        // 👇 3. A PONTE MÁGICA 👇
        consultaVm.OnAdicionarAoCarrinho = (produtoEncontrado, qtdDigitada, isAtacado) => 
        {
            // Passa a quantidade que o caixa digitou
            produtoEncontrado.QuantidadeGrade = qtdDigitada;
            
            // Manda o PDV executar o comando de Adicionar ao Carrinho!
            if (pdvVm != null && pdvVm.AddToCartCommand.CanExecute(produtoEncontrado))
            {
                pdvVm.AddToCartCommand.Execute(produtoEncontrado);
            }
        };

        // 4. Abre a janela flutuante
        var window = new ERP.WPF.Views.ConsultaPrecoView(consultaVm);
        window.Owner = Window.GetWindow(this); // Prende a janelinha na tela principal
        window.ShowDialog();
    }

    private void RolarParaOItem(System.Windows.Controls.Primitives.Selector lista)
    {
        if (lista.SelectedItem == null) return;
        if (lista is DataGrid dg) dg.ScrollIntoView(dg.SelectedItem);
        else if (lista is ListBox lb) lb.ScrollIntoView(lb.SelectedItem);
    }

    // =====================================================================
    // QUANTIDADE INLINE E NAVEGAÇÃO
    // =====================================================================
    private void TxtQuantidadeInline_KeyDown(object sender, KeyEventArgs e)
    {
        // Trava do ESC caso ele passe pela grade
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            TxtBuscaProduto.Text = string.Empty;
            EntrarEmEstadoNeutro(); // Estado Neutro
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            
            var textBox = sender as TextBox;
            textBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            var vm = DataContext as PdvViewModel;
            if (vm != null && textBox?.DataContext is ProductDto product)
            {
                if (vm.AddToCartCommand.CanExecute(product))
                {
                    vm.AddToCartCommand.Execute(product);
                }

                product.QuantidadeGrade = 1;
                if (textBox != null) textBox.Text = "1";
                
                FocarNaBusca();
            }
        }
    }

    private void TxtBuscaCliente_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ListClientes.Items.Count == 0) return;

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            if (ListClientes.SelectedIndex < ListClientes.Items.Count - 1)
            {
                ListClientes.SelectedIndex++;
                ListClientes.ScrollIntoView(ListClientes.SelectedItem);
            }
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (ListClientes.SelectedIndex > 0)
            {
                ListClientes.SelectedIndex--;
                ListClientes.ScrollIntoView(ListClientes.SelectedItem);
            }
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (ListClientes.SelectedIndex == -1 && ListClientes.Items.Count > 0) ListClientes.SelectedIndex = 0;

            if (ListClientes.SelectedItem is CustomerDto customer && DataContext is PdvViewModel vm)
            {
                if (vm.SelectCustomerCommand.CanExecute(customer))
                    vm.SelectCustomerCommand.Execute(customer);
                FocarNaBusca(); 
            }
        }
    }

    private async void TxtBuscaProduto_PreviewKeyDown(object sender, KeyEventArgs e)
{
    var vm = DataContext as PdvViewModel;
    System.Windows.Controls.Primitives.Selector listaAtiva = (vm != null && vm.IsGridView) ? GridPesquisa_Cards : GridPesquisa;

    if (listaAtiva.Items.Count == 0) return;

    // ⬇️ SETA PARA BAIXO (Navega sem tirar o cursor da pesquisa!)
    if (e.Key == Key.Down)
    {
        e.Handled = true; 
        if (listaAtiva.SelectedIndex < listaAtiva.Items.Count - 1)
            listaAtiva.SelectedIndex++; // Desce um
        else if (listaAtiva.SelectedIndex == -1)
            listaAtiva.SelectedIndex = 0; // Se não tem nenhum, pega o primeiro

        RolarParaOItem(listaAtiva);
        return;
    }
    // ⬆️ SETA PARA CIMA
    else if (e.Key == Key.Up)
    {
        e.Handled = true; 
        if (listaAtiva.SelectedIndex > 0)
            listaAtiva.SelectedIndex--; // Sobe um

        RolarParaOItem(listaAtiva);
        return;
    }
    // ⏭️ TAB (Pula da barra de pesquisa direto pra caixinha de quantidade)
    else if (e.Key == Key.Tab)
    {
        if (listaAtiva is DataGrid dg && dg.SelectedIndex >= 0)
        {
            e.Handled = true;
            dg.UpdateLayout();
            var row = (DataGridRow)dg.ItemContainerGenerator.ContainerFromIndex(dg.SelectedIndex);
            if (row != null)
            {
                var cell = GetCell(dg, row, 5); // Índice 5 é a coluna QTD no XAML
                var textBoxQtd = FindVisualChild<TextBox>(cell);
                if (textBoxQtd != null)
                {
                    textBoxQtd.Focus();
                    textBoxQtd.SelectAll(); 
                }
            }
        }
        return;
    }
    // 🟢 ENTER (Adiciona o produto selecionado no carrinho)
    else if (e.Key == Key.Enter)
    {
        e.Handled = true;
        var textBox = sender as TextBox;
        textBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

        if (vm == null) return;
        await System.Threading.Tasks.Task.Delay(350); // Dá tempo da busca terminar

        if (listaAtiva.Items.Count > 0)
        {
            if (listaAtiva.SelectedIndex == -1) listaAtiva.SelectedIndex = 0;

            if (listaAtiva.SelectedItem is ProductDto product)
            {
                if (vm.AddToCartCommand.CanExecute(product)) 
                    vm.AddToCartCommand.Execute(product);
                
                vm.SearchTerm = string.Empty;
                FocarNaBusca(); // Limpa e foca pra próxima venda
            }
        }
    }
}

    private void GridPesquisa_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true; 
            TxtBuscaProduto.Text = string.Empty;
            TxtBuscaProduto.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            EntrarEmEstadoNeutro(); // Estado Neutro
            return;
        }

        // A MÁGICA DO TAB: Pula da linha pra caixinha de quantidade!
        if (e.Key == Key.Tab)
        {
            e.Handled = true; 
            
            var dg = sender as DataGrid;
            if (dg != null && dg.SelectedIndex >= 0)
            {
                var row = (DataGridRow)dg.ItemContainerGenerator.ContainerFromIndex(dg.SelectedIndex);
                if (row != null)
                {
                    var cell = GetCell(dg, row, 5); 
                    var textBox = FindVisualChild<TextBox>(cell);
                    if (textBox != null)
                    {
                        textBox.Focus();
                        textBox.SelectAll(); 
                    }
                }
            }
            return;
        }

        // O ENTER RÁPIDO: Adiciona 1 se estiver na linha, ou deixa passar se estiver na QTD
        if (e.Key == Key.Enter)
        {
            if (e.OriginalSource is TextBox) 
            {
                return; 
            }
            
            e.Handled = true; 
            var vm = DataContext as PdvViewModel;
            if (vm != null && GridPesquisa.SelectedItem is ProductDto product)
            {
                if (vm.AddToCartCommand.CanExecute(product))
                {
                    vm.AddToCartCommand.Execute(product);
                }
                
                TxtBuscaProduto.Text = string.Empty;
                FocarNaBusca();
            }
        }
    }

    // =====================================================================
    // ATALHOS GLOBAIS ANTIGOS (Esvaziado para não dar erro no XAML)
    // =====================================================================
    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Deixei vazio propositalmente!
        // Toda a inteligência do teclado foi transferida para o Janela_PreviewKeyDown_Escudo lá em cima.
        // Assim o XAML não acusa erro de compilação por não achar esse método.
    }

    private void FocarNaBusca()
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            TxtBuscaProduto.Focus();
            Keyboard.Focus(TxtBuscaProduto);
            TxtBuscaProduto.CaretIndex = TxtBuscaProduto.Text.Length; 
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void EntrarEmEstadoNeutro()
    {
        // Força o fundo da tela a receber o foco. 
        // Assim o WPF continua "ouvindo" o seu teclado para os atalhos de M e Espaço.
        this.Focus();
        Keyboard.Focus(this);
    }

    // Auxiliares para achar a caixinha de QTD dentro da DataGrid
    private static DataGridCell GetCell(DataGrid grid, DataGridRow row, int column)
    {
        if (row != null)
        {
            var presenter = FindVisualChild<System.Windows.Controls.Primitives.DataGridCellsPresenter>(row);
            if (presenter == null) return null;
            return presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
        }
        return null;
    }
    
    private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
            if (child != null && child is T) return (T)child;
            else
            {
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
        }
        return null;
    }

    private void CartScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange > 0) CartScrollViewer.ScrollToEnd();
    }

    private void BtnLicenca_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Lembre de ajustar o 'ERP.WPF.Views' se a sua tela estiver em outra pasta!
        ERP.WPF.Views.LicencaWindow tela = new ERP.WPF.Views.LicencaWindow();
        tela.ShowDialog(); 
    }

    private void BtnTema_Click(object sender, RoutedEventArgs e)
    {
        new TemaView().ShowDialog();
    }

    // =====================================================================
    // LIBERAÇÃO DA VÍRGULA PARA FRAÇÕES (EX: CANOS, AREIA, ETC)
    // =====================================================================
    private void TxtQuantidade_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Permite apenas números e uma única vírgula
        var textBox = sender as TextBox;
        string novoTexto = textBox.Text + e.Text;

        // Se o que o usuário digitou for um número válido OU apenas uma vírgula/ponto inicial, permite
        bool ok = System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9,]$");
        
        // Não deixa colocar duas vírgulas
        if (e.Text == "," && textBox.Text.Contains(","))
        {
            ok = false;
        }

        e.Handled = !ok;
    }
}