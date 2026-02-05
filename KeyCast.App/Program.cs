using KeyCast.App;
using KeyCast.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// WinForms setup
ApplicationConfiguration.Initialize();

var builder = Host.CreateApplicationBuilder(args);

// Register Services
builder.Services.AddSingleton<MainForm>();
builder.Services.AddSingleton<TcpListenerService>();
builder.Services.AddSingleton<KeyboardHookService>();
builder.Services.AddHostedService<Worker>();

// Configure Settings (Manual binding simplified for context)
builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));

var host = builder.Build();

// Start Background Services (Worker, etc.) without blocking
await host.StartAsync();

// Run UI
var mainForm = host.Services.GetRequiredService<MainForm>();
Application.Run(mainForm);

// Cleanup when UI exits
await host.StopAsync();
