namespace AutoTest.Application.Dto;

public class TcpTargetDto
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public int Timeout { get; set; }
    public List<string> Messages { get; set; } = new List<string>();
}
