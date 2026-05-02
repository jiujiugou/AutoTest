using AutoTest.Application.Dto;
using AutoTest.Core.Assertion;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Application
{
    public class AiMonitorPatch
    {
        public string? Name { get; set; }

        public string? TargetType { get; set; }

        public string? TargetConfig { get; set; }

        public List<AssertionDto>? Assertions { get; set; }

        public string? Reason { get; set; }
    }
}
