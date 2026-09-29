import { useMemo, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useReminderCancellation } from '../hooks/useReminderCancellation';
import { DayRemindersModal, PersonalForm } from '../components/dashboard/DayRemindersModal';
import { LawyerCalendar } from '../components/dashboard/LawyerCalendar';
import { ReminderList } from '../components/dashboard/ReminderList';
import {
  dayLabelOf,
  dotTone,
  groupRemindersByDay,
  nextOccurrenceKey,
  parseDayKey,
  toDayKey,
} from '../components/dashboard/personalReminders';
import type { AppealReminderDto, PersonalReminderDto, ReminderDto } from '../types';

/**
 * صفحة التقويم (`/calendar` — محامي فقط): التقويم العربي الكامل مع نافذة اليوم،
 * وتحته **جميع** التذكيرات — ملف/استئناف كاملة، والشخصية بتعريفاتها (عنوان +
 * تكرار + الوقوع التالي) مع إنجاز/تعديل/حذف. لوحة التحكم تُبقي نسختها المصغرة
 * (التقويم + تذكيرات الأسبوع).
 */
export default function CalendarPage() {
  const { user } = useAuth();
  const userReady = Boolean(user);

  const remindersQuery = useCancellableRequest<ReminderDto[]>(
    (signal) => api.get('/reminders', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady },
  );
  const appealRemindersQuery = useCancellableRequest<AppealReminderDto[]>(
    (signal) => api.get('/appeals/reminders', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady },
  );
  const personalQuery = useCancellableRequest<PersonalReminderDto[]>(
    (signal) => api.get('/personal-reminders', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady },
  );

  const reminders = useMemo(() => remindersQuery.data ?? [], [remindersQuery.data]);
  const appealReminders = useMemo(() => appealRemindersQuery.data ?? [], [appealRemindersQuery.data]);
  const personalReminders = useMemo(() => personalQuery.data ?? [], [personalQuery.data]);

  const { cancellingKey, actionError, cancelReminder, cancelAppealReminder } =
    useReminderCancellation(remindersQuery, appealRemindersQuery);

  const [selectedDay, setSelectedDay] = useState<string | null>(null);
  const dayOccurrences = useMemo(() => {
    if (!selectedDay) return [];
    const day = parseDayKey(selectedDay);
    if (!day) return [];
    return groupRemindersByDay([...reminders, ...appealReminders], personalReminders, day, day).get(selectedDay) ?? [];
  }, [selectedDay, reminders, appealReminders, personalReminders]);

  const refreshAll = () => {
    remindersQuery.refetch();
    appealRemindersQuery.refetch();
    personalQuery.refetch();
  };

  const todayKey = toDayKey(new Date());
  const fileCount = reminders.length + appealReminders.length;

  return (
    <div className="max-w-7xl mx-auto">
      <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6 text-balance">التقويم</h2>

      <section aria-label="التقويم الشهري" className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
        <LawyerCalendar
          reminders={[...reminders, ...appealReminders]}
          personal={personalReminders}
          weekStartsOn="sunday"
          selectedDay={selectedDay}
          onSelectDay={setSelectedDay}
        />
      </section>

      {selectedDay ? (
        <DayRemindersModal
          dayKey={selectedDay}
          occurrences={dayOccurrences}
          onClose={() => setSelectedDay(null)}
          onChanged={refreshAll}
        />
      ) : null}

      <section aria-label="تذكيرات الملفات والاستئنافات" className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-6">
        <div className="flex items-center gap-2 px-4 sm:px-5 py-4 border-b border-gray-100">
          <span className="w-2 h-2 rounded-full bg-amber-500" aria-hidden="true" />
          <h3 className="font-bold text-gray-900">تذكيرات الملفات والاستئنافات</h3>
          <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium tabular-nums">
            {fileCount}
          </span>
        </div>
        {actionError || remindersQuery.error || appealRemindersQuery.error ? (
          <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
            <p className="text-red-700 text-sm">{actionError || remindersQuery.error || appealRemindersQuery.error}</p>
          </div>
        ) : null}
        {fileCount === 0 ? (
          <div className="p-10 text-center">
            <p className="text-gray-400 text-sm">لا توجد تذكيرات ملفات حالياً</p>
          </div>
        ) : (
          <ReminderList
            reminders={reminders}
            appealReminders={appealReminders}
            onCancel={cancelReminder}
            onCancelAppeal={cancelAppealReminder}
            cancellingKey={cancellingKey}
          />
        )}
      </section>

      <PersonalDefinitionsSection personal={personalReminders} onChanged={personalQuery.refetch} todayKey={todayKey} />
    </div>
  );
}

