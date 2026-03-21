using System.Text;

namespace AutoTest.Webapi
{
    public class ApiTestRunner
    {
        private readonly HttpClient _httpClient;
        public ApiTestRunner(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<(bool Success, string Message)> Run(ApiTestCase testCase)
        {
            var request = new HttpRequestMessage(new HttpMethod(testCase.Method), testCase.Url);
            if (testCase.Body != null)
                request.Content = new StringContent(testCase.Body, Encoding.UTF8, "application/json");

            foreach (var h in testCase.Headers)
                request.Headers.Add(h.Key, h.Value);

            var response = await _httpClient.SendAsync(request);
            bool success = testCase.Assertion?.Invoke(response) ?? true;

            string content = await response.Content.ReadAsStringAsync();
            return (success, content);
        }
    }
}
