/**
 * مفتاح تعطيل توليد مستندات Word مؤقتًا لحين توحيد النماذج المولدة.
 * مطابق لمفتاح الخلفية `DocumentGeneration:Enabled=false` ورسالته.
 * عند اكتمال التوحيد تُفعَّل القيمة هنا وفي `appsettings.json` معًا.
 */
export const DOCUMENT_GENERATION_ENABLED: boolean = false;

export const DOCUMENT_GENERATION_DISABLED_MESSAGE =
  'الميزة معطلة حاليًا لحين توحيد النماذج المولدة';
