import { useState, type KeyboardEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import EntityChangeLog from './EntityChangeLog';
import { UnifyNamesTab } from '../components/entity/UnifyNamesTab';
import ParentSuggestionsTab from '../components/entity/ParentSuggestionsTab';
import { AddEntityTab } from './entityRegistryReviewManagement/AddEntityTab';
import { EditEntityTab } from './entityRegistryReviewManagement/EditEntityTab';
import {
  resolveInitialTab,
  TAB_IDS,
  type TabId,
} from './entityRegistryReviewManagement/reviewTabsModel';
import { TabButton } from './entityRegistryReviewManagement/ReviewTabs';

export default function EntityRegistryReviewManagement() {
  const [searchParams] = useSearchParams();
  const [tab, setTab] = useState<TabId>(() => resolveInitialTab(searchParams.get('tab')));

  // تنقّل التبويبات بلوحة المفاتيح (WAI-ARIA): في RTL السهم الأيسر «التالي».
  const selectTabAndFocus = (next: TabId) => {
    setTab(next);
    document.getElementById(`review-tab-${next}`)?.focus();
  };

  const onTabKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const index = TAB_IDS.indexOf(tab);
    let next: TabId | null = null;
    if (event.key === 'ArrowLeft') next = TAB_IDS[(index + 1) % TAB_IDS.length];
    else if (event.key === 'ArrowRight') next = TAB_IDS[(index - 1 + TAB_IDS.length) % TAB_IDS.length];
    else if (event.key === 'Home') next = TAB_IDS[0];
    else if (event.key === 'End') next = TAB_IDS[TAB_IDS.length - 1];
    if (next === null) return;
    event.preventDefault();
    selectTabAndFocus(next);
  };

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-5">
        <h2 className="text-2xl font-bold text-gray-800 text-wrap-balance">
          مراجعة سجل الجهات العامة
        </h2>
      </div>

      {/* تبويبات */}
      <div
        className="flex flex-wrap gap-2 mb-6"
        role="tablist"
        aria-label="أقسام مراجعة سجل الجهات العامة"
        onKeyDown={onTabKeyDown}
      >
        <TabButton id="edit" label="تعديل جهة عامة" active={tab === 'edit'} onSelect={setTab} />
        <TabButton id="add" label="إضافة جهة" active={tab === 'add'} onSelect={setTab} />
        <TabButton id="unify" label="توحيد تسميات" active={tab === 'unify'} onSelect={setTab} />
        <TabButton id="log" label="سجل تغييرات الجهة" active={tab === 'log'} onSelect={setTab} />
        <TabButton id="suggestions" label="اقتراحات الأم" active={tab === 'suggestions'} onSelect={setTab} />
      </div>

      <div
        role="tabpanel"
        id={`review-tabpanel-${tab}`}
        aria-labelledby={`review-tab-${tab}`}
      >
        {tab === 'edit' && <EditEntityTab key="edit" />}
        {tab === 'add' && <AddEntityTab />}
        {tab === 'unify' && <UnifyNamesTab />}
        {tab === 'log' && <EntityChangeLog />}
        {tab === 'suggestions' && <ParentSuggestionsTab />}
      </div>
    </div>
  );
}
