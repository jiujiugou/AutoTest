using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure.Outbox
{
    public sealed class WebhookOptions
    {
        public bool Enabled { get; set; }

        public string Url { get; set; } = "";

        public int TimeoutSeconds { get; set; } = 1000;
    }
}
