using AutoTest.Core;

namespace AutoTest.Application;

public interface ITargetMap
{
    string Type { get; }
    MonitorTarget Map(string json);
}
