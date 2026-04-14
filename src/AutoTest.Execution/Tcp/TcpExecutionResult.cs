using AutoTest.Core;
using AutoTest.Core.Execution;

namespace AutoTest.Execution.Tcp;

public class TcpExecutionResult : ExecutionResult, ITcpExecutionResult
{
    public TcpExecutionResult(bool connected, string response, double latencyMs, bool success, bool sequenceCorrect, string message = null!)
    : base(success, message)
    {
        Connected = connected;
        Response = response;
        LatencyMs = latencyMs;
        SequenceCorrect = sequenceCorrect;
    }

    public bool Connected { get; }
    public string Response { get; }
    public double LatencyMs { get; }

    public bool SequenceCorrect { get; init; }

}
