import { describe, it, expect } from 'vitest';
import {
  EXECUTED_STATUS_EXECUTED,
  EXEC_STATUS_DEFERRED,
  EXEC_STATUS_DELEGATION_EXECUTED,
  EXEC_STATUS_FORCIBLY,
  EXEC_STATUS_RECOVERED,
  EXEC_STATUS_REFERRED_TO_START,
  EXEC_STATUS_SETTLED,
  EXEC_STATUS_STRUCK_OFF,
  STATE_CIRCULATING,
  STATE_DRAFT,
  STATUS_ACTION_COMPLETE_SALE,
  STATUS_ACTION_RETURN_CIRCULATING,
  STATUS_ACTION_RETURN_PARTIAL,
  STATUS_ACTION_REVERT,
  STATUS_OPTIONS,
  SUB_STATUS_FULL,
  SUB_STATUS_PARTIAL,
  allowedTargetsOf,
  canDelegateSource,
  currentExecutedLabelOf,
  currentStateOf,
  executedStatusValue,
  filterTargetsByPartial,
  getDocumentBadge,
  getDocumentStatus,
  getExecutedStatus,
  isCirculatingTarget,
  isPartialExecSubStatus,
  isStatusOption,
} from './documentStatus';
import type { DocumentResponse } from '../types';

function doc(overrides: Partial<Pick<DocumentResponse, 'execStatus' | 'execSubStatus' | 'isDraft' | 'generalEntitySide'>>) {
  return { execStatus: '', execSubStatus: '', isDraft: false, generalEntitySide: 'applicant' as const, ...overrides };
}

describe('getDocumentStatus', () => {
  it('يرجّع الحالة التنفيذية الجديدة عند وجودها', () => {
    expect(getDocumentStatus(doc({ execStatus: EXEC_STATUS_SETTLED, isDraft: true }))).toBe(
      EXECUTED_STATUS_EXECUTED,
    );
    expect(
      getDocumentStatus(doc({ execStatus: EXEC_STATUS_FORCIBLY, execSubStatus: SUB_STATUS_FULL })),
    ).toBe(EXECUTED_STATUS_EXECUTED);
    expect(
      getDocumentStatus(doc({ execStatus: EXEC_STATUS_FORCIBLY, execSubStatus: SUB_STATUS_PARTIAL })),
    ).toBe('متداول / منفذ جزئيا');
    expect(getDocumentStatus(doc({ execStatus: EXEC_STATUS_DEFERRED, isDraft: false }))).toBe(
      EXEC_STATUS_DEFERRED,
    );
  });

  it('يرجّع «مسترد» كحالة عرض مستقلة للمناب المسترد (ويعطيه شارة فوشيا)', () => {
    expect(getDocumentStatus(doc({ execStatus: EXEC_STATUS_RECOVERED }))).toBe(EXEC_STATUS_RECOVERED);
    expect(getDocumentStatus(doc({ execStatus: EXEC_STATUS_RECOVERED, isDraft: true }))).toBe(
      EXEC_STATUS_RECOVERED,
    );
    expect(getDocumentBadge(doc({ execStatus: EXEC_STATUS_RECOVERED }))).toEqual({
      text: EXEC_STATUS_RECOVERED,
      cls: 'bg-fuchsia-100 text-fuchsia-800',
    });
  });

  it('يرجّع «محال الى البداية» كحالة عرض مستقلة بشارة بنفسجية (بلا طيّ في «منفذ» حتى مع جزئيته)', () => {
    expect(getDocumentStatus(doc({ execStatus: EXEC_STATUS_REFERRED_TO_START }))).toBe(
      EXEC_STATUS_REFERRED_TO_START,
    );
    expect(
      getDocumentStatus(
        doc({ execStatus: EXEC_STATUS_REFERRED_TO_START, execSubStatus: SUB_STATUS_PARTIAL }),
      ),
    ).toBe(EXEC_STATUS_REFERRED_TO_START);
    expect(getDocumentBadge(doc({ execStatus: EXEC_STATUS_REFERRED_TO_START }))).toEqual({
      text: EXEC_STATUS_REFERRED_TO_START,
      cls: 'bg-purple-100 text-purple-700',
    });
  });

  it('يرجّع «تحت رفع» للمسودة و«متداول» للمتداول دون حالة تنفيذية', () => {
    expect(getDocumentStatus(doc({ isDraft: true }))).toBe(STATE_DRAFT);
    expect(getDocumentStatus(doc({ isDraft: false }))).toBe(STATE_CIRCULATING);
  });

  it('يعامل «منفذ إنابة» كحالة منفذة نهائية (شاشة «منفذ») حتى لو بقي مسودةً', () => {
    expect(getDocumentStatus(doc({ execStatus: EXEC_STATUS_DELEGATION_EXECUTED }))).toBe(
      EXECUTED_STATUS_EXECUTED,
    );
    expect(
      getDocumentStatus(doc({ execStatus: EXEC_STATUS_DELEGATION_EXECUTED, isDraft: true })),
    ).toBe(EXECUTED_STATUS_EXECUTED);
    expect(getDocumentBadge(doc({ execStatus: EXEC_STATUS_DELEGATION_EXECUTED }))).toEqual({
      text: EXECUTED_STATUS_EXECUTED,
      cls: 'bg-green-100 text-green-700',
    });
  });

  it('يعطي كل حالة شارة العرض الصحيحة', () => {
    expect(getDocumentBadge(doc({ execStatus: EXEC_STATUS_SETTLED }))).toEqual({
      text: EXECUTED_STATUS_EXECUTED,
      cls: 'bg-green-100 text-green-700',
    });
    expect(
      getDocumentBadge(doc({ execStatus: EXEC_STATUS_FORCIBLY, execSubStatus: SUB_STATUS_PARTIAL })),
    ).toEqual({
      text: 'متداول / منفذ جزئيا',
      cls: 'bg-cyan-100 text-cyan-700',
    });
    expect(getDocumentBadge(doc({ isDraft: true }))).toEqual({
      text: STATE_DRAFT,
      cls: 'bg-amber-100 text-amber-700',
    });
    expect(getDocumentBadge(doc({ isDraft: false }))).toEqual({
      text: STATE_CIRCULATING,
      cls: 'bg-blue-100 text-blue-700',
    });
  });
});

