import type { AxiosError } from 'axios';

export interface NumberReuseWarning {
  message: string;
  warningToken: string;
}

type WarningBody = {
  code?: string;
  message?: string;
  warningToken?: string;
};

/**
 * يستخرج تحذير إعادة استخدام رقم الأساس من استجابة 409 (`number_reuse_warning`)
 * بلا كشف معرّف/اسم/حالة الملف الآخر — الرسالة نفسها تحمل النطاق فقط.
 */
export function getNumberReuseWarning(error: unknown): NumberReuseWarning | null {
  const body = (error as AxiosError<WarningBody>)?.response?.data;
  if (!body || body.code !== 'number_reuse_warning' || !body.warningToken) return null;
  return {
    message: body.message || 'تأكد من الرقم؛ لأنه مستخدم في ملف آخر. هل تريد إدخاله على أي حال؟',
    warningToken: body.warningToken,
  };
}
