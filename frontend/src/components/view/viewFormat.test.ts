import { describe, it, expect } from 'vitest';
import { buildStatusSummary, occurrenceLine } from './viewFormat';
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
