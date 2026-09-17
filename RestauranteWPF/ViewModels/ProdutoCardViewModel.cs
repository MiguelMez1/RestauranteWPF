using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RestauranteWPF.Helpers;
using RestauranteWPF.Models;

namespace RestauranteWPF.ViewModels;

/// <summary>
/// Representa UM card de produto na tela do garçom:
/// produto + imagem + quantidade escolhida + botões [-] e [+].
/// </summary>
public class ProdutoCardViewModel : ObservableObject
{
    private int _quantidade; // começa em 0

    public ProdutoCardViewModel(Produto produto)
    {
        Produto = produto;
        Imagem = CarregarImagem(produto.Id);

        // [+] sempre pode aumentar
        AumentarCommand = new RelayCommand(() => Quantidade++);

        // [-] só fica habilitado quando a quantidade é maior que zero
        DiminuirCommand = new RelayCommand(() => Quantidade--, () => Quantidade > 0);
    }

    /// <summary>Avisa o GarcomViewModel para recalcular o resumo e o total.</summary>
    public event EventHandler? QuantidadeAlterada;

    public Produto Produto { get; }
    public string Nome => Produto.Nome;
    public decimal Preco => Produto.Preco;

    /// <summary>Imagem do produto, ou null se o arquivo não existir.</summary>
    public ImageSource? Imagem { get; }

    public ICommand AumentarCommand { get; }
    public ICommand DiminuirCommand { get; }

    /// <summary>
    /// Quantidade selecionada. Math.Max garante que nunca fique negativa.
    /// </summary>
    public int Quantidade
    {
        get => _quantidade;
        set
        {
            var valorSeguro = Math.Max(0, value);
            if (SetProperty(ref _quantidade, valorSeguro))
            {
                // Propriedades calculadas que dependem da quantidade
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(TextoResumo));
                QuantidadeAlterada?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Preço unitário × quantidade.</summary>
    public decimal Subtotal => Preco * Quantidade;

    /// <summary>Texto usado no resumo, ex.: "2x X-Burger".</summary>
    public string TextoResumo => $"{Quantidade}x {Nome}";

    /// <summary>
    /// Procura a imagem em "Imagens/{id}.png" (pasta copiada para o bin).
    /// Se não existir, a tela mostra um espaço cinza no lugar.
    /// </summary>
    private static ImageSource? CarregarImagem(long produtoId)
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "Imagens", $"{produtoId}.png");
        if (!File.Exists(caminho))
            return null;

        var imagem = new BitmapImage();
        imagem.BeginInit();
        imagem.UriSource = new Uri(caminho, UriKind.Absolute);
        imagem.CacheOption = BitmapCacheOption.OnLoad; // lê o arquivo e libera
        imagem.DecodePixelWidth = 300;                 // economiza memória
        imagem.EndInit();
        imagem.Freeze();                               // imagem somente leitura (mais leve)
        return imagem;
    }
}
