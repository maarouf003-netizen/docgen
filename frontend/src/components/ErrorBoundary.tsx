import {
  Component,
  useEffect,
  useRef,
  useState,
  type ErrorInfo,
  type ReactNode,
} from 'react';
import { reportClientError } from '../utils/errorReporting';
import { redactSensitiveText } from '../utils/redactSensitiveText';

/**
 * حدود خطأ مشتركة (P6-01a).
 *
 * الحدود تلتقط **أخطاء العرض والمكوّنات أثناء `render`/دورة الحياة** فقط —
 * وهذا حدّ React نفسه: أخطاء `async` (وعود/`setTimeout`/مستمعو الأحداث) لا
 * تصل إليها، بل تُعالَج في مواضعها (`try/catch` أو ردّ الرفض) وتُعرض كحالة
 * خطأ داخل الصفحة. لا نُدّعي التقاطها هنا.
 *
 * - `variant="screen"`: الشاشة الكاملة (حدّ الجذر) — لا شاشة بيضاء.
 * - `variant="content"`: داخل `<main>` في `Layout` — الشريط الجانبي/التنقل
 *   السفلي يبقى حيًّا، فيستطيع المستخدم مغادرة الصفحة المعطوبة بلا إعادة تحميل.
 * - `resetKeys`: عند تغيّرها (مسارات التنقل) يُصفَّر الحدّ تلقائيًا بدل أن يبقى
 *   مغلقًا على الصفحة التالية.
 */
export type ErrorBoundaryVariant = 'screen' | 'content';

export interface ErrorBoundaryProps {
  children: ReactNode;
  variant?: ErrorBoundaryVariant;
  /** مفاتيح إعادة الضبط (مثل `pathname`) — تُقارن قيمًا سطحية. */
  resetKeys?: readonly unknown[];
  /** توصيف نطاق الخطأ في النص المعروض (مثل «هذه الصفحة»). */
  scopeLabel?: string;
}

interface ErrorBoundaryState {
  error: unknown;
  errorId: string | null;
}

