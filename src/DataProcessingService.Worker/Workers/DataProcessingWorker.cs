using DataProcessingService.Core.Interfaces.Services.ETL;

namespace DataProcessingService.Worker.Workers;

public class DataProcessingWorker : BackgroundService
{
    private readonly ILogger<DataProcessingWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DataProcessingWorker(
        ILogger<DataProcessingWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataProcessingWorker starting at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Use a scope to resolve scoped services
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Example of resolving a service
                    var dataExtractionService = scope.ServiceProvider.GetRequiredService<IDataExtractionService>();
                    
                    // Perform background processing work here
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                
                // Wait for a period before running again
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred in DataProcessingWorker");
                
                // Wait a shorter time before retrying after an error
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        
        _logger.LogInformation("DataProcessingWorker stopping at: {time}", DateTimeOffset.Now);
    }
}