describe('getExecutedStatus', () => {
  const executed = (executedStatus: string) => ({
    execStatus: '',
    execSubStatus: '',
    isDraft: false,
    generalEntitySide: 'executed' as const,
    executedStatus,
  });

  it('يعزل حالة وضع «منفذ عليه» عن نظام «طالبة تنفيذ»', () => {
    expect(getExecutedStatus(executed(''))).toBe(STATE_CIRCULATING);
    expect(getExecutedStatus(executed(EXECUTED_STATUS_EXECUTED))).toBe(EXECUTED_STATUS_EXECUTED);
    expect(getExecutedStatus(executed(EXEC_STATUS_STRUCK_OFF))).toBe(EXEC_STATUS_STRUCK_OFF);
  });

  it('getDocumentStatus يعالج ملفات الصفة executed بحالتها المعزولة حتى لو حملت execStatus قديمًا', () => {
    expect(
      getDocumentStatus({
        ...executed(EXEC_STATUS_STRUCK_OFF),
        execStatus: EXEC_STATUS_SETTLED,
        isDraft: true,
      }),
    ).toBe(EXEC_STATUS_STRUCK_OFF);
    expect(
      getDocumentStatus({ ...executed(''), execStatus: EXEC_STATUS_DEFERRED, isDraft: true }),
    ).toBe(STATE_CIRCULATING);
  });

  it('getDocumentStatus يعالج ملفات صفة «عرض وايداع» بحالتها المعزولة مثل executed', () => {
    expect(
      getDocumentStatus({
        ...executed(EXECUTED_STATUS_EXECUTED),
        generalEntitySide: 'deposit' as const,
      }),
    ).toBe(EXECUTED_STATUS_EXECUTED);
    expect(
      getDocumentStatus({
        ...executed(''),
        generalEntitySide: 'deposit' as const,
        execStatus: EXEC_STATUS_DEFERRED,
        isDraft: true,
      }),
    ).toBe(STATE_CIRCULATING);
  });

  it('يعطي شارة العرض الصحيحة للمشطوب', () => {
    expect(getDocumentBadge(executed(EXEC_STATUS_STRUCK_OFF))).toEqual({
      text: EXEC_STATUS_STRUCK_OFF,
      cls: 'bg-gray-200 text-gray-700',
    });
  });
});

