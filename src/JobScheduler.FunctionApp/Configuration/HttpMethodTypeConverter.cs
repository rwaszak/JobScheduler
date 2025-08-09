using System.ComponentModel;
using System.Globalization;

namespace JobScheduler.FunctionApp.Configuration;

/// <summary>
/// Custom type converter for converting string values to HttpMethod objects during configuration binding.
/// This enables configuration properties like "HttpMethod": "GET" to be properly bound to HttpMethod types
/// in classes like JobDefinition and JobConfig.
/// 
/// The converter must be registered via TypeConverterExtensions.AddHttpMethodTypeConverter() 
/// for the .NET configuration system to use it automatically.
/// 
/// Supported HTTP methods: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS, TRACE (case-insensitive).
/// </summary>
public class HttpMethodTypeConverter : TypeConverter
{
    /// <summary>
    /// Determines if we can convert FROM the given source type TO HttpMethod.
    /// Returns true for string types, allowing the configuration system to convert 
    /// string values like "GET" or "POST" from appsettings.json to HttpMethod objects.
    /// </summary>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <summary>
    /// Converts a string value to the corresponding HttpMethod instance.
    /// Called automatically by the .NET configuration binding system when encountering HttpMethod properties.
    /// The conversion is case-insensitive (e.g., "get", "GET", or "Get" all work).
    /// </summary>
    /// <param name="context">Descriptor context (unused)</param>
    /// <param name="culture">Culture info for conversion (unused)</param>
    /// <param name="value">The string value to convert (e.g., "GET")</param>
    /// <returns>The corresponding HttpMethod instance</returns>
    /// <exception cref="ArgumentException">Thrown when an unsupported HTTP method string is provided</exception>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string stringValue)
        {
            return stringValue.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                "HEAD" => HttpMethod.Head,
                "OPTIONS" => HttpMethod.Options,
                "TRACE" => HttpMethod.Trace,
                _ => throw new ArgumentException($"Unknown HTTP method: {stringValue}", nameof(value))
            };
        }

        return base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// Determines if we can convert FROM HttpMethod TO the given destination type.
    /// Returns true for string types, enabling serialization of HttpMethod objects back to strings
    /// (useful for logging, debugging, or reverse conversion scenarios).
    /// </summary>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    /// <summary>
    /// Converts an HttpMethod instance to its string representation.
    /// Returns the HTTP method name (e.g., HttpMethod.Get becomes "GET").
    /// Primarily used for serialization, logging, or debugging purposes.
    /// </summary>
    /// <param name="context">Descriptor context (unused)</param>
    /// <param name="culture">Culture info for conversion (unused)</param>
    /// <param name="value">The HttpMethod instance to convert</param>
    /// <param name="destinationType">The target type (should be string)</param>
    /// <returns>The string representation of the HTTP method</returns>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is HttpMethod httpMethod)
        {
            return httpMethod.Method;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
