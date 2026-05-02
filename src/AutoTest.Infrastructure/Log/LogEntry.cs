using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure.Log
{
    public class LogEntry
    {
        public string TraceId { get; set; } = "";
        public DateTimeOffset Timestamp { get; set; }

        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
    }

}
