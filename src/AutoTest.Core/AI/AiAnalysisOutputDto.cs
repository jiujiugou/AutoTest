using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoTest.Core.AI
{
    public class AiAnalysisOutputDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Unknown";

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "low";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("rootCause")]
        public string RootCause { get; set; } = "";

        [JsonPropertyName("suggestion")]
        public string Suggestion { get; set; } = "";

        [JsonPropertyName("impact")]
        public string Impact { get; set; } = "single_request";

        [JsonPropertyName("faultService")]
        public string? FaultService { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("errorChain")]
        public List<ErrorChainEntry>? ErrorChain { get; set; }
    }

    public class ErrorChainEntry
    {
        [JsonPropertyName("service")]
        public string? Service { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = "";
    }
}
