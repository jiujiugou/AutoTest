using System.Text.Json.Serialization;

namespace AutoTest.Core.http;

/// <summary>
/// HTTP 请求方法。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RequestMethod
{
    Get,
    Post,
    Put,
    Delete
}
