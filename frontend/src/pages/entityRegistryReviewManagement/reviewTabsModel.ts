export type TabId = 'edit' | 'add' | 'unify' | 'log' | 'suggestions';

export interface ReviewTab {
  id: TabId;
  label: string;
}

/**
 * المصدر الوحيد لترتيب التبويبات وتسمياتها: تُبنى منه الأزرار وقائمة التنقّل
 * (`TAB_IDS`) معًا، فلا ينحرف ترتيب لوحة المفاتيح عن ترتيب العرض.
 */
export const REVIEW_TABS: readonly ReviewTab[] = [
  { id: 'edit', label: 'تعديل جهة عامة' },
  { id: 'add', label: 'إضافة جهة' },
  { id: 'unify', label: 'توحيد تسميات' },
  { id: 'log', label: 'سجل تغييرات الجهة' },
  { id: 'suggestions', label: 'اقتراحات الأم' },
];

/** ترتيب التبويبات المرئي (يُستخدم في تنقّل لوحة المفاتيح والاختبارات). */
export const TAB_IDS: readonly TabId[] = REVIEW_TABS.map((tab) => tab.id);

/** يحوّل قيمة `?tab=` إلى تبويب معروف، والافتراضي «تعديل جهة عامة». */
export function resolveInitialTab(tab: string | null): TabId {
  if (tab === 'add' || tab === 'log' || tab === 'unify' || tab === 'suggestions') return tab;
  return 'edit';
}
