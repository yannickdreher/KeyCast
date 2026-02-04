using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KeyCast.Service.Services;

public class TcpListenerService(ILogger<TcpListenerService> logger, IOptions<Settings> settings) : IDisposable
{
    private readonly ILogger<TcpListenerService> _logger = logger;
    private readonly Settings _settings = settings.Value;
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private CancellationTokenSource? _acceptCancellation;

    // Events for UI
    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public int Port => _settings.TcpListenerPort;

    public bool TryStart()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _settings.TcpListenerPort);
            _listener.Start();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("TCP Listener successfully started on localhost:{Port}", 
                    _settings.TcpListenerPort);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error starting TCP Listener on localhost:{Port}", 
                    _settings.TcpListenerPort);
            }
            return false;
        }
    }

    public async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        if (_listener == null)
        {
            throw new InvalidOperationException("TCP Listener is not started");
        }

        _acceptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            while (!_acceptCancellation.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_acceptCancellation.Token);
                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
                
                _clients.TryAdd(endpoint, client);
                ClientConnected?.Invoke(this, endpoint);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("New client connected: {ClientEndpoint}", endpoint);
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("TCP client acceptance was cancelled");
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error accepting TCP clients");
            }
        }
    }

    public async Task WriteAsync(string data, CancellationToken cancellationToken)
    {
        if (_listener == null) return;
        if (_clients.IsEmpty) return;

        var asciiData = ConvertToAsciiWithDelimiter(data);
        var bytes = Encoding.ASCII.GetBytes(asciiData);
        var disconnectedClients = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            _clients,
            new ParallelOptions 
            { 
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2
            },
            async (kvp, ct) =>
            {
                var (endpoint, client) = kvp;
                try
                {
                    if (client.Connected)
                    {
                        await client.GetStream().WriteAsync(bytes, ct);
                        await client.GetStream().FlushAsync(ct);
                    }
                    else
                    {
                        disconnectedClients.Add(endpoint);
                    }
                }
                catch (Exception)
                {
                    disconnectedClients.Add(endpoint);
                }
            });

        // Remove disconnected clients
        foreach (var endpoint in disconnectedClients)
        {
            if (_clients.TryRemove(endpoint, out var client))
            {
                client.Dispose();
                ClientDisconnected?.Invoke(this, endpoint);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Client {ClientEndpoint} removed", endpoint);
                }
            }
        }
    }

    private static string ConvertToAsciiWithDelimiter(string data)
    {
        var sb = new StringBuilder(data.Length * 4);
        foreach (char c in data)
        {
            sb.Append((int)c); 
            sb.Append('\n'); 
        }
        return sb.ToString();
    }

    public void Dispose()
    {
        _acceptCancellation?.Cancel();
        
        foreach (var kvp in _clients)
        {
            try
            {
                kvp.Value.Close();
                kvp.Value.Dispose();
            }
            catch { }
        }
        
        _clients.Clear();
        _listener?.Stop();
        GC.SuppressFinalize(this);
    }
}