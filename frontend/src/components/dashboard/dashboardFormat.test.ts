import { describe, it, expect } from 'vitest';
import {
  arabicCount,
  deltaPercent,
  firstName,
  formatDelta,
  isDueTodayOrOverdue,
  previousSelection,
  trendDirectionText,
  zeroFilledMonthlyCounts,
} from './dashboardFormat';

describe('firstName', () => {
  it('يستخرج أول كلمة مع تطبيع الفراغات', () => {
    expect(firstName('أحمد محمد الخطيب')).toBe('أحمد');
    expect(firstName('  أحمد   الخطيب  ')).toBe('أحمد');
    expect(firstName('محامي')).toBe('محامي');
  });

  it('يُرجع فارغًا عند غياب الاسم', () => {
    expect(firstName(null)).toBe('');
    expect(firstName(undefined)).toBe('');
    expect(firstName('   ')).toBe('');
  });
});

describe('previousSelection', () => {
  it('يحسب الشهر السابق مع لفّ كانون الثاني', () => {
    expect(previousSelection({ year: 2026, month: 8 }, 'monthly')).toEqual({ year: 2026, month: 7 });
    expect(previousSelection({ year: 2026, month: 1 }, 'monthly')).toEqual({ year: 2025, month: 12 });
  });

  it('يحسب الربع السابق مع اللفّ', () => {
    expect(previousSelection({ year: 2026, quarter: 3 }, 'quarterly')).toEqual({ year: 2026, quarter: 2 });
    expect(previousSelection({ year: 2026, quarter: 1 }, 'quarterly')).toEqual({ year: 2025, quarter: 4 });
  });

  it('يحسب السنة السابقة ويحترم حد 1900', () => {
    expect(previousSelection({ year: 2026 }, 'yearly')).toEqual({ year: 2025 });
    expect(previousSelection({ year: 1900 }, 'yearly')).toBeNull();
    expect(previousSelection({ year: 1900, month: 1 }, 'monthly')).toBeNull();
  });

  it('يُرجع null عند غياب التحديد أو عدم تطابق النطاق', () => {
    expect(previousSelection(null, 'monthly')).toBeNull();
    expect(previousSelection({ year: 2026 }, 'monthly')).toBeNull();
  });
});

describe('deltaPercent', () => {
  it('يحسب النسبة الصحيحة بإشارة صريحة', () => {
    expect(deltaPercent(5, 2)).toBe(150);
    expect(deltaPercent(2, 4)).toBe(-50);
    expect(deltaPercent(3, 3)).toBe(0);
  });

  it('يُرجع null عند غياب السابق أو صفره', () => {
    expect(deltaPercent(5, null)).toBeNull();
    expect(deltaPercent(5, undefined)).toBeNull();
    expect(deltaPercent(5, 0)).toBeNull();
  });

  it('ينسّق الدلتا بإشارة ومنزلة عشرية واحدة', () => {
    expect(formatDelta(150)).toBe('+150%');
    expect(formatDelta(-12.55)).toBe('-12.6%');
    expect(formatDelta(0)).toBe('0%');
  });
});

describe('zeroFilledMonthlyCounts', () => {
  it('يملأ الفجوات بالأصفار بين أول شهر وآخره', () => {
    const out = zeroFilledMonthlyCounts([
      { year: 2026, month: 3, count: 4 },
      { year: 2026, month: 1, count: 2 },
    ]);
    expect(out).toEqual([
      { year: 2026, month: 1, count: 2 },
      { year: 2026, month: 2, count: 0 },
      { year: 2026, month: 3, count: 4 },
    ]);
  });

  it('يعبر حد السنة ويُرجع فارغًا بلا بيانات', () => {
    const out = zeroFilledMonthlyCounts([
      { year: 2025, month: 12, count: 1 },
      { year: 2026, month: 2, count: 1 },
    ]);
    expect(out.map((p) => `${p.year}-${p.month}`)).toEqual(['2025-12', '2026-1', '2026-2']);
    expect(zeroFilledMonthlyCounts([])).toEqual([]);
  });
});

describe('arabicCount', () => {
  it('يستخدم المفرد التام عند 1 والعدد مع الجمع فوقه', () => {
    expect(arabicCount(1, 'مراسلة عاجلة واحدة', 'مراسلات عاجلة')).toBe('مراسلة عاجلة واحدة');
    expect(arabicCount(2, 'مراسلة عاجلة واحدة', 'مراسلات عاجلة')).toBe('2 مراسلات عاجلة');
    expect(arabicCount(5, 'جهة واحدة بانتظار المراجعة', 'جهات بانتظار المراجعة')).toBe(
      '5 جهات بانتظار المراجعة',
    );
  });
});

describe('trendDirectionText', () => {
  it('يقارن آخر نقطة بالأولى: ارتفاع/انخفاض/ثبات', () => {
    expect(trendDirectionText([{ count: 2 }, { count: 5 }])).toBe('ارتفاع');
    expect(trendDirectionText([{ count: 5 }, { count: 2 }])).toBe('انخفاض');
    expect(trendDirectionText([{ count: 3 }, { count: 0 }, { count: 3 }])).toBe('ثبات');
  });

  it('يُرجع «نقطة واحدة» عند غياب السلسلة أو فرادتها', () => {
    expect(trendDirectionText([])).toBe('نقطة واحدة');
    expect(trendDirectionText([{ count: 4 }])).toBe('نقطة واحدة');
  });
});

describe('isDueTodayOrOverdue', () => {
  it('يُرجع صحيحًا لليوم والماضي وخطأ للمستقبل والفاسد', () => {
    const today = new Date();
    const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    expect(isDueTodayOrOverdue(iso)).toBe(true);
    expect(isDueTodayOrOverdue('2000-01-01')).toBe(true);
    expect(isDueTodayOrOverdue('2999-01-01')).toBe(false);
    expect(isDueTodayOrOverdue('ليس تاريخًا')).toBe(false);
    expect(isDueTodayOrOverdue('')).toBe(false);
  });
});
