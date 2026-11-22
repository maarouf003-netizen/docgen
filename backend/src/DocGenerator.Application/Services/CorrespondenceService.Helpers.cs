using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class CorrespondenceService : ICorrespondenceService
{
    /// <summary>
    /// ترقيم الرسائل داخل نطاق المراسلة: الأولى تحمل رقم المراسلة نفسه، واللاحقات
    /// والردود <c>{رقم المراسلة}-{التسلسل}</c> حيث التسلسل = عدد الرسائل + 1.
    /// حتمي بلا تخمين عشوائي: لا سباق توليد ولا غموض ترقيم في السجل الرسمي —
    /// والترتيب الزمني المرجعي يبقى بالمعرّف كما في كل مسارات القراءة.
    /// قرار موثّق — سباق مقبول: العدّاد يُقرأ خارج المعاملة، فطلبٌ متزامن نادر قد
    /// يحسب التسلسل نفسه لرسالتين (لا قيد تفرّد ممكن على MessageNumber لأن الأولى
    /// تكرّر نفس رقم المراسلة عمدًا). أثره تجميلي لا مرجعي: الترتيب الرسمي بالمعرّف
    /// بكل مسارات القراءة، ولا مرجع خارجي على MessageNumber. المحفّز الأرجح (نقرة
    /// مزدوجة) مسدود من الواجهة — زر الإرسال معطَّل أثناء الحفظ — والإغلاق الكامل
    /// بعدّاد ذرّي (UPDATE … RETURNING) مؤجَّل بقرار صريح لأنه يستلزم هجرة EF
    /// لا تبرّر كلفتها الأثرُ المتبقي النادر.
    /// </summary>
    private static string NextMessageNumber(Correspondence letter)
    {
        var sequence = letter.Messages.Count + 1;
        if (sequence <= 1)
            return letter.CorrespondenceNumber;
        return letter.CorrespondenceNumber
            + "-"
            + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// الرقم بصيغة {الرمز}-{السنة}-{عشوائي 5 خانات} مع فحص تفرّد مسبق بإعادة المحاولة:
    /// سعة 100,000 رقم لكل بادئة/سنة (5 خانات بدل 4) لاستيعاب الضغط دون استنفاد مبكر.
    /// الفحص استباقي فقط (خارج المعاملة)؛ الضمان النهائي قيد التفرّد الفريد في قاعدة
    /// البيانات مع حلقة إعادة التوليد في مسار الإنشاء عند اصطدامه (F1).
    /// </summary>
    private async Task<string> GenerateUniqueNumberAsync(string code, DateTime at,
        CancellationToken ct)
    {
        var prefix = NormalizePrefix(code);
        var year = at.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

        for (var attempt = 0; attempt < MaxNumberGenerationAttempts; attempt++)
        {
            var random = _entropy.NextInt64(0, 100_000)
                .ToString(System.Globalization.CultureInfo.InvariantCulture)
                .PadLeft(NumberRandomDigits, '0');
            var candidate = $"{prefix}-{year}-{random}";
            if (!await _letters.NumberExistsAsync(candidate, ct))
                return candidate;
        }

        throw new DocumentConflictException("تعذر توليد رقم فريد للمراسلة، حاول مجدداً");
    }

    private static string NormalizePrefix(string code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return "CR";
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            builder.Append(char.IsWhiteSpace(ch) ? '-' : ch);
        return builder.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// نص الرسالة الصافي بعد التعقيم، مع فرض حدّ الطول: الفارغ يُرفض برسالة
    /// النوع، والمتجاوز يُرفض برسالة الحد (`MaxBodyPlainTextLength`).
    /// </summary>
    private static string RequireBodyText(string bodyHtml, string emptyMessage, string kindLabel)
    {
        var bodyText = HtmlInputSanitizer.ToPlainText(bodyHtml);
        if (string.IsNullOrWhiteSpace(bodyText))
            throw new ArgumentException(emptyMessage);
        if (bodyText.Length > MaxBodyPlainTextLength)
            throw new ArgumentException(
                $"{kindLabel} يتجاوز الحد الأقصى ({MaxBodyPlainTextLength.ToString(System.Globalization.CultureInfo.InvariantCulture)} حرف)");
        return bodyText;
    }

    private static string? NormalizeImportanceFilter(string? importance)
    {
        if (string.IsNullOrWhiteSpace(importance))
            return null;
        var term = importance.Trim().ToLowerInvariant();
        if (!Correspondence.IsValidImportance(term))
            throw new ArgumentException("فلتر الأهمية غير صالح");
        return term;
    }

    /// <summary>
    /// أدوار الكتابة في المراسلات: محامٍ/رئيس قسم/شعبة/مندوب جهة (تُفرض أيضًا عبر
    /// RolePermissions في المتحكمات؛ هذا الفحص دفاع داخلي للخدمة). الشعبة ترث
    /// القسم هنا بموجب القرار §2.21 — لا تأليف على الملفات لها (`MayAttach`
    /// يردها)، بل مراسلات عامة بفرعها.
    /// </summary>
    private static bool CanWrite(UserRole role)
        => role is UserRole.Lawyer or UserRole.Head or UserRole.SubHead or UserRole.EntityManager;
}
