using JobScheduler.FunctionApp.Configuration;
using JobScheduler.FunctionApp.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Test script to demonstrate fail-fast validation
var configuration = new ConfigurationBuilder()
    .AddJsonFile("test-config-binding.json")
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.AddHttpMethodTypeConverter();
services.Configure<JobSchedulerOptions>(configuration.GetSection(JobSchedulerOptions.SectionName));
services.AddSingleton<IValidateOptions<JobSchedulerOptions>, ValidateJobSchedulerOptions>();

var serviceProvider = services.BuildServiceProvider();

try
{
    var options = serviceProvider.GetRequiredService<IOptions<JobSchedulerOptions>>().Value;
    Console.WriteLine("ERROR: Validation should have failed!");
}
catch (OptionsValidationException ex)
{
    Console.WriteLine("SUCCESS: Fail-fast validation caught the error!");
    Console.WriteLine($"Error: {ex.Message}");
}
