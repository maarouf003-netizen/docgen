import { describe, it, expect } from 'vitest';
import { getApiErrorMessage } from './client';

describe('getApiErrorMessage', () => {
  it('يعيد رسالة تعذر الاتصال عند خطأ شبكة دون استجابة', () => {
    expect(getApiErrorMessage({ isAxiosError: true, response: undefined })).toBe(
      'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة',
    );
  });

  it('يعيد رسالة صلاحية عند 403', () => {
    expect(getApiErrorMessage({ isAxiosError: true, response: { status: 403, data: {} } })).toBe(
      'لا تملك صلاحية تنفيذ هذا الإجراء',
    );
  });

  it('يعيد رسالة الخادم عند خطأ 400 يحمل message', () => {
    expect(
      getApiErrorMessage({
        isAxiosError: true,
        response: { status: 400, data: { message: 'حالة غير صالحة' } },
      }),
    ).toBe('حالة غير صالحة');
  });

  it('يعيد رسالة عامة عند خطأ غير معروف', () => {
    expect(getApiErrorMessage(new Error('something'))).toBe('حدث خطأ غير متوقع');
  });
});
