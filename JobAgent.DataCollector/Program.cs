using JobAgent.DataCollector.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Warning);
});

builder.ConfigureServices((context, services) =>
{
    services.AddHttpClient();
    services.AddScoped<DataCollectorOrchestrator>();
});

var host = builder.Build();

Console.WriteLine("Uruchamianie pełnego skanowania...\n");

using (var scope = host.Services.CreateScope())
{
    var orchestrator = scope.ServiceProvider
        .GetRequiredService<DataCollectorOrchestrator>();

    await orchestrator.RunTestCollection();
}

Console.WriteLine("\nNaciśnij dowolny klawisz aby zakończyć...");
Console.ReadKey();