function createErrorId(): string {
  try {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }
  } catch {
    // بيئة قديمة/سياق غير آمن — نسقط إلى معرّف وهمي كافٍ للربط.
  }
  return `err-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function resetKeysChanged(
  previous: readonly unknown[] | undefined,
  next: readonly unknown[] | undefined,
): boolean {
  if (previous === next) return false;
  if (!previous || !next || previous.length !== next.length) return true;
  return previous.some((value, index) => !Object.is(value, next[index]));
}

async function copyText(text: string): Promise<void> {
  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return;
  }
  if (typeof document === 'undefined') throw new Error('clipboard unavailable');
  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.opacity = '0';
  document.body.appendChild(area);
  try {
    area.select();
    const copied = typeof document.execCommand === 'function' && document.execCommand('copy');
    if (!copied) throw new Error('copy failed');
  } finally {
    document.body.removeChild(area);
  }
}

interface ErrorFallbackProps {
  errorId: string;
  variant: ErrorBoundaryVariant;
  scopeLabel?: string;
  onRetry: () => void;
}

function ErrorFallback({ errorId, variant, scopeLabel, onRetry }: ErrorFallbackProps) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'failed'>('idle');
  const Heading = variant === 'screen' ? 'h1' : 'h2';
  const scope = scopeLabel ?? (variant === 'screen' ? 'التطبيق' : 'هذه الصفحة');

  // نقل التركيز إلى العنوان عند ظهور الشاشة: يُعلَن الخطأ لقارئ الشاشة
  // (role="alert") ويصبح زر «إعادة المحاولة» على بُعد Tab واحد.
  useEffect(() => {
    headingRef.current?.focus();
  }, []);

  const handleCopy = async () => {
    try {
      await copyText(errorId);
      setCopyState('copied');
    } catch {
      setCopyState('failed');
    }
  };

  return (
    <div
      role="alert"
      dir="rtl"
      className={
        variant === 'screen'
          ? 'min-h-dvh w-full flex items-center justify-center bg-gray-50 p-4'
          : 'w-full py-8 px-2 sm:px-4'
      }
    >
      <section className="w-full max-w-xl mx-auto rounded-xl border border-amber-300 bg-white p-5 sm:p-8 text-center shadow-sm">
        <div className="text-4xl" aria-hidden="true">
          ⚠️
        </div>
        <Heading
          ref={headingRef}
          tabIndex={-1}
          className="mt-3 text-lg sm:text-xl font-bold text-gray-900 text-balance rounded focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 focus-visible:ring-offset-2"
        >
          {variant === 'screen' ? 'حدث خطأ غير متوقع في التطبيق' : `تعذّر عرض ${scope}`}
        </Heading>
        <p className="mt-2 text-sm sm:text-base text-gray-600 leading-relaxed text-pretty">
          لم تُفقد بياناتك المحفوظة. أعد المحاولة، أو انتقل إلى صفحة أخرى، وإن استمر الخطأ
          فأبلغ الدعم مع معرّف الخطأ أدناه.
        </p>
        <p className="mt-3 text-xs text-gray-500 break-all">
          معرّف الخطأ:{' '}
          <span dir="ltr" className="font-mono tabular-nums select-all">
            {errorId}
          </span>
        </p>
        <div className="mt-5 flex flex-col sm:flex-row flex-wrap items-stretch sm:items-center justify-center gap-3">
          <button
            type="button"
            onClick={onRetry}
            className="min-h-11 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 focus-visible:ring-offset-2"
          >
            إعادة المحاولة
          </button>
          <a
            href="/"
            className="min-h-11 inline-flex items-center justify-center rounded-lg border border-emerald-700 px-4 py-2 text-sm font-medium text-emerald-800 hover:bg-emerald-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 focus-visible:ring-offset-2"
          >
            العودة إلى الرئيسية
          </a>
          <button
            type="button"
            onClick={handleCopy}
            className="min-h-11 rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-600 focus-visible:ring-offset-2"
          >
            نسخ معرّف الخطأ
          </button>
        </div>
        <p role="status" aria-live="polite" className="mt-3 min-h-5 text-xs">
          {copyState === 'copied' && <span className="text-emerald-700">تم نسخ معرّف الخطأ.</span>}
          {copyState === 'failed' && (
            <span className="text-red-700">تعذّر النسخ — انسخ المعرّف يدويًا.</span>
          )}
        </p>
      </section>
    </div>
  );
}

export default class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null, errorId: null };

  static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
    return { error, errorId: createErrorId() };
  }

  componentDidUpdate(previousProps: ErrorBoundaryProps) {
    if (
      this.state.error != null &&
      resetKeysChanged(previousProps.resetKeys, this.props.resetKeys)
    ) {
      this.reset();
    }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    const errorId = this.state.errorId ?? createErrorId();
    const message = redactSensitiveText(error?.message || 'unknown render error', 2000);
    const name = redactSensitiveText(error?.name || 'Error', 120);

    // `console` في التطوير فقط: لا رموز ولا بيانات شخصية (انظر `redactSensitiveText`)،
    // ولا `stack`/`componentStack` خامًا قد يحمل مسارات أو قيمًا من النموذج.
    // شرط `MODE` إضافة قابلة للاختبار (بناء الإنتاج `DEV=false` أصلًا).
    if (import.meta.env.DEV && import.meta.env.MODE !== 'production') {
      console.error(`[ErrorBoundary:${errorId}] ${name}: ${message}`);
    }

    // تقرير الخادم يبقى سلوكًا قائمًا (صامتًا)، لكن بعد التنقية أيضًا.
    const stack = redactSensitiveText(
      [error?.stack, info.componentStack].filter(Boolean).join('\n'),
      8000,
    );
    reportClientError({
      message: `ErrorBoundary ${errorId}: ${message}`,
      stack,
      component: 'ErrorBoundary',
    });
  }

  private reset = () => {
    this.setState({ error: null, errorId: null });
  };

  render() {
    if (this.state.error != null && this.state.errorId) {
      return (
        <ErrorFallback
          errorId={this.state.errorId}
          variant={this.props.variant ?? 'screen'}
          scopeLabel={this.props.scopeLabel}
          onRetry={this.reset}
        />
      );
    }
    return this.props.children;
  }
}