function PersonalDefinitionsSection({
  personal,
  onChanged,
  todayKey,
}: {
  personal: PersonalReminderDto[];
  onChanged: () => void;
  todayKey: string;
}) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [deleting, setDeleting] = useState<PersonalReminderDto | null>(null);
  const [actingKey, setActingKey] = useState<string | null>(null);
  const [error, setError] = useState('');

  const toggleNext = async (p: PersonalReminderDto) => {
    const next = nextOccurrenceKey(p);
    if (!next) return;
    const key = `done-${p.id}`;
    setActingKey(key);
    setError('');
    try {
      await api.patch(`/personal-reminders/${p.id}/occurrences`, { occurrenceDate: next, done: true });
      onChanged();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setActingKey(null);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setActingKey(`delete-${deleting.id}`);
    setError('');
    try {
      await api.delete(`/personal-reminders/${deleting.id}`);
      setDeleting(null);
      setEditingId(null);
      onChanged();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setActingKey(null);
    }
  };

  return (
    <section aria-label="التذكيرات الشخصية" className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-6">
      <div className="flex items-center gap-2 px-4 sm:px-5 py-4 border-b border-gray-100">
        <span className="w-2 h-2 rounded-full bg-violet-500" aria-hidden="true" />
        <h3 className="font-bold text-gray-900">التذكيرات الشخصية</h3>
        <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium tabular-nums">
          {personal.length}
        </span>
      </div>

      {error ? (
        <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
          <p role="alert" className="text-red-700 text-sm">{error}</p>
        </div>
      ) : null}

      {deleting ? (
        <div className="mx-4 sm:mx-5 mt-4 border border-red-200 bg-red-50 rounded-2xl p-4" role="alert">
          <p className="text-sm text-red-900 font-medium break-words">
            حذف «{deleting.title}» مع كل تكراراته؟ لا يمكن التراجع.
          </p>
          <div className="flex gap-2 mt-3">
            <button
              type="button"
              onClick={confirmDelete}
              disabled={actingKey !== null}
              className="min-h-11 px-4 rounded-lg bg-red-600 hover:bg-red-700 text-white text-sm font-medium disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-red-600"
            >
              {actingKey !== null ? 'جارٍ الحذف…' : 'تأكيد الحذف'}
            </button>
            <button
              type="button"
              onClick={() => setDeleting(null)}
              className="min-h-11 px-4 rounded-lg border border-gray-200 text-sm hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              تراجع
            </button>
          </div>
        </div>
      ) : null}

      {personal.length === 0 ? (
        <div className="p-10 text-center">
          <p className="text-gray-400 text-sm">لا تذكيرات شخصية — أضفها من أي يوم في التقويم أعلاه</p>
        </div>
      ) : (
        <ul className="divide-y divide-gray-100">
          {personal.map((p) => {
            const next = nextOccurrenceKey(p);
            const doneKey = `done-${p.id}`;
            return (
              <li key={p.id} className="px-4 sm:px-5 py-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-medium text-gray-900 break-words">{p.title}</p>
                    {p.notes ? (
                      <p className="text-xs text-gray-500 mt-0.5 break-words line-clamp-2">{p.notes}</p>
                    ) : null}
                    <p className="text-xs text-gray-400 mt-1">
                      {p.recurrence}
                      {p.recurrenceEnd ? ` · حتى ${p.recurrenceEnd}` : ' · بلا نهاية'}
                      {next ? ` · الوقوع التالي: ${dayLabelOf(next)}` : ' · انتهت التكرارات'}
                    </p>
                  </div>
                  <span
                    className={`w-2.5 h-2.5 rounded-full shrink-0 mt-1.5 ${dotTone(p.color)}`}
                    aria-hidden="true"
                  />
                </div>
                {editingId === p.id ? (
                  <div className="mt-2">
                    <PersonalForm
                      key={`cal-edit-${p.id}`}
                      dayKey={todayKey}
                      initial={p}
                      onClose={() => setEditingId(null)}
                      onSaved={() => {
                        setEditingId(null);
                        onChanged();
                      }}
                    />
                  </div>
                ) : (
                  <div className="flex flex-wrap gap-2 mt-2">
                    {next ? (
                      <button
                        type="button"
                        onClick={() => toggleNext(p)}
                        disabled={actingKey === doneKey}
                        className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 hover:bg-gray-50 disabled:opacity-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
                      >
                        تم
                      </button>
                    ) : null}
                    <button
                      type="button"
                      onClick={() => setEditingId(p.id)}
                      className="min-h-11 px-3 rounded-lg text-xs font-medium border border-gray-200 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
                    >
                      تعديل
                    </button>
                    <button
                      type="button"
                      onClick={() => setDeleting(p)}
                      className="min-h-11 px-3 rounded-lg text-xs font-medium text-red-700 hover:bg-red-50 focus-visible:ring-2 focus-visible:ring-red-600"
                    >
                      حذف
                    </button>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
