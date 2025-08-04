using System.ComponentModel;
using System.Globalization;

namespace JobScheduler.FunctionApp.Configuration;

/// <summary>
/// Custom type converter for converting string values to HttpMethod objects during configuration binding.
/// This enables configuration properties like "HttpMethod": "GET" to be properly bound to HttpMethod types.
/// </summary>
public class HttpMethodTypeConverter : TypeConverter
{
    /// <summary>
    /// Returns true if the source type is string, indicating we can convert from string to HttpMethod.
    /// </summary>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <summary>
    /// Converts a string value to the corresponding HttpMethod instance.
    /// </summary>
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
    /// Returns true if the destination type is string, indicating we can convert from HttpMethod to string.
    /// </summary>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    /// <summary>
    /// Converts an HttpMethod instance to its string representation.
    /// </summary>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is HttpMethod httpMethod)
        {
            return httpMethod.Method;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
