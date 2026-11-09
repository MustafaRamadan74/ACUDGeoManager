namespace UtilityNetwork.Core.Models;

public sealed record ChangeSetSummaryDto(
    Guid Id,
    Guid? UserId,
    string Description,
    string Status,
    DateTime CreatedAt,
    DateTime? UndoneAt,
    int ActionsCount,
    IReadOnlyList<string> AffectedFeatures);

public sealed record UndoRedoStackDto(
    bool CanUndo,
    bool CanRedo,
    IReadOnlyList<ChangeSetSummaryDto> UndoStack,
    IReadOnlyList<ChangeSetSummaryDto> RedoStack);

public sealed record UndoResultDto(
    Guid ChangeSetId,
    string Description,
    int RevertedActionsCount,
    string Status);

public sealed record RedoResultDto(
    Guid ChangeSetId,
    string Description,
    int ReappliedActionsCount,
    string Status);

public sealed record ConflictingFeatureDto(
    string TableName,
    Guid RecordId,
    string FeatureName,
    int ExpectedVersion,
    int? CurrentVersion,
    string Reason);

public sealed class UndoRedoConflictException : Exception
{
    public IReadOnlyList<ConflictingFeatureDto> Conflicts { get; }

    public UndoRedoConflictException(IReadOnlyList<ConflictingFeatureDto> conflicts)
        : base("One or more features have conflicts and cannot be undone or redone.")
    {
        Conflicts = conflicts;
    }
}

