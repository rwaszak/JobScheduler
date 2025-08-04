using System.Net;

namespace JobScheduler.FunctionApp.Tests.TestHelpers
{
    public class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        private readonly List<CapturedRequest> _requests = new();

        public IReadOnlyList<CapturedRequest> Requests => _requests.AsReadOnly();

        public void AddResponse(HttpStatusCode statusCode, string content = "")
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }

        public void AddResponse(HttpResponseMessage response)
        {
            _responses.Enqueue(response);
        }

        public void AddException(Exception exception)
        {
            _responses.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent($"Exception: {exception.Message}")
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            // Capture the request details before they get disposed
            var capturedRequest = new CapturedRequest
            {
                Method = request.Method,
                RequestUri = request.RequestUri,
                Headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                Content = request.Content != null ? await request.Content.ReadAsStringAsync() : null,
                Authorization = request.Headers.Authorization
            };
            
            _requests.Add(capturedRequest);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No more responses configured for TestHttpMessageHandler");
            }

            var response = _responses.Dequeue();
            
            // Simulate network delay
            await Task.Delay(10, cancellationToken);
            
            return response;
        }

        public HttpRequestMessage GetRequest(int index = 0)
        {
            if (index >= _requests.Count)
                throw new ArgumentOutOfRangeException($"Request at index {index} not found. Total requests: {_requests.Count}");
            
            var captured = _requests[index];
            var request = new HttpRequestMessage(captured.Method, captured.RequestUri);
            
            if (captured.Content != null)
            {
                request.Content = new StringContent(captured.Content);
            }
            
            // Reconstruct headers
            foreach (var header in captured.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            // Reconstruct Authorization header
            if (captured.Authorization != null)
            {
                request.Headers.Authorization = captured.Authorization;
            }
            
            return request;
        }

        public CapturedRequest GetCapturedRequest(int index = 0)
        {
            if (index >= _requests.Count)
                throw new ArgumentOutOfRangeException($"Request at index {index} not found. Total requests: {_requests.Count}");
            
            return _requests[index];
        }

        public void Reset()
        {
            _requests.Clear();
            while (_responses.Count > 0)
            {
                _responses.Dequeue()?.Dispose();
            }
        }
    }

    public class CapturedRequest
    {
        public HttpMethod Method { get; set; } = HttpMethod.Get;
        public Uri? RequestUri { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? Content { get; set; }
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; set; }
    }
}
