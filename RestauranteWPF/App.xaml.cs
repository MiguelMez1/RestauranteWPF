using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using RestauranteWPF.Services;
using RestauranteWPF.ViewModels;
using RestauranteWPF.Views;

namespace RestauranteWPF;

/// <summary>
/// Ponto de partida do programa.
/// 1. Configura o idioma (R$ e datas no formato brasileiro);
/// 2. Conecta ao Supabase;
/// 3. Cria os ViewModels e abre as duas telas.
/// </summary>
public partial class App : Application
{
    private SupabaseService? _servico;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigurarCulturaBrasileira();

        // Impede rodar com a configuração de exemplo
        if (SupabaseConfig.NaoConfigurado)
        {
            MessageBox.Show(
                "Preencha a URL e a chave anon do Supabase no arquivo SupabaseConfig.cs.",
                "Configuração necessária", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // Conecta ao Supabase (banco + Realtime)
        try
        {
            _servico = new SupabaseService(SupabaseConfig.Url, SupabaseConfig.ChaveAnon);
            await _servico.InicializarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível conectar ao Supabase.\n\nDetalhes: {ex.Message}",
                "Erro de conexão", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Os dois ViewModels usam o MESMO serviço (uma única conexão)
        var garcomViewModel = new GarcomViewModel(_servico);
        var cozinhaViewModel = new CozinhaViewModel(_servico);

        var telaGarcom = new GarcomWindow(garcomViewModel);
        var telaCozinha = new CozinhaWindow(cozinhaViewModel);

        telaGarcom.Show();
        telaCozinha.Show();

        // Carrega os dados das duas telas ao mesmo tempo
        await Task.WhenAll(garcomViewModel.CarregarAsync(), cozinhaViewModel.CarregarAsync());
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _servico?.Encerrar(); // fecha a conexão do Realtime
        base.OnExit(e);
    }

    /// <summary>
    /// Faz o WPF usar pt-BR: StringFormat=C vira "R$ 25,90".
    /// </summary>
    private static void ConfigurarCulturaBrasileira()
    {
        var cultura = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));
    }
}
