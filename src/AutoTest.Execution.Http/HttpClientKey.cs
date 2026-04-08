using AutoTest.Core.Target.Http;
using System;
using System.Collections.Generic;
using System.Text;

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
