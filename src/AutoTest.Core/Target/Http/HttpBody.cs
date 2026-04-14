namespace AutoTest.Core.Target.Http;

/// <summary>
/// HTTP 请求体描述。
/// </summary>
public class HttpBody
{
    /// <summary>
    /// 请求体类型（JSON/Form/Raw）。
    /// </summary>
    public BodyType Type { get; private set; }

    /// <summary>
    /// 请求体的 Content-Type。
    /// </summary>
    public string ContentType { get; private set; }

    /// <summary>
    /// 请求体内容（与 <see cref="Type"/> 对应）。
    /// </summary>
    public object? Value { get; private set; }

    /// <summary>
    /// 创建 HTTP 请求体。
    /// </summary>
    public HttpBody(BodyType type, string contentType, object? value = null)
    {
        Type = type;
        ContentType = contentType;
        Value = value;
    }
}

/// <summary>
/// HTTP 请求体类型。
/// </summary>
public enum BodyType
{
    Json,
    FormUrlEncoded,
    Raw
}
