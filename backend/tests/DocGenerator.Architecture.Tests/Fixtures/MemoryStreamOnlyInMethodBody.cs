namespace DocGenerator.Architecture.Tests.Fixtures;

/// <summary>
/// عيّنة اصطناعية تثبت أن محرك <c>NetArchTest</c> يرصد الاعتماديات الموجودة داخل
/// أجسام الدوال (IL) لا في التوقيعات فقط.
/// <para>
/// النوع لا يحمل أي حقل/خاصية/معامل من نوع <see cref="System.IO.MemoryStream"/>؛
/// الاعتماد موجود في جسم <see cref="Describe"/> وحده. يُستخدم في
/// <c>ArchitectureRuleEngineTests</c> كتحكم إيجابي دائم: لو توقفت المكتبة عن فحص
/// الأجسام بعد ترقية، يفشل الحارس بدل أن يمرّ على الفراغ.
/// </para>
/// </summary>
internal sealed class MemoryStreamOnlyInMethodBody
{
    internal static string Describe()
    {
        using var stream = new System.IO.MemoryStream();
        return stream.GetType().FullName ?? string.Empty;
    }
}
