using System.Data;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Outbox;

namespace AutoTest.Cli;

internal class NullOutboxRepository : IOutboxRepository
{
    public Task AddAsync(OutboxMessage message, IDbTransaction? tx = null) => Task.CompletedTask;
    public Task<IReadOnlyList<OutboxMessage>> LockNextBatchAsync(int take, TimeSpan lockDuration, string lockedBy, DateTime utcNow, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OutboxMessage>>(Array.Empty<OutboxMessage>());
    public Task MarkSentAsync(Guid id, string lockedBy, DateTime sentAt, CancellationToken ct) => Task.CompletedTask;
    public Task MarkFailedAsync(Guid id, string lockedBy, string error, DateTime nextAttemptAt, CancellationToken ct) => Task.CompletedTask;
    public Task MarkDeadLetterAsync(Guid id, string lockedBy, string error, CancellationToken ct) => Task.CompletedTask;
    public Task<int> DeleteExpiredDeadLettersAsync(DateTime cutoff, CancellationToken ct) => Task.FromResult(0);
}
