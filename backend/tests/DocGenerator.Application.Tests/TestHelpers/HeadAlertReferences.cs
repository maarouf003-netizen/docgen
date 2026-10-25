using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// مراجع حقيقية للتنبيهات النظامية في اختبارات التكامل: <c>DelegationId</c> لا يحمل
/// مفتاحًا أجنبيًا في المخطط (فهرس فقط)، لذا يحتاج فحص المراجع قبل الإدراج صفوف
/// إنابة فعلية بدل معرّفات اصطناعية لا وجود لها.
/// </summary>
public static class HeadAlertReferences
{
    /// <summary>
    /// ينشئ ملفًا أصليًا واحدًا ثم إنابات بالمعرّفات الصريحة المطلوبة، كلها تابعة
    /// لذلك الملف (علاقة واحد-إلى-متعدد مشروعة)، ويعيد معرّف الملف.
    /// </summary>
    public static int SeedDelegations(
        DocGeneratorDbContext db, int branchId, int createdById, params int[] delegationIds)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(delegationIds);

        var document = new Document
        {
            BranchId = branchId,
            CreatedById = createdById,
            IsDraft = false,
        };
        db.Documents.Add(document);
        db.SaveChanges();

        foreach (var id in delegationIds.Distinct().OrderBy(value => value))
        {
            db.DocumentDelegations.Add(new DocumentDelegation
            {
                Id = id,
                SourceDocumentId = document.Id,
                CreatedById = createdById,
            });
        }
        db.SaveChanges();

        return document.Id;
    }
}
