namespace DocGenerator.Domain.Enums;

/// <summary>
/// جمهور التنبيه النظامي — يحدّد نطاق الإشعار وشكل مفتاح منع التكرار (<c>DedupeKey</c>)
/// وشكل <c>TargetType</c> المخزَّن. لا يُشتق الجمهور من <c>TargetType</c>؛ العكس صحيح.
/// </summary>
public enum HeadAlertAudience
{
    /// <summary>كل المحامين المفعّلين في الفرع — صف واحد لكل (حدث، فرع، جمهور).</summary>
    BranchLawyers,

    /// <summary>كل رؤساء الأقسام والشعب المفعّلون في الفرع — صف واحد لكل (حدث، فرع، جمهور).</summary>
    BranchHeads,

    /// <summary>محامٍ واحد محدَّد.</summary>
    SingleLawyer,

    /// <summary>رئيس قسم/شعبة واحد محدَّد.</summary>
    SingleHead,

    /// <summary>منشئ الملف (يُشتق من <c>DocumentId</c> أو من <c>RecipientUserId</c> صراحةً).</summary>
    DocumentCreator
}
