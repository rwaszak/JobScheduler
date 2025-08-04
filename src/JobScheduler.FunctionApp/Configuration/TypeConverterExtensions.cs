using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.FunctionApp.Configuration;

/// <summary>
/// Extension methods for configuring custom type converters for configuration binding.
/// </summary>
public static class TypeConverterExtensions
{
    /// <summary>
    /// Registers the HttpMethodTypeConverter to enable automatic conversion of string values 
    /// to HttpMethod objects during configuration binding.
    /// This must be called before any configuration binding that involves HttpMethod properties.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHttpMethodTypeConverter(this IServiceCollection services)
    {
        // Register the type converter globally for HttpMethod type
        TypeDescriptor.AddAttributes(typeof(HttpMethod), new TypeConverterAttribute(typeof(HttpMethodTypeConverter)));
        
        return services;
    }
}
