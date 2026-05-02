using AutoTest.Core.Outbox;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure.Outbox
{
    internal sealed class OutboxRow
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public DateTime OccurredAt { get; set; }
        public int Status { get; set; }
        public int Attempts { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime? LockedUntil { get; set; }
        public string? LockedBy { get; set; }
        public string? LastError { get; set; }
        public DateTime? SentAt { get; set; }

        public OutboxMessage ToModel()
        {
            return new OutboxMessage
            {
                Id = Guid.Parse(Id),
                Type = Type,
                PayloadJson = PayloadJson,
                OccurredAt = OccurredAt,
                Status = (OutboxStatus)Status,
                Attempts = Attempts,
                NextAttemptAt = NextAttemptAt,
                LockedUntil = LockedUntil,
                LockedBy = LockedBy,
                LastError = LastError,
                SentAt = SentAt
            };
        }
    }
}
