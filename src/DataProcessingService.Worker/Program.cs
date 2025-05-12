using DataProcessingService.Infrastructure;
using DataProcessingService.Worker.Workers;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Add infrastructure services
        services.AddInfrastructure(hostContext.Configuration);
        
        // Register worker services
        services.AddHostedService<DataProcessingWorker>();
    })
    .Build();

await host.RunAsync();
