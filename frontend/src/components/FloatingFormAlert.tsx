interface FloatingFormAlertProps {
  /** نص الخطأ — يُعلن فورًا لقارئ الشاشة (role=alert). */
  message: string;
  /** إغلاق التنبيه يدويًا — الرسالة لا تختفي تلقائيًا (خطأ حقيقي لا إشعار عابر). */
  onDismiss: () => void;
}

/**
 * تنبيه خطأ عائم مستقل عن تخطيط الصفحة: يظهر في منطقة العرض أينما كان موضع
 * المستخدم (النماذج الطويلة التي يكون زر حفظها في الأسفل)، ويبقى حتى يغلقه
 * صاحبه أو يزول الخطأ. مثبت أعلى الشاشة على الجوال والمكتبي معًا، بـ z-index
 * أعلى من شريط التنقل السفلي (z-40) ودرج القائمة (z-50).
 *
 * قرار مقصود: لا `autoFocus` — يعلن نفسه عبر `role="alert"` بلا سلب التركيز
 * من الحقل الذي يعمل عليه المستخدم، ولا حركة إلزامية (يحترم تقليل الحركة).
 */
export default function FloatingFormAlert({ message, onDismiss }: FloatingFormAlertProps) {
  return (
    <div className="fixed inset-x-3 top-3 z-[60] mx-auto max-w-lg rounded-xl border border-red-300 bg-red-50 px-3 py-2.5 text-red-800 shadow-lg sm:max-w-xl sm:px-4">
      <div className="flex items-start gap-2">
        <p
          role="alert"
          aria-atomic="true"
          className="min-w-0 flex-1 text-sm font-bold leading-relaxed break-words"
        >
          {message}
        </p>
        <button
          type="button"
          onClick={onDismiss}
          aria-label="إغلاق رسالة الخطأ"
          className="shrink-0 -me-1 -my-1 inline-flex min-h-11 min-w-11 items-center justify-center rounded-full text-red-700 hover:bg-red-100 focus-visible:ring-2 focus-visible:ring-red-600"
        >
          <span aria-hidden="true" className="text-lg leading-none">×</span>
        </button>
      </div>
    </div>
  );
}
