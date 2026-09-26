/**
 * اسم الفرع الافتراضي للجهة الأم (بلا اسم فرعي) — مرآة حرفية لـ
 * `PublicEntityBranchCatalog.ParentBranchName` في الخلفية
 * (`backend/src/DocGenerator.Domain/Enums/PublicEntityCatalogs.cs`).
 */
export const PARENT_BRANCH_NAME = 'الجهة الأم';

/**
 * صيغة تسمية فرع الجهة العامة: «المحافظة/الفرع»، والجهة الأم (أو الفراغ)
 * تُعرَض بالمحافظة وحدها بلا تكرار فرعي، والفرع بلا محافظة يُعرَض وحده.
 *
 * ⚠️ الدلالة مرآة لـ `PublicEntityBranchCatalog.Label` في الخلفية، وهي
 * المصدر المرجعي (عمود «فرع الجهة» في تصدير البوابة). القاعدتان مربوطتان
 * باختبارات متقابلة على الجهتين: تعديل هنا يفشل
 * `PublicEntityBranchCatalogTests`، وتعديل هناك يفشل
 * `publicEntityBranchLabel.test.ts` — فلا تتمايز الصيغة بصمت.
 */
export function publicEntityBranchLabel(
  governorate: string | null | undefined,
  branchName: string | null | undefined,
): string {
  const gov = (governorate ?? '').trim();
  const branch = (branchName ?? '').trim();
  if (!branch || branch === PARENT_BRANCH_NAME) return gov;
  return gov ? `${gov}/${branch}` : branch;
}
