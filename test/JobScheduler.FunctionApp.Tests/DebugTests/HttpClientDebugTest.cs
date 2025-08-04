using System.Net;
using Xunit;
using FluentAssertions;
using JobScheduler.FunctionApp.Tests.TestHelpers;

namespace JobScheduler.FunctionApp.Tests.DebugTests;

public class HttpClientDebugTest
{
    [Fact]
    public async Task TestHttpMessageHandler_InternalServerError_ShouldThrowHttpRequestException()
    {
        // Arrange
        var handler = new TestHttpMessageHandler();
        handler.AddResponse(HttpStatusCode.InternalServerError, "Server Error");
        
        var httpClient = new HttpClient(handler);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            var response = await httpClient.PostAsync("https://test.com/api", null);
            response.EnsureSuccessStatusCode(); // This should throw
        });
        
        exception.Should().NotBeNull();
        handler.Requests.Should().HaveCount(1);
    }
}
