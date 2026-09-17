using System.Windows;
using RestauranteWPF.ViewModels;

namespace RestauranteWPF.Views;

/// <summary>
/// Code-behind da Tela do Garçom.
/// Fica propositalmente vazio: toda a lógica está no GarcomViewModel.
/// </summary>
public partial class GarcomWindow : Window
{
    public GarcomWindow(GarcomViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel; // liga a tela ao ViewModel (Bindings)
    }
}
