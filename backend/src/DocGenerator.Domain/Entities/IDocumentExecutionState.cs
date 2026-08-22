namespace DocGenerator.Domain.Entities;

/// <summary>
/// الحقول الخام التي تُشتق منها حالة العرض الموحدة — يحققها كيان Document
/// وDocumentResponse معًا فتعمل قواعد الاستنتاج على الطرفين دون تكرار.
/// </summary>
public interface IDocumentExecutionState
{
    string? GeneralEntitySide { get; }
    string? ExecutedStatus { get; }
    string? ExecStatus { get; }
    string? ExecSubStatus { get; }
    bool IsDraft { get; }
}
