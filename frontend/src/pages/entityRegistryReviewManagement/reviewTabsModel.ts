export type TabId = 'edit' | 'add' | 'unify' | 'log' | 'suggestions';

/** ترتيب التبويبات المرئي (يُستخدم في تنقّل لوحة المفاتيح والاختبارات). */
export const TAB_IDS: TabId[] = ['edit', 'add', 'unify', 'log', 'suggestions'];

/** يحوّل قيمة `?tab=` إلى تبويب معروف، والافتراضي «تعديل جهة عامة». */
export function resolveInitialTab(tab: string | null): TabId {
  if (tab === 'add' || tab === 'log' || tab === 'unify' || tab === 'suggestions') return tab;
  return 'edit';
}
