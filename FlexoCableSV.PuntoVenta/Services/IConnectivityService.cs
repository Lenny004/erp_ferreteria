namespace FlexoCableSV.PuntoVenta.Services;

public interface IConnectivityService
{
    Task<ConnectivityStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed record ConnectivityStatus(bool IsOnline, string Message);
