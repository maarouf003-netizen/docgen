import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import EntityChangeLog from './EntityChangeLog';
import { UnifyNamesTab } from '../components/entity/UnifyNamesTab';
import ParentSuggestionsTab from '../components/entity/ParentSuggestionsTab';
import { AddEntityTab } from './entityRegistryReviewManagement/AddEntityTab';
import { EditEntityTab } from './entityRegistryReviewManagement/EditEntityTab';
import {
  resolveInitialTab,
  REVIEW_TABS,
  TAB_IDS,
  type TabId,
} from './entityRegistryReviewManagement/reviewTabsModel';
import { TabButton } from './entityRegistryReviewManagement/ReviewTabs';
import { useTabListKeyboard } from '../hooks/useTabListKeyboard';

export default function EntityRegistryReviewManagement() {
  const [searchParams] = useSearchParams();
  const [tab, setTab] = useState<TabId>(() => resolveInitialTab(searchParams.get('tab')));

  // تنقّل التبويبات بلوحة المفاتيح (WAI-ARIA): في RTL السهم الأيسر «التالي».
  const onTabKeyDown = useTabListKeyboard({
    ids: TAB_IDS,
    activeId: tab,
    onSelect: setTab,
    tabId: (id) => `review-tab-${id}`,
  });

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
        {REVIEW_TABS.map((reviewTab) => (
          <TabButton
            key={reviewTab.id}
            id={reviewTab.id}
            label={reviewTab.label}
            active={tab === reviewTab.id}
            onSelect={setTab}
          />
        ))}
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
