using System.Windows;

namespace RestauranteWPF.Helpers;

/// <summary>
/// No WPF, só a thread da interface pode alterar controles e coleções
/// ligadas à tela. Os eventos do Realtime chegam em outra thread,
/// então usamos o Dispatcher para "entregar" o trabalho à thread da UI.
/// </summary>
public static class UiThread
{
    /// <summary>Executa uma ação síncrona na thread da interface.</summary>
    public static void Executar(Action acao)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return; // o programa está sendo fechado

        if (dispatcher.CheckAccess())
            acao();
        else
            dispatcher.BeginInvoke(acao);
    }

    /// <summary>
    /// Executa uma ação assíncrona na thread da interface.
    /// Depois de cada "await", o código continua na mesma thread da UI.
    /// A própria ação deve tratar seus erros (try/catch).
    /// </summary>
    public static void Executar(Func<Task> acao)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        dispatcher.BeginInvoke(new Action(async () => await acao()));
    }
}
