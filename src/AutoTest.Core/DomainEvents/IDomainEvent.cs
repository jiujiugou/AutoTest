namespace AutoTest.Core.DomainEvents;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
