using AutoTest.Core;

namespace AutoTest.Application;

public interface ITargetBuilder
{
    string Type { get; }
    MonitorTarget Build(string json);
}
