import { useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import type { CancellableRequestResult } from './useCancellableRequest';
import type { AppealReminderDto, ReminderDto } from '../types';

/**
 * إلغاء تذكيرات الملف/الاستئناف (حذف التذكير لا الإجراء) — منطق مشترك بين
 * لوحة المحامي وصفحة التقويم: يُحدّث القائمة محليًا فور النجاح.
 */
export function useReminderCancellation(
  remindersQuery: Pick<CancellableRequestResult<ReminderDto[]>, 'setData'>,
  appealRemindersQuery: Pick<CancellableRequestResult<AppealReminderDto[]>, 'setData'>,
) {
  const [cancellingKey, setCancellingKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState('');

  const cancelReminder = async (r: ReminderDto) => {
    const key = String(r.actionId);
    setCancellingKey(key);
    setActionError('');
    try {
      await api.delete(`/documents/${r.documentId}/actions/${r.actionId}/reminder`);
      remindersQuery.setData((prev) => (prev ?? []).filter((x) => x.actionId !== r.actionId));
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setCancellingKey(null);
    }
  };

  const cancelAppealReminder = async (r: AppealReminderDto) => {
    const key = `appeal-${r.actionId}`;
    setCancellingKey(key);
    setActionError('');
    try {
      await api.delete(`/appeals/${r.appealId}/actions/${r.actionId}/reminder`);
      appealRemindersQuery.setData((prev) => (prev ?? []).filter((x) => x.actionId !== r.actionId));
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setCancellingKey(null);
    }
  };

  return { cancellingKey, actionError, cancelReminder, cancelAppealReminder };
}
