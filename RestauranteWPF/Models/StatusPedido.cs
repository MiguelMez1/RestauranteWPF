namespace RestauranteWPF.Models;

/// <summary>
/// Centraliza os status possíveis de um pedido e a ordem do fluxo:
/// NOVO → PREPARANDO → PRONTO → ENTREGUE.
/// Usar constantes evita erros de digitação espalhados pelo código.
/// </summary>
public static class StatusPedido
{
    public const string Novo = "NOVO";
    public const string Preparando = "PREPARANDO";
    public const string Pronto = "PRONTO";
    public const string Entregue = "ENTREGUE";

    /// <summary>
    /// Retorna o próximo status do fluxo, ou null quando o pedido já foi entregue.
    /// </summary>
    public static string? Proximo(string status) => status switch
    {
        Novo => Preparando,
        Preparando => Pronto,
        Pronto => Entregue,
        _ => null
    };
}
