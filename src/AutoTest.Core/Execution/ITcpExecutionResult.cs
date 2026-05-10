namespace AutoTest.Core.Execution;

public interface ITcpExecutionResult
{
    bool Connected { get; }
    string Response { get; }
    double LatencyMs { get; }
    double ConnectLatencyMs { get; }
    List<string> Responses { get; }
    bool SequenceCorrect { get; }
}
