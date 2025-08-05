using System;

// Simple test to check the ConvertToAzureFriendlyName method
public class TestNaming
{
    public static void Main()
    {
        Console.WriteLine($"container_app_health -> {ConvertToAzureFriendlyName("container_app_health")}");
        Console.WriteLine($"daily_batch_job -> {ConvertToAzureFriendlyName("daily_batch_job")}");
        Console.WriteLine($"container-app-health -> {ConvertToAzureFriendlyName("container-app-health")}");
        Console.WriteLine($"daily-batch -> {ConvertToAzureFriendlyName("daily-batch")}");
    }
    
    private static string ConvertToAzureFriendlyName(string jobName)
    {
        var parts = jobName.Split('-', '_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return jobName;
        
        var result = parts[0].ToLowerInvariant();
        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                result += char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
            }
        }
        return result;
    }
}
