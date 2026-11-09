namespace UtilityNetwork.Core.Exceptions;

public sealed class PendingApprovalRequiredException : Exception
{
    public Guid PendingChangeId { get; }
    public string Operation { get; }
    public string FeatureTypeCode { get; }

    public PendingApprovalRequiredException(
        Guid pendingChangeId,
        string operation,
        string featureTypeCode,
        string message) : base(message)
    {
        PendingChangeId = pendingChangeId;
        Operation = operation;
        FeatureTypeCode = featureTypeCode;
    }
}
