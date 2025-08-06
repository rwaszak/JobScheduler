namespace JobScheduler.FunctionApp.Tests.TestHelpers;

/// <summary>
/// Test-specific job name constants that are independent of the production JobNames.
/// These should be used exclusively in tests to avoid coupling with production job definitions.
/// </summary>
public static class TestJobNames
{
    /// <summary>
    /// Test job for basic HTTP health check scenarios
    /// </summary>
    public const string TestHealthCheck = "testHealthCheck";

    /// <summary>
    /// Test job for authentication scenarios
    /// </summary>
    public const string TestAuthJob = "testAuthJob";

    /// <summary>
    /// Test job for error handling scenarios
    /// </summary>
    public const string TestErrorJob = "testErrorJob";

    /// <summary>
    /// Test job for timeout scenarios
    /// </summary>
    public const string TestTimeoutJob = "testTimeoutJob";

    /// <summary>
    /// All test job names for validation testing
    /// </summary>
    public static readonly string[] AllTestJobs = 
    {
        TestHealthCheck,
        TestAuthJob, 
        TestErrorJob,
        TestTimeoutJob
    };
}
