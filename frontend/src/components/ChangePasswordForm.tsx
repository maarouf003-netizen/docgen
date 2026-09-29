import { useRef, useState, type FormEvent } from 'react';
import { api } from '../api/client';

/**
 * نموذج تغيير كلمة المرور المشترك — يُستخدم في صفحة `ChangePassword`
 * وبطاقة `/account` (لا تكرار للمنطق):
 * تطابق الجديدتين + طول ≥ 6 + `POST /auth/change-password`، مع
 * `autocomplete`/`name` صحيحة، وملصقات قابلة للنقر، وتركيز أول خطأ.
 */
export function ChangePasswordForm() {
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fieldError, setFieldError] = useState<'new' | 'confirm' | null>(null);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const newRef = useRef<HTMLInputElement>(null);
  const confirmRef = useRef<HTMLInputElement>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setMessage(null);
    setFieldError(null);

    if (newPassword.length < 6) {
      setFieldError('new');
      setMessage({ ok: false, text: 'كلمة المرور يجب أن تكون 6 أحرف على الأقل' });
      newRef.current?.focus();
      return;
    }
    if (newPassword !== confirmPassword) {
      setFieldError('confirm');
      setMessage({ ok: false, text: 'كلمتا المرور الجديدتان غير متطابقتين' });
      confirmRef.current?.focus();
      return;
    }

    setBusy(true);
    try {
      await api.post('/auth/change-password', { oldPassword, newPassword });
      setMessage({ ok: true, text: 'تم تغيير كلمة المرور بنجاح' });
      setOldPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } }).response?.data?.message;
      setMessage({ ok: false, text: msg || 'فشل تغيير كلمة المرور' });
    } finally {
      setBusy(false);
    }
  };

  const inputClass = (invalid: boolean) =>
    `w-full min-h-11 border rounded-lg px-3 py-2 focus-visible:ring-2 focus-visible:ring-emerald-600 ${
      invalid ? 'border-red-400' : 'border-gray-300'
    }`;

  return (
    <form onSubmit={submit}>
      {message && (
        <div
          role={message.ok ? 'status' : 'alert'}
          className={`border rounded-lg p-3 mb-4 text-sm ${
            message.ok
              ? 'bg-green-50 text-green-700 border-green-200'
              : 'bg-red-50 text-red-700 border-red-200'
          }`}
        >
          {message.text}
        </div>
      )}

      <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="pwd-current">
        كلمة المرور الحالية
      </label>
      <input
        id="pwd-current"
        name="currentPassword"
        type="password"
        autoComplete="current-password"
        value={oldPassword}
        onChange={(e) => setOldPassword(e.target.value)}
        required
        className={`${inputClass(false)} mb-4`}
      />

      <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="pwd-new">
        كلمة المرور الجديدة
      </label>
      <input
        ref={newRef}
        id="pwd-new"
        name="newPassword"
        type="password"
        autoComplete="new-password"
        value={newPassword}
        onChange={(e) => setNewPassword(e.target.value)}
        required
        aria-invalid={fieldError === 'new'}
        className={`${inputClass(fieldError === 'new')} mb-4`}
      />

      <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="pwd-confirm">
        تأكيد كلمة المرور
      </label>
      <input
        ref={confirmRef}
        id="pwd-confirm"
        name="confirmPassword"
        type="password"
        autoComplete="new-password"
        value={confirmPassword}
        onChange={(e) => setConfirmPassword(e.target.value)}
        required
        aria-invalid={fieldError === 'confirm'}
        className={`${inputClass(fieldError === 'confirm')} mb-6`}
      />

      <button
        type="submit"
        disabled={busy}
        className="w-full bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white font-bold rounded-lg py-2.5 transition-colors min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-600"
      >
        {busy ? 'جارِ الحفظ...' : 'حفظ'}
      </button>
    </form>
  );
}