describe('canDelegateSource', () => {
  it('«منفذ جبريا (منفذ جزئيا)» يبقى قابلًا للتسطير (قرار 9)', () => {
    expect(
      canDelegateSource(doc({ execStatus: EXEC_STATUS_FORCIBLY, execSubStatus: SUB_STATUS_PARTIAL })),
    ).toBe(true);
  });

  it('يمنع التسطير على المسودة وصفة المنفذين والشطب والتريث والمسترد', () => {
    expect(canDelegateSource(doc({ isDraft: true }))).toBe(false);
    expect(
      canDelegateSource(doc({ execStatus: EXEC_STATUS_FORCIBLY, execSubStatus: SUB_STATUS_FULL })),
    ).toBe(false);
    expect(canDelegateSource(doc({ execStatus: EXEC_STATUS_SETTLED }))).toBe(false);
    expect(canDelegateSource(doc({ execStatus: EXEC_STATUS_DELEGATION_EXECUTED }))).toBe(false);
    expect(canDelegateSource(doc({ execStatus: EXEC_STATUS_STRUCK_OFF }))).toBe(false);
    expect(canDelegateSource(doc({ execStatus: EXEC_STATUS_DEFERRED }))).toBe(false);
    expect(canDelegateSource(doc({ execStatus: EXEC_STATUS_RECOVERED }))).toBe(false);
    expect(canDelegateSource(doc({ execStatus: EXEC_STATUS_REFERRED_TO_START }))).toBe(false);
  });

  it('يمنع التسطير على ملفات صفة «منفذ عليه»/«عرض وايداع» ولو كانت متداولة', () => {
    expect(canDelegateSource(doc({ generalEntitySide: 'executed' as const }))).toBe(false);
    expect(canDelegateSource(doc({ generalEntitySide: 'deposit' as const }))).toBe(false);
  });

  it('يسمح بالتسطير للمتداول فقط (وليس التريث/المسترد)', () => {
    expect(canDelegateSource(doc({}))).toBe(true);
  });
});

describe('STATUS_OPTIONS', () => {
  it('يستبعد «محال الى البداية» كسابقه «منفذ» (صفحته الخاصة): بلا منفذ/مشطوب/الجزئية المركبة', () => {
    expect(STATUS_OPTIONS).not.toContain(EXEC_STATUS_REFERRED_TO_START);
    expect(STATUS_OPTIONS).toEqual([EXEC_STATUS_DEFERRED, STATE_DRAFT, STATE_CIRCULATING]);
    expect(STATUS_OPTIONS).not.toContain(EXECUTED_STATUS_EXECUTED);
    expect(STATUS_OPTIONS).not.toContain(EXEC_STATUS_STRUCK_OFF);
    expect(STATUS_OPTIONS).not.toContain('متداول / منفذ جزئيا');
  });

  it('isStatusOption يقرّ الخيارات المعتمدة فقط ويعقم الباقي («محال الى البداية» وغيره)', () => {
    expect(isStatusOption(EXEC_STATUS_DEFERRED)).toBe(true);
    expect(isStatusOption(STATE_CIRCULATING)).toBe(true);
    expect(isStatusOption('')).toBe(false);
    expect(isStatusOption(EXEC_STATUS_REFERRED_TO_START)).toBe(false);
    expect(isStatusOption(EXECUTED_STATUS_EXECUTED)).toBe(false);
    expect(isStatusOption('قيمة عشوائية')).toBe(false);
  });
});

