import { describe, it, expect } from 'vitest';
import { buildStatusSummary, identityFileNumber, occurrenceLine } from './viewFormat';
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

describe('occurrenceLine (إحالة نقل دوائر — C3)', () => {
  it('يسرد الإحالة الجماعية بالأرقام القديمة ومسار الدائرتين', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'circuit-referred',
          occurrenceTypeLabel: 'إحالة نقل دائرة',
          fileNumber: '66',
          fileType: 'صلح',
          year: 2026,
          fromCircuitName: 'دائرة المصدر',
          toCircuitName: 'دائرة الوجهة',
          details: { fromCircuit: 'دائرة المصدر', toCircuit: 'دائرة الوجهة', oldNumber: '66' },
        }),
      ),
    ).toBe('تم نقل هذا الملف رقم 66 نوع صلح لعام 2026 من دائرة «دائرة المصدر» إلى دائرة «دائرة الوجهة»');
  });

  it('يلحق ذيل تغيّر دائرة إنابة المنيب عند وجوده', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'circuit-referred',
          occurrenceTypeLabel: 'إحالة نقل دائرة',
          fileNumber: '66',
          fileType: 'صلح',
          year: 2026,
          fromCircuitName: 'دائرة المصدر',
          toCircuitName: 'دائرة الوجهة',
          details: { fromCircuit: 'دائرة المصدر', toCircuit: 'دائرة الوجهة', delegationCircuitChange: 'من «دائرة أ» إلى «دائرة ب»' },
        }),
      ),
    ).toBe(
      'تم نقل هذا الملف رقم 66 نوع صلح لعام 2026 من دائرة «دائرة المصدر» إلى دائرة «دائرة الوجهة» — وتغيّرت بذلك دائرة إنابة الملف المنيب: من «دائرة أ» إلى «دائرة ب»',
    );
  });

  it('يسرد تبديل الدائرة من التعديل العادي (via=edit) بالأرقام الحالية', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'circuit-referred',
          occurrenceTypeLabel: 'إحالة نقل دائرة',
          fileNumber: '101',
          fileType: 'حقوق',
          year: 2026,
          fromCircuitName: 'دائرة السابق',
          toCircuitName: 'دائرة الجديد',
          details: { fromCircuit: 'دائرة السابق', toCircuit: 'دائرة الجديد', via: 'edit' },
        }),
      ),
    ).toBe('تم تبديل دائرة الملف من دائرة «دائرة السابق» إلى دائرة «دائرة الجديد» — على الرقم 101 حقوق 2026');
  });

  it('يسرد وقعتي الطرف المقابل (منيب→مناب ومناب→منيب) بنمط متجانس', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'source-file-moved',
          occurrenceTypeLabel: 'نقل الملف المنيب',
          fileNumber: '66',
          fileType: 'صلح',
          year: 2026,
          fromCircuitName: 'دائرة المصدر',
          toCircuitName: 'دائرة الوجهة',
          details: { fromCircuit: 'دائرة المصدر', toCircuit: 'دائرة الوجهة' },
        }),
      ),
    ).toBe('تم نقل الملف المنيب رقم 66 نوع صلح لعام 2026 من دائرة «دائرة المصدر» إلى دائرة «دائرة الوجهة»');
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'mirror-file-moved',
          occurrenceTypeLabel: 'نقل الملف المناب',
          fileNumber: '66',
          fileType: 'صلح',
          year: 2026,
          fromCircuitName: 'دائرة المصدر',
          toCircuitName: 'دائرة الوجهة',
          details: { fromCircuit: 'دائرة المصدر', toCircuit: 'دائرة الوجهة' },
        }),
      ),
    ).toBe('تم نقل الملف المناب رقم 66 نوع صلح لعام 2026 من دائرة «دائرة المصدر» إلى دائرة «دائرة الوجهة»');
  });

  it('يتجاوز الحقول الناقصة بلا فراغات زائدة', () => {
    expect(
      occurrenceLine(
        occurrence({
          occurrenceType: 'source-file-moved',
          occurrenceTypeLabel: 'نقل الملف المنيب',
          fromCircuitName: 'دائرة المصدر',
          toCircuitName: 'دائرة الوجهة',
        }),
      ),
    ).toBe('تم نقل الملف المنيب من دائرة «دائرة المصدر» إلى دائرة «دائرة الوجهة»');
  });

  it('صف الإرث (بلا details ولا حقول الوقعة) يبقى على السرْد الموحّد بلا انفجار', () => {
    // F1: صف وقعة قديمة وصل قبل تعريف أي مفاتيح (لا details ولا آثار) — يسقط
    // إلى مقطع الافتتاح وحده من السطر الموحّد (صفر أثر جانبي على التسمية/البطاقات).
    expect(
      occurrenceLine({
        id: 1,
        occurrenceType: 'circuit-referred',
        occurrenceTypeLabel: 'إحالة نقل دائرة',
      }),
    ).toBe('تم نقل هذا الملف');
  });
});

describe('identityFileNumber (بطاقة «رقم الملف والسنة» المدمجة)', () => {
  it('يدمج الرقم والسنة بصيغة «66 / 2026»', () => {
    expect(identityFileNumber(targetDoc({ fileNumber: '66', fileYear: '2026' }))).toBe('66 / 2026');
  });

  it('يفضّل displayFileNumber/displayFileYear على الخام', () => {
    expect(
      identityFileNumber(
        targetDoc({
          displayFileNumber: '7',
          fileNumber: '66',
          displayFileYear: '2025',
          fileYear: '2026',
        }),
      ),
    ).toBe('7 / 2025');
  });

  it('يعرض الرقم وحده عند غياب السنة', () => {
    expect(identityFileNumber(targetDoc({ fileNumber: '66', fileYear: '' }))).toBe('66');
  });

  it('يعرض السنة وحدها عند غياب الرقم', () => {
    expect(identityFileNumber(targetDoc({ fileNumber: '  ', fileYear: '2026' }))).toBe('2026');
  });

  it('يعرض «—» عند غياب الرقم والسنة معًا', () => {
    expect(identityFileNumber(targetDoc({ fileNumber: '', fileYear: '' }))).toBe('—');
  });

  it('يعرض «—» للملف تحت الرفع حتى مع وجود رقم خام', () => {
    expect(identityFileNumber(targetDoc({ isDraft: true, fileNumber: '66', fileYear: '2026' }))).toBe(
      '—',
    );
  });

  it('لا يستعير الرقم الخام عند فراغ رقم العرض (مصدر واحد للزوجين)', () => {
    expect(
      identityFileNumber(
        targetDoc({ displayFileNumber: '  ', fileNumber: '66', displayFileYear: '2026' }),
      ),
    ).toBe('2026');
  });

  it('لا يخلط رقم العرض مع سنة القيد الأصلية عند غياب سنة العرض', () => {
    expect(
      identityFileNumber(
        targetDoc({ displayFileNumber: '1500', fileNumber: '99', fileYear: '2024' }),
      ),
    ).toBe('1500');
  });
});
