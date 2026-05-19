using System.Windows;

namespace ERP.WPF.Views;

public partial class QuickAddView : Window
{
    // Variáveis que a tela principal vai ler depois que o PopUp fechar
    public string ItemName { get; private set; } = string.Empty;
    public bool IsSaved { get; private set; } = false;

    // O construtor recebe o título ("Nova Marca", "Nova Categoria", etc)
    public QuickAddView(string title)
    {
        InitializeComponent();
        TxtTitle.Text = title;
        
        // Coloca o cursor piscando no campo de texto automaticamente
        Loaded += (s, e) => TxtName.Focus();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Por favor, informe o nome.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ItemName = TxtName.Text.Trim();
        IsSaved = true;
        Close(); // Fecha a janelinha
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close(); // Fecha sem salvar
    }
}