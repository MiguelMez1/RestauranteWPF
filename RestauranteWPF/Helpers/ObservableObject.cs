using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RestauranteWPF.Helpers;

/// <summary>
/// Classe base dos ViewModels.
/// Implementa INotifyPropertyChanged: quando uma propriedade muda,
/// o WPF é avisado e atualiza automaticamente o que está na tela (Binding).
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Avisa a interface que a propriedade mudou.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? nomePropriedade = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nomePropriedade));
    }

    /// <summary>
    /// Altera o campo e avisa a interface, mas só se o valor for diferente.
    /// Retorna true quando houve mudança.
    /// </summary>
    protected bool SetProperty<T>(ref T campo, T valor, [CallerMemberName] string? nomePropriedade = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
            return false;

        campo = valor;
        OnPropertyChanged(nomePropriedade);
        return true;
    }
}
