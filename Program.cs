using Azure_Backup_Snapshots_Cleaner;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, builder) => { 
        builder.AddJsonFile(Path.Combine(context.HostingEnvironment.ContentRootPath, "filters.json"), optional: false, reloadOnChange: true);
        builder.AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddOptions<SnapshotFilterOptions>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("SnapshotFilters").Bind(settings);
            });
    })
    // .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
