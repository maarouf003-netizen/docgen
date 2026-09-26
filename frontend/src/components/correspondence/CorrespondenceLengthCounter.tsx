import { CORRESPONDENCE_MAX_BODY_CHARS } from './correspondenceDisplay';

/**
 * عدّاد أحرف النص الصافي لصياغة المراسلة (تسطير/لاحق/رد) — مصدر واحد
 * للعدّاد والتحذير في كل المسارات بدل نسخ JSX متباينة.
 * `plainLen` يُحسب في المكوّن الأب بالمقياس نفسه في الخادم
 * (`correspondencePlainText`)، والمنع المبكر هنا إرشادي والفرض في الخدمة.
 *
 * ربط قارئ الشاشة: الأب يولّد `counterId` بـ`useId` ويمرّره هنا `id` وللمحرر
 * عبر `describedById` في `RichTextEditor`، فيُعلَن العدّاد وصفًا لحقل الصياغة.
 */
export default function CorrespondenceLengthCounter({
  counterId,
  plainLen,
  tooLong,
}: {
  counterId: string;
  plainLen: number;
  tooLong: boolean;
}) {
  return (
    <>
      <div className="mt-1.5 flex justify-end">
        <span
          id={counterId}
          className={`text-xs tabular-nums ${tooLong ? 'text-red-600 font-bold' : 'text-gray-400'}`}
        >
          {plainLen} / {CORRESPONDENCE_MAX_BODY_CHARS}
        </span>
      </div>
      {tooLong && (
        <p className="text-red-600 text-sm mt-1" role="alert">
          تجاوز النص الحد الأقصى ({CORRESPONDENCE_MAX_BODY_CHARS} حرف) — قصّر النص قبل الإرسال.
        </p>
      )}
    </>
  );
}
