import { useCallback, useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { isAxiosError } from 'axios';
import { api, getApiErrorMessage, getDownloadErrorMessage } from '../../api/client';
import { readDocumentListPage } from '../../api/documents';
import { useAuth } from '../../auth/useAuth';
import { isHeadOrSubHeadRole } from '../../auth/roles';
import { useIsMobile } from '../../hooks/useMediaQuery';
import { useCancellableRequest } from '../../hooks/useCancellableRequest';
import { useDebouncedValue } from '../../hooks/useDebouncedValue';
import { downloadBlob } from '../../utils/download';
import { isStatusOption } from '../../utils/documentStatus';
import { displayFileNumber, identityName } from '../../utils/documentDisplay';
import { todayLocalKey } from '../../utils/dates';
import {
  loadDocumentsListPosition,
  loadLastViewedDocumentId,
  saveDocumentsListPosition,
  saveLastViewedDocumentId,
} from '../../utils/listSession';
import type { DocumentListItemDto, DocumentResponse, PagedResult } from '../../types';

/**
 * منطق صفحة قائمة الملفات: البحث/الفلاتر، حفظ الموضع واستعادته، ترقيم المؤشر,
 * «تحميل المزيد»، تمييز آخر ملف، وتصدير إكسل. منقول حرفيًا من `DocumentsList`
 * في `P6-02e` بلا تغيير سلوكي.
 */
export function useDocumentsList() {
  const { hasFullAccess, user } = useAuth();
  const isHeadOrSubHead = isHeadOrSubHeadRole(user?.role);
  const isMobile = useIsMobile();
  // استعادة موضع القائمة (البحث/الفلاتر/الصفحة) وملف «آخر ما فُتح» من جلسة المتصفح عند العودة
  // من صفحة ملف، فيعود المستخدم إلى مكانه ويُميَّز الملف الذي كان يعمل عليه.
  const [saved] = useState(() => loadDocumentsListPosition());
  const [searchParams] = useSearchParams();
  const [query, setQuery] = useState(saved?.query ?? '');
  // تأخير البحث النصي 300ms (مثل البوابة/السجل): طلب واحد بعد توقف الكتابة لا لكل حرف.
  const debouncedQuery = useDebouncedValue(query, 300);
  // فلتر الحالة من الـ `URL` (روابط التعمّق من لوحة المحامي) يسبق موضع الجلسة المستعاد،
  // والقيمة الغريبة تُهمل في الحالتين (تُعامل كأنها «لا فلتر»).
  const [status, setStatus] = useState(() => {
    const fromUrl = searchParams.get('status');
    if (fromUrl && isStatusOption(fromUrl)) return fromUrl;
    return saved?.status && isStatusOption(saved.status) ? saved.status : '';
  });
  // فلتر `URL` طبقة مؤقتة: لا يُحفَظ في الجلسة حتى يلمسه المستخدم يدويًا —
  // وإلا علّق رابط التعمّق فلترًا دائمًا في الزيارات النظيفة اللاحقة.
  const [statusTouched, setStatusTouched] = useState(false);
  const persistedStatus = statusTouched ? status : (saved?.status && isStatusOption(saved.status) ? saved.status : '');

  // إعادة ضبط الصفحات المضافة عند تغيير أي فلتر أو رابط تعمّق (P3-05b).
  const listGeneration = useRef(0);
  const resetPaging = () => {
    // يُبطل أي «تحميل المزيد» جارٍ حتى لو لم يُبنَ المفتاح الجديد بعد (دفعة React واحدة).
    listGeneration.current += 1;
    setExtraItems([]);
    setExtraCursors([]);
    setNextCursor(null);
    setLoadMoreError('');
    setRestoreAttempted(false);
  };

  // مزامنة الـ `URL` مع الحالة عند تغيّره دون إعادة تركيب (رجوع/تقدم أو رابط تعمّق
  // ثانٍ) — التعديل اليدوي لا يُمسّ، ورابط التعمّق الجديد يبدأ دومًا من الصفحة الأولى.
  const urlStatusParam = searchParams.get('status');
  const appliedUrlStatus = useRef<string | null>(null);
  useEffect(() => {
    const valid = urlStatusParam && isStatusOption(urlStatusParam) ? urlStatusParam : null;
    if (valid !== appliedUrlStatus.current) {
      appliedUrlStatus.current = valid;
      if (valid) {
        setStatus(valid);
        setStatusTouched(false);
        resetPaging();
      }
    }
  }, [urlStatusParam]);
  const [applicant, setApplicant] = useState(saved?.applicant ?? '');
  const [court, setCourt] = useState(saved?.court ?? '');
  const [lawyer, setLawyer] = useState(saved?.lawyer ?? '');
  const [administrativeBranch, setAdministrativeBranch] = useState(saved?.administrativeBranch ?? '');
  const [executedEntity, setExecutedEntity] = useState(saved?.executedEntity ?? '');
  const [publicEntityBranch, setPublicEntityBranch] = useState(saved?.publicEntityBranch ?? '');
  const [extraItems, setExtraItems] = useState<DocumentListItemDto[]>([]);
  const [extraCursors, setExtraCursors] = useState<string[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadMoreError, setLoadMoreError] = useState('');
  const [restoreAttempted, setRestoreAttempted] = useState(false);

  const [focusId, setFocusId] = useState<number | null>(null);
  const [focusName, setFocusName] = useState<string | null>(null);
  // «آخر ملف فُتح» يُقرأ بعد تحميل هوية المستخدم (AuthContext بلا هوية أصلًا ثم يملؤها من /auth/me)،
  // فيظهر الشريط/التمييز للمالك الحالي فقط ولا يسرّب سجل مستخدم آخر. وحارس المحاولة يمنع إعادة
  // جلب اسم الملف عند كل تغيّر بيانات القائمة بعد فشل غير 403/404 (انقطاع شبكة أو خطأ خادم).
  const focusFetchAttempted = useRef(false);
  useEffect(() => {
    focusFetchAttempted.current = false;
    setFocusId(loadLastViewedDocumentId(user?.id ?? null));
  }, [user?.id]);
  const [exporting, setExporting] = useState(false);
  const [exportMsg, setExportMsg] = useState('');
  const [actionsDocId, setActionsDocId] = useState<number | null>(null);
  const [applicants, setApplicants] = useState<string[]>([]);
  const [courts, setCourts] = useState<string[]>([]);
  const [lawyers, setLawyers] = useState<string[]>([]);
  const [administrativeBranches, setAdministrativeBranches] = useState<string[]>([]);
  const [executedEntities, setExecutedEntities] = useState<string[]>([]);
  const [publicEntityBranches, setPublicEntityBranches] = useState<string[]>([]);
  // عدد المشاهدات أُزيل من النظام بالكامل (قرار المالك) — لا عمود ولا عدّاد في الواجهة.
  const canSeeAdministrativeBranch = hasFullAccess;
  const canSeeAssignedLawyer = hasFullAccess || isHeadOrSubHead;
  const canSearchByLawyer = hasFullAccess || isHeadOrSubHead;
  const canCreate = user?.role === 'lawyer';
  // `BQ-001`: المدير يرى المحذوفات كالمشرف — ورئيس الشعبة كنطاق رئيس القسم (قرار §2.1).
  const canViewDeleted = user?.role === 'lawyer' || user?.role === 'head' || user?.role === 'subhead' || user?.role === 'admin' || user?.role === 'manager';
  const canRotate = user?.role === 'lawyer';

  // يُمنع تصدير كل الملفات: يتطلب التصدير تطبيق فلتر واحد على الأقل (بحث أو أي فلتر منسدل)،
  // وإلا يُعطَّل الزر ويُعترض الطلب دفاعيًا قبل الإرسال.
  const hasActiveFilter = Boolean(
    query || status || applicant || court || lawyer || administrativeBranch || executedEntity || publicEntityBranch,
  );

  const buildListParams = useCallback((cursor?: string) => {
    const params = new URLSearchParams();
    if (debouncedQuery) params.set('q', debouncedQuery);
    if (status) params.set('status', status);
    if (applicant) params.set('applicant', applicant);
    if (court) params.set('court', court);
    if (lawyer) params.set('lawyer', lawyer);
    if (administrativeBranch) params.set('administrativeBranch', administrativeBranch);
    if (executedEntity) params.set('executedEntity', executedEntity);
    if (publicEntityBranch) params.set('publicEntityBranch', publicEntityBranch);
    params.set('perPage', '20');
    if (cursor) params.set('cursor', cursor);
    else params.set('page', '1');
    return params;
  }, [debouncedQuery, status, applicant, court, lawyer, administrativeBranch, executedEntity, publicEntityBranch]);

  const listQuery = useCancellableRequest<PagedResult<DocumentListItemDto>>(
    (signal) => api.get(`/documents/list?${buildListParams().toString()}`, { signal }).then((r) => readDocumentListPage(r.data)),
    [buildListParams],
  );

  // مفتاح الفلتر الحالي: أي «تحميل المزيد» يبدأ بسباق فلتر آخر يُسقط استجابته
  // بدل إلحاق صفوف فلتر قديم بقائمة جديد (فجوة/تلوّث نتائج).
  const listKey = buildListParams().toString();
  const listKeyRef = useRef(listKey);
  listKeyRef.current = listKey;

  const data = listQuery.data;
  const loading = listQuery.isLoading;
  // الصفحة الأولى من الشبكة + الصفحات المضافة بمؤشر «تحميل المزيد» (P3-05b).
  const items = data ? [...data.items, ...extraItems] : [];
  const effectiveNextCursor = extraCursors.length === 0 ? (data?.nextCursor ?? null) : nextCursor;

  // يُحفظ الموضع الحالي (والفلاتر وسلسلة المؤشرات) كي تُستعاد القائمة في مكانها عند العودة من صفحة ملف.
  // (الحالة القادمة من `URL` لا تُحفَظ حتى تُلمَس — انظر `persistedStatus`.)
  useEffect(() => {
    // لا تُكتب سلسلة فارغة فوق موضع محفوظ قبل اكتمال محاولة استعادته (P3-05b).
    if (!restoreAttempted && saved?.cursors?.length)
      return;
    saveDocumentsListPosition({
      query, status: persistedStatus, applicant, court, lawyer, administrativeBranch,
      executedEntity, publicEntityBranch, page: 1,
      ...(extraCursors.length > 0 ? { cursors: extraCursors } : {}),
    });
  }, [query, persistedStatus, applicant, court, lawyer, administrativeBranch, executedEntity, publicEntityBranch, extraCursors, restoreAttempted, saved]);

  // استعادة الصفحات المضافة عند العودة: يُعاد تشغيل سلسلة المؤشرات المحفوظة (بحد أقصى
  // طول السلسلة نفسها) ما دامت الفلاتر مطابقة؛ وأي فشل يُبقي الصفحة الأولى سليمة.
  useEffect(() => {
    if (restoreAttempted || !data || !saved?.cursors?.length || listQuery.isLoading)
      return;
    // لا تستعيد سلسلة المؤشرات قبل استقرار البحث المؤجّل: المؤشر نفسه محفوظ مع فلتر
    // قديم، و`buildListParams(savedCursor)` يقرأ `debouncedQuery`؛ فلو استُعيدت أثناء
    // نافذة التأخير لأُرسل `cursor` بسلسلة `q` **مختلفة** فتُلحق صفوف فلتر آخر
    // بالقائمة (أو يُحرق `restoreAttempted` فيُفقد الموضع).
    if (query !== debouncedQuery)
      return;

    const savedStatus = saved.status && isStatusOption(saved.status) ? saved.status : '';
    const matchesSavedFilters =
      query === saved.query && persistedStatus === savedStatus && applicant === saved.applicant
      && court === saved.court && lawyer === saved.lawyer
      && administrativeBranch === saved.administrativeBranch && executedEntity === saved.executedEntity
      && publicEntityBranch === saved.publicEntityBranch;

    setRestoreAttempted(true);
    if (!matchesSavedFilters)
      return;

    let cancelled = false;
    // جيل القائمة: تغيير أي فلتر أثناء الاستعادة يُبطل ما بُني لفلتر آخر (مثل `loadMore`).
    const restoreGeneration = listGeneration.current;
    const restore = async () => {
      let cursor = data.nextCursor ?? null;
      const restored: DocumentListItemDto[] = [];
      const used: string[] = [];
      for (const savedCursor of (saved.cursors ?? []).slice(0, 20)) {
        if (cancelled || listGeneration.current !== restoreGeneration || !cursor || cursor !== savedCursor)
          break;
        const response = await api.get<PagedResult<DocumentListItemDto>>(
          `/documents/list?${buildListParams(savedCursor).toString()}`);
        const page = readDocumentListPage(response.data);
        restored.push(...page.items);
        used.push(savedCursor);
        cursor = page.nextCursor ?? null;
      }
      if (!cancelled && listGeneration.current === restoreGeneration) {
        setExtraItems(restored);
        setExtraCursors(used);
        setNextCursor(cursor);
      }
    };
    void restore().catch(() => {
      // فشل استعادة الموضع لا يمسّ الصفحة الأولى المعروضة.
    });
    return () => { cancelled = true; };
  }, [data, restoreAttempted, saved, listQuery.isLoading, buildListParams,
    query, debouncedQuery, persistedStatus, applicant, court, lawyer, administrativeBranch, executedEntity, publicEntityBranch]);

  // «تحميل المزيد»: يجلب الصفحة التالية بالمؤشر ويُلحقها بلا إزاحة (P3-05b).
  const loadMore = async () => {
    const cursor = effectiveNextCursor;
    // لا تحميل أثناء جلب فلتر جديد (المؤشر/الصفوف المعروضة من الفلتر السابق).
    if (!cursor || loadingMore || loading)
      return;
    const requestKey = listKeyRef.current;
    const requestGeneration = listGeneration.current;
    setLoadingMore(true);
    setLoadMoreError('');
    try {
      const response = await api.get<PagedResult<DocumentListItemDto>>(
        `/documents/list?${buildListParams(cursor).toString()}`);
      // الفلتر تغيّر أثناء الطلب: لا نلحق صفحة تنتمي لقائمة أخرى.
      if (listKeyRef.current !== requestKey || listGeneration.current !== requestGeneration) return;
      const page = readDocumentListPage(response.data);
      setExtraItems((previous) => [...previous, ...page.items]);
      setExtraCursors((previous) => [...previous, cursor]);
      setNextCursor(page.nextCursor ?? null);
    } catch (err) {
      if (listKeyRef.current !== requestKey || listGeneration.current !== requestGeneration) return;
      setLoadMoreError(getApiErrorMessage(err));
    } finally {
      setLoadingMore(false);
    }
  };

  // عند فتح ملف من القائمة: يُحفظ الموضع الحالي ويُسجَّل الملف كآخر ما فُتح ليميّز عند العودة.
  const openDocument = (id: number) => {
    saveDocumentsListPosition({
      query, status: persistedStatus, applicant, court, lawyer, administrativeBranch,
      executedEntity, publicEntityBranch, page: 1,
      ...(extraCursors.length > 0 ? { cursors: extraCursors } : {}),
    });
    saveLastViewedDocumentId(id, user?.id ?? null);
  };

  const focusVisible = focusId != null && items.some((x) => x.id === focusId);
  const scrolledToFocus = useRef(false);
  useEffect(() => {
    // ينتظر اكتمال جلب القائمة: يُمرَّر إلى الملف إذا كان ظاهرًا، ولا يُجلب اسمه (للشريط الاحتياطي)
    // إلا عند تأكد الغياب عن الصفحة المستعادة؛ فلا يُرسل طلب زائد في كل زيارة.
    if (focusId == null || data == null) return;
    if (focusVisible) {
      if (!scrolledToFocus.current) {
        scrolledToFocus.current = true;
        document.getElementById(`doc-row-${focusId}`)?.scrollIntoView({ block: 'center' });
      }
    } else if (focusName == null && !focusFetchAttempted.current) {
      // محاولة واحدة لكل focusId في هذا التركيب تُمنع إعادة الجلب عند تغيّر بيانات القائمة (data).
      focusFetchAttempted.current = true;
      api
        .get<DocumentResponse>(`/documents/${focusId}`)
        .then((r) => setFocusName(identityName(r.data) || displayFileNumber(r.data) || `مستند ${focusId}`))
        .catch((err: unknown) => {
          // رفض الصلاحية أو غياب الملف: الملف ليس مقروءًا للمستخدم الحالي — يُمسح التركيز
          // ويُخفى الشريط (لا بديل مجهول برابط ميت). غيره (انقطاع شبكة/5xx) لا يمسح شيئًا.
          if (isAxiosError(err) && (err.response?.status === 403 || err.response?.status === 404)) {
            saveLastViewedDocumentId(null, user?.id ?? null);
            setFocusId(null);
          }
        });
    }
    // user?.id في الاعتماديات: يعيد الفحص عند تبديل الهوية وتعطيل سجلات المستخدمين الآخرين.
  }, [focusId, focusVisible, focusName, data, user?.id]);

  useEffect(() => {
    const params = new URLSearchParams();
    if (status) params.set('status', status);
    if (applicant) params.set('applicant', applicant);
    if (court) params.set('court', court);
    if (lawyer) params.set('lawyer', lawyer);
    if (administrativeBranch) params.set('administrativeBranch', administrativeBranch);
    if (executedEntity) params.set('executedEntity', executedEntity);
    if (publicEntityBranch) params.set('publicEntityBranch', publicEntityBranch);
    const qs = params.toString();
    api
      .get<{ applicants: string[]; courts: string[]; lawyers?: string[]; administrativeBranches: string[]; executedEntities: string[]; publicEntityBranches: string[] }>(
        `/documents/filter-options${qs ? `?${qs}` : ''}`,
      )
      .then((r) => {
        setApplicants(Array.isArray(r.data.applicants) ? r.data.applicants : []);
        setLawyers(Array.isArray(r.data.lawyers) ? r.data.lawyers : []);
        setAdministrativeBranches(Array.isArray(r.data.administrativeBranches) ? r.data.administrativeBranches : []);
        setExecutedEntities(Array.isArray(r.data.executedEntities) ? r.data.executedEntities : []);
        setPublicEntityBranches(Array.isArray(r.data.publicEntityBranches) ? r.data.publicEntityBranches : []);
      })
      .catch(() => {});
    // مصدر خيارات «دائرة التنفيذ» من السجل (B24) — دوائر الفرع للمحامي/الرئيس،
    // وكل الدوائر للمدير/المشرف (من الإحصاءات) — مع بقاء عقد الفلتر (court= نصًا)
    // لأن Court يبقى مُزامَنًا. احتياطي القيم الحية عند فراغ السجل فقط.
    (async () => {
      try {
        if (hasFullAccess) {
          const r = await api.get<Array<{ circuitName: string }>>('/execution-circuits/stats');
          const names = ((r?.data ?? []) as Array<{ circuitName: string }>)
            .map((c) => c.circuitName)
            .filter(Boolean);
          if (names.length > 0) {
            setCourts([...new Set(names)]);
            return;
          }
        } else {
          const r = await api.get<Array<{ id: number; name: string }>>('/execution-circuits/for-lawyer');
          const names = ((r?.data ?? []) as Array<{ id: number; name: string }>).map((c) => c.name).filter(Boolean);
          if (names.length > 0) {
            setCourts(names);
            return;
          }
        }
      } catch {
        // السجل الفارغ أو دور بلا نقطة: احتياطي القيم الحية أدناه.
      }
      try {
        const fr = await api.get<{ courts: string[] }>(`/documents/filter-options${qs ? `?${qs}` : ''}`);
        if (Array.isArray(fr?.data?.courts)) setCourts(fr.data.courts);
      } catch {
        // احتياطي صامت: تُبقى القائمة السابقة.
      }
    })();
  }, [status, applicant, court, lawyer, administrativeBranch, executedEntity, publicEntityBranch, hasFullAccess]);

  useEffect(() => {
    if (hasActiveFilter) setExportMsg('');
  }, [hasActiveFilter]);

  const exportExcel = () => {
    if (!hasActiveFilter) {
      setExportMsg('طبّق فلترًا واحدًا على الأقل قبل التصدير');
      return;
    }
    setExportMsg('');
    setExporting(true);
    // عبر مثيل axios المشترك: يضيف الترويسة والوسم تلقائيًا، ويعيد توجيه 401،
    // ولا يكرر قراءة مفتاح التوكن يدويًا.
    api
      .get('/documents/export', {
        params: { q: query, status, applicant, court, lawyer, administrativeBranch, executedEntity, publicEntityBranch },
        responseType: 'blob',
      })
      .then((res) => {
        downloadBlob(res.data as Blob, `الملفات التنفيذية ${todayLocalKey()}.xlsx`);
      })
      .catch(async (err: unknown) => {
        // أخطاء التنزيل `Blob` فيُستخرج نصها (كـ429 الحارس) بدل رسالة عامة.
        setExportMsg(await getDownloadErrorMessage(err));
      })
      .finally(() => setExporting(false));
  };

  const colSpan =
    7 +
    (canSeeAssignedLawyer ? 1 : 0) +
    (canSeeAdministrativeBranch ? 1 : 0);

  return {
    isMobile,
    query,
    setQuery,
    status,
    setStatus,
    setStatusTouched,
    applicant,
    setApplicant,
    court,
    setCourt,
    lawyer,
    setLawyer,
    administrativeBranch,
    setAdministrativeBranch,
    executedEntity,
    setExecutedEntity,
    publicEntityBranch,
    setPublicEntityBranch,
    applicants,
    courts,
    lawyers,
    administrativeBranches,
    executedEntities,
    publicEntityBranches,
    canSeeAdministrativeBranch,
    canSeeAssignedLawyer,
    canSearchByLawyer,
    canCreate,
    canViewDeleted,
    canRotate,
    exporting,
    exportMsg,
    exportExcel,
    resetPaging,
    listQuery,
    loading,
    data,
    items,
    loadMore,
    loadingMore,
    loadMoreError,
    effectiveNextCursor,
    focusId,
    focusName,
    focusVisible,
    actionsDocId,
    setActionsDocId,
    openDocument,
    colSpan,
  };
}

export type DocumentsListState = ReturnType<typeof useDocumentsList>;
