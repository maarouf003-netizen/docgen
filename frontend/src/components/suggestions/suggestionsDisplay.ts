/**
 * حدث يُطلق عند تعليم اقتراح تطوير مقروءًا — يعيد جلب عدّاد غير المقروء فورًا
 * في لوحة المدير/المشرف وصفحة الاقتراحات (بلا استطلاع متكرر).
 */
export const SUGGESTIONS_UNREAD_EVENT = 'app-suggestions:unread-changed';

/** نقطة عدّ الاقتراحات غير المقروءة — صندوق المشرف/المدير. */
export const SUGGESTIONS_UNREAD_ENDPOINT = '/app-suggestions/unread-count';