describe('currentStateOf (نقطة انطلاق نافذة «تغيير الحالة»)', () => {
  it('يطابق الحالات الخام بلا تفضيل displayStatus (بخلاف getDocumentStatus)', () => {
    expect(currentStateOf(doc({ execStatus: EXEC_STATUS_DEFERRED }))).toBe(EXEC_STATUS_DEFERRED);
    expect(currentStateOf(doc({ execStatus: EXEC_STATUS_RECOVERED }))).toBe(EXEC_STATUS_RECOVERED);
    expect(currentStateOf(doc({ execStatus: EXEC_STATUS_REFERRED_TO_START }))).toBe(
      EXEC_STATUS_REFERRED_TO_START,
    );
    expect(currentStateOf(doc({ execStatus: EXEC_STATUS_SETTLED }))).toBe(EXEC_STATUS_SETTLED);
    expect(currentStateOf(doc({ execStatus: EXEC_STATUS_FORCIBLY }))).toBe(EXEC_STATUS_FORCIBLY);
  });

  it('يقدّم «مشطوب» من الجهتين ويعود للمسودة/المتداول عند الفراغ', () => {
    expect(currentStateOf(doc({ execStatus: EXEC_STATUS_STRUCK_OFF }))).toBe(EXEC_STATUS_STRUCK_OFF);
    expect(
      currentStateOf({ ...doc({}), executedStatus: EXEC_STATUS_STRUCK_OFF } as unknown as Parameters<
        typeof currentStateOf
      >[0]),
    ).toBe(EXEC_STATUS_STRUCK_OFF);
    expect(currentStateOf(doc({ isDraft: true }))).toBe(STATE_DRAFT);
    expect(currentStateOf(doc({ isDraft: false }))).toBe(STATE_CIRCULATING);
  });

  it('القيم الفارغة وnull تُعامل متداولًا (لا تسقط في فرع خاطئ)', () => {
    expect(currentStateOf(doc({ execStatus: '' }))).toBe(STATE_CIRCULATING);
    expect(
      currentStateOf(doc({ execStatus: null as unknown as string, isDraft: false })),
    ).toBe(STATE_CIRCULATING);
  });
});

describe('allowedTargetsOf + filterTargetsByPartial (آلة الانتقال)', () => {
  it('من المتداول كل الانتقالات المسموحة، ومن تحت الرفع تريث/تسوية فقط', () => {
    expect(allowedTargetsOf(STATE_CIRCULATING, false, false)).toEqual([
      EXEC_STATUS_DEFERRED,
      EXEC_STATUS_SETTLED,
      EXEC_STATUS_FORCIBLY,
      EXEC_STATUS_STRUCK_OFF,
      EXEC_STATUS_REFERRED_TO_START,
    ]);
    expect(allowedTargetsOf(STATE_DRAFT, false, false)).toEqual([
      EXEC_STATUS_DEFERRED,
      EXEC_STATUS_SETTLED,
    ]);
  });

  it('من التريث تسوية وتراجع ومحال، ومن التسوية تراجع فقط', () => {
    expect(allowedTargetsOf(EXEC_STATUS_DEFERRED, false, false)).toEqual([
      EXEC_STATUS_SETTLED,
      STATUS_ACTION_REVERT,
      EXEC_STATUS_REFERRED_TO_START,
    ]);
    expect(allowedTargetsOf(EXEC_STATUS_SETTLED, false, false)).toEqual([STATUS_ACTION_REVERT]);
  });

  it('من الجبري تراجع ومحال وإكمال بيع، مع فلتر الجزئية للحمل الختامي', () => {
    const raw = allowedTargetsOf(EXEC_STATUS_FORCIBLY, false, true);
    expect(raw).toEqual([
      STATUS_ACTION_REVERT,
      EXEC_STATUS_REFERRED_TO_START,
      STATUS_ACTION_COMPLETE_SALE,
    ]);
    expect(filterTargetsByPartial(raw, EXEC_STATUS_FORCIBLY, true)).toEqual(raw);
    expect(filterTargetsByPartial(raw, EXEC_STATUS_FORCIBLY, false)).toEqual([STATUS_ACTION_REVERT]);
  });

  it('من المحال عودة واحدة بحسب الجزئية (اللازمة)، والترشيح يحفظها', () => {
    expect(allowedTargetsOf(EXEC_STATUS_REFERRED_TO_START, false, false)).toEqual([
      STATUS_ACTION_RETURN_CIRCULATING,
    ]);
    expect(allowedTargetsOf(EXEC_STATUS_REFERRED_TO_START, false, true)).toEqual([
      STATUS_ACTION_RETURN_PARTIAL,
    ]);
    expect(
      filterTargetsByPartial(
        [STATUS_ACTION_RETURN_CIRCULATING, STATUS_ACTION_RETURN_PARTIAL],
        EXEC_STATUS_REFERRED_TO_START,
        false,
      ),
    ).toEqual([STATUS_ACTION_RETURN_CIRCULATING]);
  });

  it('الملف المناب: متداول يُشطب فحسب، وغيره بلا انتقالات', () => {
    expect(allowedTargetsOf(STATE_CIRCULATING, true, false)).toEqual([EXEC_STATUS_STRUCK_OFF]);
    expect(allowedTargetsOf(EXEC_STATUS_DEFERRED, true, false)).toEqual([]);
    expect(allowedTargetsOf(EXEC_STATUS_RECOVERED, true, false)).toEqual([]);
    expect(allowedTargetsOf(EXEC_STATUS_STRUCK_OFF, true, false)).toEqual([]);
  });

  it('الحالات النهائية والمجهولة بلا انتقالات (لا تسرب لخيارات وهمية)', () => {
    expect(allowedTargetsOf(EXEC_STATUS_STRUCK_OFF, false, false)).toEqual([]);
    expect(allowedTargetsOf(EXEC_STATUS_RECOVERED, false, false)).toEqual([]);
    expect(allowedTargetsOf('قيمة عشوائية', false, false)).toEqual([]);
    expect(allowedTargetsOf('', false, false)).toEqual([]);
  });
});

