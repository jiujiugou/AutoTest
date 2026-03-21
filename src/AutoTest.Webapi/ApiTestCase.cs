namespace AutoTest.Webapi
{
    public class ApiTestCase
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string Method { get; set; } = "GET";
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? Body { get; set; }
        public Func<HttpResponseMessage, bool>? Assertion { get; set; }
    }
}
