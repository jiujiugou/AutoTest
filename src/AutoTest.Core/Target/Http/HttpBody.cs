namespace AutoTest.Core.Target.Http;

public class HttpBody
{
    public BodyType Type { get; private set; }

    public string ContentType { get; private set; }
    public object? Value { get; private set; }

    public HttpBody(BodyType type, string contentType, object? value = null)
    {
        Type = type;
        ContentType = contentType;
        Value = value;
    }
}
public enum BodyType
{
    Json,
    FormUrlEncoded,
    Raw
}
