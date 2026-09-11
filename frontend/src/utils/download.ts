/**
 * تنزيل Blob باسم ملف آمن مع إدارة كاملة لدورة الرابط (إنشاء، نقر، إزالة، تحرير).
 * يرفض الأسماء الفارغة/البيضاء بصريح الخطأ — كل المُستدعين يضمنون اسمًا غير فارغ.
 */
export function downloadBlob(blob: Blob, filename: string): void {
  const name = filename.trim();
  if (!name) throw new Error('downloadBlob: يجب توفير اسم ملف غير فارغ');
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = name;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}