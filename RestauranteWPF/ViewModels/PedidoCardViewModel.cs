using System.Windows.Input;
using RestauranteWPF.Helpers;
using RestauranteWPF.Models;

namespace RestauranteWPF.ViewModels;

/// <summary>
/// Representa UM pedido exibido na tela (card da cozinha
/// ou linha de acompanhamento na tela do garçom).
/// </summary>
public class PedidoCardViewModel : ObservableObject
{
    private string _status;

    /// <param name="pedido">Dados vindos do banco.</param>
    /// <param name="itens">Textos prontos, ex.: "2x X-Burger".</param>
    /// <param name="avancarStatus">
    /// Ação executada pelo botão do card. null = card sem botão (tela do garçom).
    /// </param>
    public PedidoCardViewModel(
        Pedido pedido,
        IEnumerable<string> itens,
        Func<PedidoCardViewModel, Task>? avancarStatus)
    {
        Id = pedido.Id;
        Mesa = pedido.Mesa;
        CriadoEm = pedido.CriadoEm.ToLocalTime(); // converte para o horário do computador
        _status = pedido.Status;
        Itens = itens.ToList();

        if (avancarStatus != null)
        {
            // O botão só fica habilitado se existir um próximo status
            AvancarStatusCommand = new AsyncRelayCommand(
                () => avancarStatus(this),
                () => ProximoStatus != null);
        }
    }

    public long Id { get; }
    public int Mesa { get; }
    public DateTimeOffset CriadoEm { get; }
    public IReadOnlyList<string> Itens { get; }

    public ICommand? AvancarStatusCommand { get; }

    // Textos prontos para a tela
    public string Titulo => $"Pedido #{Id}";
    public string MesaTexto => $"Mesa {Mesa}";
    public string Horario => CriadoEm.ToString("HH:mm");

    /// <summary>
    /// Status atual. Quando muda (clique ou Realtime), o texto do botão
    /// e o selo colorido são atualizados automaticamente.
    /// </summary>
    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(ProximoStatus));
                OnPropertyChanged(nameof(TextoBotao));
            }
        }
    }

    public string? ProximoStatus => StatusPedido.Proximo(Status);

    /// <summary>Texto do botão conforme o status atual.</summary>
    public string TextoBotao => Status switch
    {
        StatusPedido.Novo => "COMEÇAR PREPARO",
        StatusPedido.Preparando => "FINALIZAR",
        StatusPedido.Pronto => "MARCAR COMO ENTREGUE",
        _ => string.Empty
    };
}