describe('currentExecutedLabelOf + executedStatusValue (عائلة «منفذ عليه»)', () => {
  it('يقرأ منفذ/مشطوب/متداول من executedStatus فقط', () => {
    expect(currentExecutedLabelOf({ executedStatus: EXECUTED_STATUS_EXECUTED })).toBe(
      EXECUTED_STATUS_EXECUTED,
    );
    expect(currentExecutedLabelOf({ executedStatus: EXEC_STATUS_STRUCK_OFF })).toBe(
      EXEC_STATUS_STRUCK_OFF,
    );
    expect(currentExecutedLabelOf({ executedStatus: '' })).toBe(STATE_CIRCULATING);
    expect(currentExecutedLabelOf({} as { executedStatus?: string })).toBe(STATE_CIRCULATING);
  });

  it('المتداول تُرسل سلسلة فارغة، وغيرها يُرسل كما هو', () => {
    expect(executedStatusValue(STATE_CIRCULATING)).toBe('');
    expect(executedStatusValue(EXECUTED_STATUS_EXECUTED)).toBe(EXECUTED_STATUS_EXECUTED);
    expect(executedStatusValue(EXEC_STATUS_STRUCK_OFF)).toBe(EXEC_STATUS_STRUCK_OFF);
  });
});

describe('isPartialExecSubStatus + isCirculatingTarget (حراس دقيقة)', () => {
  it('الجزئية تطابق الثابت فقط (بما فيها null/الفارغ)', () => {
    expect(isPartialExecSubStatus(SUB_STATUS_PARTIAL)).toBe(true);
    expect(isPartialExecSubStatus(SUB_STATUS_FULL)).toBe(false);
    expect(isPartialExecSubStatus('')).toBe(false);
    expect(isPartialExecSubStatus(null)).toBe(false);
    expect(isPartialExecSubStatus(undefined)).toBe(false);
  });

  it('المتداول الهدف هو null أو الفراغ فقط', () => {
    expect(isCirculatingTarget({ execStatus: '' })).toBe(true);
    expect(isCirculatingTarget({ execStatus: null as unknown as string })).toBe(true);
    expect(isCirculatingTarget({ execStatus: undefined as unknown as string })).toBe(true);
    expect(isCirculatingTarget({ execStatus: EXEC_STATUS_DEFERRED })).toBe(false);
    expect(isCirculatingTarget({ execStatus: EXEC_STATUS_FORCIBLY })).toBe(false);
  });
});
