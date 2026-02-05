using KeyCast.App.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeyCast.App;

public class Worker(
    ILogger<Worker> logger,
    KeyboardHookService keyboardHookService,
    TcpListenerService tcpListener) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private readonly KeyboardHookService _keyboardHookService = keyboardHookService;
    private readonly TcpListenerService _tcpListener = tcpListener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Keyboard-to-TCP Bridge Service starting...");

        // Start TCP Listener
        if (!_tcpListener.TryStart())
        {
            _logger.LogError("Could not start TCP Listener. Service will terminate.");
            return;
        }

        // Connect event handler
        _keyboardHookService.KeyPressed += (sender, key) =>
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Key received: {Key}", key);
            }
            _ = _tcpListener.WriteAsync(key.ToString(), CancellationToken.None);
        };

        // Start keyboard hook
        _keyboardHookService.Start();

        // Start TCP client acceptance in background
        var acceptClientsTask = _tcpListener.AcceptClientsAsync(stoppingToken);

        try
        {
            // Wait until service is stopped
            await acceptClientsTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Service is shutting down...");
        }
        finally
        {
            _keyboardHookService.Stop();
            _tcpListener.Dispose();
        }
    }
}
