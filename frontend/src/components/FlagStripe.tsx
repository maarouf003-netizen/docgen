/**
 * شريط علم الجمهورية العربية السورية الرفيع (أخضر/أبيض/أسود) مع ثلاث نجوم حمراء
 * في الشريط الأبيض — يُستخدم أعلى نموذج تسجيل الدخول. زخرفي بالكامل (`aria-hidden`).
 */
export function FlagStripe({ className = '' }: { className?: string }) {
  return (
    <div aria-hidden="true" className={`flex h-2 ${className}`}>
      <span className="flex-1 bg-[#007A3D]" />
      <span className="flex flex-1 items-center justify-center gap-[3px] bg-white">
        {[0, 1, 2].map((index) => (
          <svg
            key={index}
            data-testid="flag-star"
            viewBox="0 0 24 24"
            className="h-2 w-2 fill-[#CE1126]"
          >
            <path d="M12 2.4l2.94 5.96 6.58.96-4.76 4.64 1.12 6.55L12 17.42l-5.88 3.09 1.12-6.55-4.76-4.64 6.58-.96z" />
          </svg>
        ))}
      </span>
      <span className="flex-1 bg-neutral-900" />
    </div>
  );
}
