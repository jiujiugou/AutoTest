using AutoTest.Core.Target.Http;

namespace AutoTest.Execution.Http
{
    record HttpClientKey(
    bool AllowAutoRedirect,
    bool IgnoreSslErrors,
    string? ProxyUrl,
    AuthType? AuthType,
    string? AuthToken,
    string? AuthUsername,
    string? AuthPassword
);
}
