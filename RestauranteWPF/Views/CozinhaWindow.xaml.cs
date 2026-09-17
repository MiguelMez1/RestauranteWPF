using System.Windows;
using RestauranteWPF.ViewModels;

namespace RestauranteWPF.Views;

/// <summary>
/// Code-behind da Tela da Cozinha.
/// Toda a lógica está no CozinhaViewModel.
/// </summary>
public partial class CozinhaWindow : Window
{
    public CozinhaWindow(CozinhaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
