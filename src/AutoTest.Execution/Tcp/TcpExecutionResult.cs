using AutoTest.Core;
using AutoTest.Core.Execution;

namespace AutoTest.Execution.Tcp;

public class TcpExecutionResult : ExecutionResult, ITcpExecutionResult
{
    public bool Connected { get; }
    public string Response { get; }
    public double LatencyMs { get; }
    public double ConnectLatencyMs { get; }
    public List<string> Responses { get; }
    public bool SequenceCorrect { get; }

    public TcpExecutionResult(
        bool connected,
        string response,
        double latencyMs,
        double connectLatencyMs,
        bool success,
        List<string> responses,
        string message)
        : base(success, message)
    {
        Connected = connected;
        Response = response;
        LatencyMs = latencyMs;
        ConnectLatencyMs = connectLatencyMs;
        Responses = responses;
        SequenceCorrect = true;
    }
}
