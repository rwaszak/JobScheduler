using System.Text.Json;
using JobScheduler.FunctionApp.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JobScheduler.FunctionApp.Functions
{
    public class ManualTriggerFunctions
    {
        private readonly IJobExecutor _jobExecutor;
        private readonly IJobConfigurationProvider _configProvider;
        private readonly ILogger<ManualTriggerFunctions> _logger;

        public ManualTriggerFunctions(
            IJobExecutor jobExecutor,
            IJobConfigurationProvider configProvider,
            ILogger<ManualTriggerFunctions> logger)
        {
            _jobExecutor = jobExecutor;
            _configProvider = configProvider;
            _logger = logger;
        }

        // Manual job trigger: POST /api/jobs/{jobName}/trigger
        [Function("TriggerJob")]
        public async Task<HttpResponseData> TriggerJob(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "jobs/{jobName}/trigger")]
            HttpRequestData req,
            string jobName)
        {
            try
            {
                _logger.LogInformation("Manual trigger requested for job: {JobName}", jobName);

                var config = _configProvider.GetJobConfig(jobName);
                var result = await _jobExecutor.ExecuteAsync(config);

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");

                var responseData = new
                {
                    JobName = jobName,
                    Success = result.IsSuccess,
                    Status = result.Status,
                    Duration = result.Duration.TotalMilliseconds,
                    AttemptCount = result.AttemptCount,
                    Message = result.IsSuccess ? "Job completed successfully" : result.ErrorMessage,
                    Timestamp = DateTime.UtcNow
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(responseData, new JsonSerializerOptions { WriteIndented = true }));
                return response;
            }
            catch (ArgumentException)
            {
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await errorResponse.WriteStringAsync($"Job '{jobName}' not found");
                return errorResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering job {JobName}", jobName);

                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error executing job: {ex.Message}");
                return errorResponse;
            }
        }

        // List all jobs: GET /api/jobs
        [Function("ListJobs")]
        public async Task<HttpResponseData> ListJobs(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "jobs")]
            HttpRequestData req)
        {
            var jobs = _configProvider.GetAllJobConfigs()
                .Select(config => new
                {
                    config.JobName,
                    config.Endpoint,
                    config.HttpMethod,
                    config.Tags,
                    HasAuth = !string.IsNullOrEmpty(config.AuthSecretName)
                })
                .OrderBy(j => j.JobName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var responseData = new
            {
                Jobs = jobs,
                Count = jobs.Count(),
                Timestamp = DateTime.UtcNow
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseData, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }

        // Health check endpoint: GET /api/health
        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
            HttpRequestData req)
        {
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            try 
            {
                var jobCount = _configProvider.GetAllJobConfigs().Count();
                var healthData = new
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    JobCount = jobCount,
                    Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "unknown"
                };

                await response.WriteStringAsync(JsonSerializer.Serialize(healthData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                response.Headers.Add("Content-Type", "application/json");
                
                var errorData = new
                {
                    Status = "Unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message,
                    Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "unknown"
                };
                
                await response.WriteStringAsync(JsonSerializer.Serialize(errorData, new JsonSerializerOptions { WriteIndented = true }));
            }
            
            return response;
        }
    }
}