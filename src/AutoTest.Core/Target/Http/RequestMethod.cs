using System.Text.Json.Serialization;

namespace AutoTest.Core.http;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RequestMethod
{
    Get,
    Post,
    Put,
    Delete
}
