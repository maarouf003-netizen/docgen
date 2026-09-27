using System.Text.Json;
using System.Text.Json.Serialization;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Common;

/// <summary>
/// مصدر موحّد لتسلسل لقطات أطراف الاستئناف (AppellantsJson / AppelleesJson) ومعالجتها.
/// يستخدم ترميزًا مرتاخًا (UnsafeRelaxedJsonEscaping) حتى تبقى الأسماء العربية نصًا
/// مقروءًا في العمود — يلزم لبحث «الاستئنافات» بالأسماء عبر Contains.
/// تشمل دالة تحديث اسم جهة عامة داخل اللقطات (تُستخدم عند إعادة تسمية / دمج / حلول
/// جهة عامة لتنعكس على لقطات الاستئنافات المرتبطة بملفاتها المتأثرة).
/// </summary>
public static class AppealSnapshotSerializer
{
    public static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>نوع الطرف المرجعي للجهة العامة في الطالب (applicant side).</summary>
    public const string KindApplicantEntity = "applicant-entity";

    /// <summary>نوع الطرف المرجعي للجهة العامة في المنفذ عليه (executed public).</summary>
    public const string KindExecutedPublic = "executed-public";

    /// <summary>نوع الطرف المرجعي لطالب التنفيذ الاعتباري (الجهة العامة) في وضع «منفذ عليه».</summary>
    public const string KindExecutionApplicant = "execution-applicant";

    /// <summary>فكِّ لقطة أطراف (مستأنفين أو مستأنف عليهم) إلى قائمة أطراف.</summary>
    public static List<AppealPartyDto> DeserializeParties(string? json)
        => TryDeserializeParties(json).Parties;

    /// <summary>
    /// فكٌّ مع وسم الجودة: تالف عندما يكون الحمل غير فارغ ولا يُنتج أطرافًا (JSON مكسور أو
    /// بنية غير قائمة) — يُعرض موسومًا لا قائمة فارغة نظيفة. الفارغ تطبيع لا تلف.
    /// </summary>
    public static (List<AppealPartyDto> Parties, bool IsCorrupted) TryDeserializeParties(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (new List<AppealPartyDto>(), false);
        try
        {
            return (JsonSerializer.Deserialize<List<AppealPartyDto>>(json, SnapshotJsonOptions)
                    ?? new List<AppealPartyDto>(), false);
        }
        catch (JsonException)
        {
            return (new List<AppealPartyDto>(), true);
        }
    }

    /// <summary>
    /// هل اللقطة تالفة (تستحق الوسم/التدقيق)؟ الفارغ و`"null"` النصية تطبيع لا تلف.
    /// المصدر الوحيد لتعريف التلف — تُبنى عليه المزامنة والوسم معًا فلا يتشعب التعريف.
    /// </summary>
    public static bool IsCorruptedSnapshot(string? json)
    {
        var trimmed = json?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)
            || string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
            return false;
        return TryDeserializeParties(json).IsCorrupted;
    }

    /// <summary>تسلسل قائمة الأطراف إلى نص اللقطة (بذات الخيارات).</summary>
    public static string SerializeParties(IEnumerable<AppealPartyDto> parties)
        => JsonSerializer.Serialize(parties, SnapshotJsonOptions);

    /// <summary>
    /// يحدّث أسماء صور الجهات العامة (kind ∈ {applicant-entity, executed-public}) داخل لقطة
    /// أطراف تمثّل صفوفًا مرتّلة في الملف (PartyId = معرّف صف الوصلة بالملف). يمرّر أسماءً
    /// جديدة مقرونة بـ (Kind, PartyId) — عند كثرة الدمج/الحلول قد يحمل صف وصلة عدة قيود،
    /// ويحتمل أن يختلف اسم الصف المخزَّن عن الاسم المعياري، لذا تُطابق الصورة عبر المعرّف
    /// لا عبر الاسم حصرًا. لا يمسّ الأطراف الأخرى (طبيعيون/ضمان/ورثة...). بلا تطابق →
    /// تُعاد اللقطة كما هي (منطق عدم الكسر). اللقطة التالفة تُتخطى بلا كسر المعاملة.
    /// القيمة الفارغة/الـ null تُطبَّع إلى "[]" لا تُترك null خامًا.
    /// </summary>
    public static string UpdateEntityParties(
        string? json,
        IReadOnlyDictionary<(string Kind, int PartyId), string> newNames)
    {
        var (parties, corrupted) = TryDeserializeParties(json);
        if (parties.Count == 0)
        {
            // اللقطة التالفة تُتخطى بلا كسر المعاملة (وتُدوَّن عند المستدعي)؛
            // الفارغ/"null" النصية تُطبَّع إلى "[]" لا تُترك null خامًا.
            return corrupted ? json! : "[]";
        }

        var changed = false;
        var updated = new List<AppealPartyDto>(parties.Count);
        foreach (var party in parties)
        {
            if (IsPublicEntityKind(party.Kind)
                && newNames.TryGetValue((party.Kind, party.PartyId), out var newName)
                && !string.Equals(party.Name, newName, StringComparison.Ordinal))
            {
                updated.Add(party with { Name = newName });
                changed = true;
            }
            else
            {
                updated.Add(party);
            }
        }

        return changed ? SerializeParties(updated) : json!;
    }

    private static bool IsPublicEntityKind(string? kind)
        => kind == KindApplicantEntity || kind == KindExecutedPublic || kind == KindExecutionApplicant;
}
