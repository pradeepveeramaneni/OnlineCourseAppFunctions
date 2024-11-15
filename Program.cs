using LSC.OnlineCourse.Functions.Email;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

//var builder = FunctionsApplication.CreateBuilder(args);

//builder.ConfigureFunctionsWebApplication();

////Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

//builder.Build().Run();


var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
              .AddEnvironmentVariables();
    })
     .ConfigureServices(services =>
     {
         services.AddHttpClient(); // Register HttpClient
         services.AddApplicationInsightsTelemetryWorkerService();
         services.ConfigureFunctionsApplicationInsights();
         services.AddScoped<IEmailNotification, EmailNotification>();

     })
     .ConfigureLogging(logging =>
     {
         logging.AddConsole();
     })
    .Build();

host.Run();
