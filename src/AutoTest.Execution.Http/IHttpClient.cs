using AutoTest.Core.Target.Http;
using Flurl.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Execution.Http
{
    public interface IHttpClient
    {
        Task<FlurlClient> GetOrCreateClient(HttpTarget target);
    }
}
