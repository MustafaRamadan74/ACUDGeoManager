namespace UtilityNetwork.Core.Exceptions;

public sealed class OptimisticConcurrencyException : Exception
{
    public Guid EntityId { get; }
    public int ExpectedVersion { get; }

    public OptimisticConcurrencyException(Guid entityId, int expectedVersion)
        : base($"The entity with ID '{entityId}' was modified or deleted by another operation. Expected version: {expectedVersion}.")
    {
        EntityId = entityId;
        ExpectedVersion = expectedVersion;
    }
}
