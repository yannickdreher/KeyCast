using KeyCast.App;
using KeyCast.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

ApplicationConfiguration.Initialize();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));

builder.Services.AddSingleton<MainForm>();
builder.Services.AddSingleton<TcpListenerService>();
builder.Services.AddSingleton<KeyboardHookService>();
builder.Services.AddHostedService<Worker>();


var host = builder.Build();

await host.StartAsync();

var mainForm = host.Services.GetRequiredService<MainForm>();
Application.Run(mainForm);

await host.StopAsync();
