using AutoTest.Core.Target.Http;
using Flurl.Http;
using MySqlX.XDevAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace AutoTest.Execution.Http
{
    internal class FlurlHttpClient:IHttpClient
    {
        private readonly ConcurrentDictionary<HttpClientKey, FlurlClient> _handlers = new();

        public Task<FlurlClient> GetOrCreateClient(HttpTarget target)
        {
            var key = new HttpClientKey(
                    target.AllowAutoRedirect ?? false,
                    target.IgnoreSslErrors ?? false,
                    target.ProxyUrl,
                    target.AuthType,
                    target.AuthToken,
                    target.AuthUsername,
                    target.AuthPassword
                );
            var client = _handlers.GetOrAdd(key, _ =>
            {
                // 1. 创建 Handler
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = target.AllowAutoRedirect ?? false,
                    MaxAutomaticRedirections = target.MaxRedirects,
                    UseCookies = target.UseCookies ?? false,
                    ServerCertificateCustomValidationCallback = (target.IgnoreSslErrors ?? false)
                        ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        : null
                };

                // 2. 配置代理
                if (!string.IsNullOrEmpty(target.ProxyUrl))
                {
                    handler.Proxy = new WebProxy(target.ProxyUrl)
                    {
                        Credentials = !string.IsNullOrEmpty(target.ProxyUser)
                            ? new NetworkCredential(target.ProxyUser, target.ProxyPass)
                            : null
                    };
                    handler.UseProxy = true;
                }

                // 3. 创建 HttpClient + FlurlClient
                var httpClient = new HttpClient(handler);
                var flurlClient = new FlurlClient(httpClient);

                // 4. 配置认证 Header
                switch (target.AuthType)
                {
                    case AuthType.Bearer:
                        if (!string.IsNullOrEmpty(target.AuthToken))
                            flurlClient.WithOAuthBearerToken(target.AuthToken);
                        break;
                    case AuthType.Basic:
                        if (!string.IsNullOrEmpty(target.AuthUsername) && !string.IsNullOrEmpty(target.AuthPassword))
                        {
                            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{target.AuthUsername}:{target.AuthPassword}"));
                            flurlClient.WithHeader("Authorization", $"Basic {auth}");
                        }
                        break;
                    case AuthType.ApiKeyHeader:
                        if (!string.IsNullOrEmpty(target.AuthToken))
                            flurlClient.WithHeader("X-Api-Key", target.AuthToken);
                        break;
                }

                return flurlClient;
            });

            return Task.FromResult(client);
        }
    }
}
