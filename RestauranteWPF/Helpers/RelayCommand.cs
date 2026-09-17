using System.Windows.Input;

namespace RestauranteWPF.Helpers;

/// <summary>
/// Comando simples para ligar botões a métodos do ViewModel
/// (sem precisar de eventos Click no code-behind).
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _executar;
    private readonly Func<bool>? _podeExecutar;

    public RelayCommand(Action executar, Func<bool>? podeExecutar = null)
    {
        _executar = executar;
        _podeExecutar = podeExecutar;
    }

    // O CommandManager do WPF reavalia automaticamente se o botão
    // deve ficar habilitado (ex.: botão "-" desabilita com quantidade 0).
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _podeExecutar?.Invoke() ?? true;

    public void Execute(object? parameter) => _executar();
}

/// <summary>
/// Comando para métodos assíncronos (que acessam o banco).
/// Enquanto está executando, o botão fica desabilitado —
/// isso evita cliques duplos e pedidos duplicados.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _executar;
    private readonly Func<bool>? _podeExecutar;
    private bool _executando;

    public AsyncRelayCommand(Func<Task> executar, Func<bool>? podeExecutar = null)
    {
        _executar = executar;
        _podeExecutar = podeExecutar;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) =>
        !_executando && (_podeExecutar?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _executando = true;
        CommandManager.InvalidateRequerySuggested(); // desabilita o botão
        try
        {
            await _executar();
        }
        finally
        {
            _executando = false;
            CommandManager.InvalidateRequerySuggested(); // reabilita o botão
        }
    }
}
