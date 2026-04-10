namespace MaterialClient.Common.Events;

/// <summary>
///     Detail operation type enum for distinguishing operation types in DetailOperationCompletedMessage.
/// </summary>
public enum DetailOperationType
{
    Save,
    Abolish,
    Match,
    Complete
}
