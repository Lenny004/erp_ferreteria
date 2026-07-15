namespace Ferreteria.PuntoVenta.Services.Domain;

/// <summary>
/// Cancela búsquedas async previas cuando el usuario escribe en un cuadro de texto (patrón debounce por cancelación).
/// </summary>
internal sealed class AsyncSearchCoordinator : IDisposable
{
    private CancellationTokenSource? _currentSearch;

    /// <summary>Inicia una nueva búsqueda y cancela la anterior si aún está en curso.</summary>
    public CancellationToken BeginNewSearch()
    {
        _currentSearch?.Cancel();
        _currentSearch?.Dispose();
        _currentSearch = new CancellationTokenSource();
        return _currentSearch.Token;
    }

    /// <summary>Cancela y libera el <see cref="CancellationTokenSource"/> en curso.</summary>
    public void Dispose()
    {
        _currentSearch?.Cancel();
        _currentSearch?.Dispose();
        _currentSearch = null;
    }
}
