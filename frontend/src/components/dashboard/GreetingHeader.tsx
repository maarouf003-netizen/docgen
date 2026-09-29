import { useState } from 'react';
import { firstName } from './dashboardFormat';
import { GREETINGS, nextGreetingIndex } from './greetingContent';

/**
 * رسالة الترحيب أعلى لوحة المحامي: صيغة ثابتة لكل زيارة (تتقدّم مع كل دخول
 * للوحة) — بلا تبدل تلقائي داخل الزيارة، وبلا مناطق حيّة لقارئ الشاشة.
 */
export function GreetingHeader({ fullName }: { fullName?: string | null }) {
  const [index] = useState(() => nextGreetingIndex());
  const name = firstName(fullName) || (fullName ?? '').trim();

  return (
    <div>
      <h2 className="text-xl sm:text-2xl font-bold text-gray-900 text-balance">
        {GREETINGS[index]}، {name}
      </h2>
    </div>
  );
}
