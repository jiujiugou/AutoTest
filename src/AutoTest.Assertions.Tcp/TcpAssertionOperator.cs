namespace AutoTest.Assertions.Tcp;

public enum TcpAssertionOperator
{
    Equal,      // 完全匹配，适合 Connected、Response
    Contains,   // 部分匹配，适合 Response
    LessThan,   // 小于，适合 LatencyMs
    GreaterThan // 大于，适合 LatencyMs
}
