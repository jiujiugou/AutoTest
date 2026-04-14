using AutoTest.Core.Target.Http;
using Flurl.Http;

namespace AutoTest.Execution.Http
{
    public interface IHttpClient
    {
        Task<FlurlClient> GetOrCreateClient(HttpTarget target);
    }
}
