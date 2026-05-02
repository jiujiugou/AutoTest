using System.Threading;
using System.Threading.Tasks;

namespace AutoTest.Core.AI
{
    public interface IAiClient
    {
        Task<string> AnalyzeAsync(string inputJson, CancellationToken cancellationToken = default);
    }
}
