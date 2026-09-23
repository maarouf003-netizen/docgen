import { describe, it, expect } from 'vitest';
import { buildStatusSummary, occurrenceLine } from './viewFormat';
import { formatDate } from '../../utils/dates';
import type { DocumentOccurrenceDto, DocumentResponse } from '../../types';

function targetDoc(overrides: Record<string, unknown>): DocumentResponse {
  return {
    sourceDelegationId: 9,
    execStatus: '',
    occurrences: [],
    ...overrides,
  } as unknown as DocumentResponse;
}

function occurrence(overrides: Partial<DocumentOccurrenceDto>): DocumentOccurrenceDto {
  return { id: 1, occurrenceType: 'recovered', occurrenceTypeLabel: 'استرداد', ...overrides };
}

describe('buildStatusSummary (فروع L5 للملف المناب)', () => {
  it('يقدّم «تريث تبعًا للملف المنيب» مع اسم المنيب وكتاب التريث', () => {
    const summary = buildStatusSummary(
      targetDoc({ execStatus: 'تريث', tarithNumber: '77', tarithDate: '1/8/2026' }),
      { sourceLabel: 'أحمد محمد خالد' },
    );
    expect(summary).toBe(
      'تريث تبعًا للملف المنيب (أحمد محمد خالد) بموجب كتاب التريث رقم 77 بتاريخ 1/8/2026',
    );
  });

  it('يحذف قوس اسم المنيب عند غيابه دون فراغات زائدة', () => {
    const summary = buildStatusSummary(
      targetDoc({ execStatus: 'تريث', tarithNumber: '77', tarithDate: '1/8/2026' }),
    );
    expect(summary).toBe('تريث تبعًا للملف المنيب بموجب كتاب التريث رقم 77 بتاريخ 1/8/2026');
  });

  it('يقرأ سبب «مسترد» من وقعة الاسترداد الأخيرة (تسوية ببراءة الذمة)', () => {
    const summary = buildStatusSummary(
      targetDoc({
        execStatus: 'مسترد',
        occurrences: [
          occurrence({
            details: { recoveryReason: 'منفذ بالتسوية', baraetNumber: '5', baraetDate: '2/8/2026' },
          }),
        ],
      }),
      { sourceLabel: 'أحمد محمد خالد' },
    );
    expect(summary).toBe(
      'مسترد لاعتبار الملف المنيب (أحمد محمد خالد) منفذًا بالتسوية بكتاب براءة الذمة رقم 5',
    );
  });

  it('يسرد «مسترد» الجبري بتحصيل المبلغ في الملف المنيب', () => {
    const summary = buildStatusSummary(
      targetDoc({
        execStatus: 'مسترد',
        occurrences: [occurrence({ details: { recoveryReason: 'منفذ جبريا' } })],
      }),
      { sourceLabel: 'أحمد محمد خالد' },
    );
    expect(summary).toBe(
      'مسترد لاعتبار الملف المنيب (أحمد محمد خالد) منفذًا جبريًا كاملًا — بتحصيل المبلغ المطالب به جبريًا في الملف المنيب',
    );
  });

  it('يسقط «مسترد» بلا وقعة استرداد على الفرع الجبري دفاعًا', () => {
    const summary = buildStatusSummary(targetDoc({ execStatus: 'مسترد' }), {
      sourceLabel: 'أحمد محمد خالد',
    });
    expect(summary).toContain('منفذًا جبريًا كاملًا');
  });
});

describe('occurrenceLine (وقعة recovered — L4)', () => {
  it('يسرد استرداد التسوية ببراءة الذمة رقمًا وتاريخًا', () => {
    expect(
      occurrenceLine(
        occurrence({
          details: { recoveryReason: 'منفذ بالتسوية', baraetNumber: '5', baraetDate: '2/8/2026' },
        }),
      ),
    ).toBe('استرداد الملف المناب لاعتبار الملف المنيب منفذًا ببراءة الذمة رقم 5 تاريخ 2/8/2026');
  });

  it('يسرد استرداد الاكتمال الجبري بتحصيل المبلغ في المنيب', () => {
    expect(occurrenceLine(occurrence({ details: { recoveryReason: 'منفذ جبريا' } }))).toBe(
      'استرداد الملف المناب لاعتبار الملف المنيب منفذًا — بتحصيل المبلغ المطالب به جبريًا في الملف المنيب',
    );
  });
});

describe('buildStatusSummary (محال الى البداية)', () => {
  it('يسرد كتاب المطالعة بعدم وجود أموال وكتاب الإحالة', () => {
    const summary = buildStatusSummary(
      targetDoc({
        execStatus: 'محال الى البداية',
        noFundsDemandNumber: '5',
        noFundsDemandDate: '2026-01-08',
        startReferralNumber: '6',
        startReferralDate: '2026-02-01',
      }),
    );
    expect(summary).toBe(
      `محال إلى قسم البداية لعدم وجود أموال للتنفيذ عليها بموجب كتاب المطالعة رقم 5 بتاريخ ${formatDate('2026-01-08')} وبكتاب الإحالة رقم 6 بتاريخ ${formatDate('2026-02-01')}`,
    );
  });

  it('لا يعرض كتاب الإحالة عند غيابه كاملًا', () => {
    const summary = buildStatusSummary(
      targetDoc({ execStatus: 'محال الى البداية', noFundsDemandNumber: '5', noFundsDemandDate: '2026-01-08' }),
    );
    expect(summary).not.toContain('وبكتاب الإحالة');
  });

  it('يلحق مقطع الجزئية (المحصل والتحويل) لمن دخل من «منفذ جبريا - جزئيا» — مرآة فرع الجبريا', () => {
    const summary = buildStatusSummary(
      targetDoc({
        execStatus: 'محال الى البداية',
        execSubStatus: 'منفذ جزئيا',
        noFundsDemandNumber: '5',
        noFundsDemandDate: '2026-01-08',
        collectedAmount: 750,
        collectedCurrency: 'ليرة سورية',
        forcibleTransferDate: '2026-01-15',
        forcibleTransferNoticeNumber: '44',
      }),
    );
    expect(summary).toContain('(منفذ جزئيا)');
    expect(summary).toContain('المبلغ المحصل: 750 ليرة سورية');
    expect(summary).toContain(`تحويل البدل بتاريخ ${formatDate('2026-01-15')}`);
    expect(summary).toContain('بإشعار رقم 44');
  });
});

describe('occurrenceLine (referred-to-start و revert — B3/B4)', () => {
  it('يسرد الإحالة بكتاب المطالعة بعدم وجود الأموال', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'referred-to-start',
          details: { noFundsDemandNumber: '5', noFundsDemandDate: '2026-01-08' },
        }),
      ),
    ).toBe('محال الى البداية بموجب كتاب المطالعة بعدم وجود أموال رقم 5 بتاريخ 2026-01-08');
  });

  it('يعرض السرد النصي الجاهز لوقعة العودة (revertNarration) بدل مفاتيح كتاب السير', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'revert',
          details: { revertNarration: 'أعيد السير به بعد موافاتنا بأموال للتنفيذ عليها' },
        }),
      ),
    ).toBe('أعيد السير به بعد موافاتنا بأموال للتنفيذ عليها');
  });

  it('يبقي وقعة «تراجع» الكلاسيكية على مفاتيح كتاب السير عند غياب السرد', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'revert',
          details: { sayerNumber: '8', sayerDate: '1/8/2026' },
        }),
      ),
    ).toBe('تراجع عن الحالة بموجب كتاب السير بالملف رقم 8 بتاريخ 1/8/2026');
  });
});
