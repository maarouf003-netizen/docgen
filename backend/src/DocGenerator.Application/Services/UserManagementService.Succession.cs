using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class UserManagementService
{
    private async Task<int?> ResolveBranchAsync(int? branchId, UserRole role, CancellationToken ct)
    {
        if (!BranchRoles.Contains(role))
            return null;

        if (branchId is null)
            throw new ArgumentException("يجب تحديد الفرع لهذا الدور");

        var branch = await _branches.GetByIdAsync(branchId.Value, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        return branch.Id;
    }

    /// <summary>
    /// شعبة الحساب (§9.1 + §4.3): إلزامية لرئيس الشعبة (وجودًا وتطابق فرع)،
    /// ومرفوضة لغيره. تُستدعى بعد حل الفرع لأن التطابق عليه.
    /// </summary>
    private async Task<int?> ResolveSectionAsync(UserRole role, int? branchId, int? sectionId, CancellationToken ct)
    {
        if (role != UserRole.SubHead)
        {
            if (sectionId.HasValue)
                throw new ArgumentException("الشعبة مخصصة لرئيس الشعبة فقط");
            return null;
        }
        if (sectionId is null)
            throw new ArgumentException("الشعبة إلزامية لرئيس الشعبة — اختر شعبة الفرع");
        var section = await _sections.GetByIdAsync(sectionId.Value, ct);
        if (section is null)
            throw new ArgumentException("الشعبة غير موجودة");
        if (section.BranchId != branchId)
            throw new ArgumentException("الشعبة ليست ضمن فرع الحساب");
        return section.Id;
    }

    private static UserRole ParseRole(string? role)
    {
        if (!Enum.TryParse<UserRole>(role?.Trim(), ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException("دور غير صالح");
        return parsed;
    }

    /// <summary>
    /// وحدانية الرئاسة المفعّلة (قرار §2.3/§2.26 + §9.2): رئيس قسم واحد مفعّل لكل
    /// فرع، ورئيس شعبة مفعّل واحد لكل شعبة — بالرسائل المعتمدة (§14). القيد الجزئي
    /// الفريد في القاعدة ظهرًا للكتابة المباشرة؛ هذا الحارس يعطي `400` نظيفًا بدل `500`.
    /// </summary>
    private async Task GuardSingleActiveHeadAsync(UserRole role, int? branchId, int? sectionId, int? excludeUserId, CancellationToken ct)
    {
        if (role is not (UserRole.Head or UserRole.SubHead))
            return;
        if (await _users.ExistsActiveHeadAsync(role, branchId, sectionId, excludeUserId, ct))
            throw new ArgumentException(role == UserRole.Head ? "الفرع لا يقبل رئيسي قسم" : "الشعبة مشغولة برئيس مفعّل");
    }

    /// <summary>
    /// التحقق من الخلف (§9.3): موجود ومفعّل وفي نفس الفرع وغير الذات، ودوره
    /// قابل للرئاسة (محامٍ يُرقَّى أو رئيس — لا مشرف/مدير/مندوب).
    /// </summary>
    private async Task<User> ResolveSuccessorAsync(User user, int successorId, CancellationToken ct)
    {
        var successor = await _users.GetByIdAsync(successorId, ct)
            ?? throw new ArgumentException("الخلف غير موجود");
        if (successor.Id == user.Id)
            throw new ArgumentException("الخلف لا يمكن أن يكون الحساب نفسه");
        if (!successor.IsActive)
            throw new ArgumentException("الخلف يجب أن يكون حسابًا مفعّلًا");
        if (successor.BranchId != user.BranchId)
            throw new ArgumentException("الخلف يجب أن يكون في نفس الفرع");
        if (successor.Role is UserRole.Admin or UserRole.Manager or UserRole.EntityManager)
            throw new ArgumentException("الخلف يجب أن يكون محاميًا أو رئيسًا");
        return successor;
    }

    /// <summary>
    /// تطبيق الإحلال (قرار §2.18) — ضمن معاملة التعطيل نفسها: الخلف يحل محل
    /// السلف بكل شيء (الدور والشعبة)، مع نقل محامي الصفر ملفات والتنبيهات
    /// المعلقة والكتب المعلقة، وإبطال الجلسات، وصفي سجل تعاقب.
    /// </summary>
    private async Task ApplySuccessionAsync(User predecessor, User successor, string? actorName, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        successor.Role = predecessor.Role;
        successor.SectionId = predecessor.SectionId;
        successor.TokenVersion++;
        successor.UpdatedAt = now;
        _users.Update(successor);

        // محامو الصفر ملفات الذين أنشأهم السلف يتبعون الخلف (§2.10).
        // يُعاد الجلب لا التحديث المباشر لنتائج `AsNoTracking`: نسخة واحدة
        // متتبَّعة لكل مفتاح بلا تعارض — والخلف نفسه مستثنى (ما زال محاميًا في
        // القاعدة لحظة الاستعلام، وإسناده لذاته حلقة مرفوضة).
        var createdIds = (await _users.ListByCreatorAsync(predecessor.Id, ct))
            .Select(u => u.Id)
            .ToList();
        foreach (var lawyerId in createdIds)
        {
            if (lawyerId == successor.Id)
                continue;
            var lawyer = await _users.GetByIdAsync(lawyerId, ct);
            if (lawyer is null || lawyer.Role != UserRole.Lawyer)
                continue;
            if ((await _documents.ListByOwnerAsync(lawyer.Id, ct)).Count > 0)
                continue;
            lawyer.CreatedById = successor.Id;
            lawyer.UpdatedAt = now;
            _users.Update(lawyer);
        }

        // الكتب المعلقة (غير المجابة — بلا رد من المستلم) تُنقَل للخلف (§2.24).
        var messages = await _messages.ListAsync(ct);
        var repliedByPredecessor = messages
            .Where(m => m.Kind == CorrespondenceMessage.KindReply && m.AuthorId == predecessor.Id)
            .Select(m => m.CorrespondenceId)
            .ToHashSet();
        var letters = await _correspondences.ListAsync(ct);
        foreach (var c in letters.Where(c => c.TargetUserId == predecessor.Id && !repliedByPredecessor.Contains(c.Id)))
        {
            c.TargetUserId = successor.Id;
            c.RecipientSectionId = successor.SectionId;
            c.UpdatedAt = now;
            _correspondences.Update(c);
        }

        // تُحفظ تغييرات المستخدم/الملفات/الكتب أولًا، ثم تُنقل التنبيهات في خدمة
        // مستقلة قد تعيد المحاولة بعد تعارض فريد (فتفصل ChangeTracker) — بذلك لا
        // تُفقد تعديلات التعاقب غير التنبيهية عند إعادة المحاولة.
        await _uow.SaveChangesAsync(ct);
        await _alertSuccession.ApplyAsync(
            predecessor.Id,
            successor.Id,
            predecessor.FullName,
            successor.FullName,
            actorName,
            ct);

        var successionAt = DateTime.UtcNow;
        await _successions.AddAsync(new HeadSuccession
        {
            BranchId = predecessor.BranchId!.Value,
            SectionId = predecessor.SectionId,
            UserId = predecessor.Id,
            Role = predecessor.Role,
            Event = HeadSuccessionEventCatalog.Deactivated,
            At = successionAt,
            ActorName = actorName,
            Reason = $"عُطّل وحل محله {successor.FullName} ({successor.Username})",
        }, ct);
        await _successions.AddAsync(new HeadSuccession
        {
            BranchId = successor.BranchId!.Value,
            SectionId = successor.SectionId,
            UserId = successor.Id,
            Role = successor.Role,
            Event = HeadSuccessionEventCatalog.Succeeded,
            At = successionAt,
            ActorName = actorName,
            Reason = $"خلفًا لـ {predecessor.FullName} ({predecessor.Username})",
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// حد المشرف (`BQ-001د`): المدير يدير كل الأدوار عدا المشرف — إنشاء حساب
    /// مشرف أو المساس بحساب مشرف (تعديل/إيقاف) مرفوض `403`. الفاعل الغائب
    /// (مسارات قديمة/اختبارات بلا فاعل) يتجاوز الفحص مؤقتًا — كل نقاط الإنتاج
    /// تمرر الفاعل.
    /// </summary>
    private async Task GuardManagerAdminBoundaryAsync(
        int? actorUserId, bool requestedIsAdmin, bool targetIsAdmin, CancellationToken ct)
    {
        if (actorUserId is null)
            return;
        var actor = await _users.GetByIdAsync(actorUserId.Value, ct);
        if (actor?.Role == UserRole.Manager && (requestedIsAdmin || targetIsAdmin))
            throw new UnauthorizedAccessException("حسابات المشرف يديرها مشرف فقط");
    }
}
