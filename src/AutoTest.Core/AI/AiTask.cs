using System;

namespace AutoTest.Core.AI
{
    public class AiTask
    {
        public Guid Id { get; set; }

        public string TaskType { get; set; } = null!;

        /// <summary>
        /// 业务归属 ID（OutboxMessageId 或 ExecutionRecordId，按场景使用）
        /// </summary>
        public Guid BizId { get; set; }

        public string InputJson { get; set; } = null!;

        public string? OutputJson { get; set; }

        public int Attempts { get; set; }

        public string Status { get; set; } = null!; // Pending / Processing / Success / Failed / DeadLetter

        public DateTime NextRunAt { get; set; }

        public string? LockedBy { get; set; }

        public DateTime? LockedAt { get; set; }

        public string? Error { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
