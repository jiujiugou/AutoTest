using Microsoft.AspNetCore.Mvc.Testing;
using AutoTest.Webapi;
using Microsoft.VisualBasic;
using Xunit;
using AutoTest.Application.Dto;
namespace AutoTest.test;

public class TestAppFactory : WebApplicationFactory<Program>
{
    /*private readonly HttpClient client;
    public TestAppFactory(TestAppFactory factory)
    {
        client = factory.CreateClient();
    }
    [Fact]
    public async Full_Flow_Should_Work()
    {
        var dto = new MonitorDto()
        {
            Name = "Test Monitor",
            TargetType = "HTTP",
            TargetConfig = "{\"Url\":\"http://example.com\",\"Method\":\"GET\"}",
            IsEnabled = true,
            Assertions = new List<AssertionDto>
            {
                new AssertionDto()
                {
                    Type = "Http",
                    ConfigJson = "{\"ExpectedStatusCode\":200}"
                }
            }
        };
    }*/
}