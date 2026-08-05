import { useState, type FormEvent } from 'react';
import { api } from '../api/client';

export default function ChangePassword() {
  const [oldPassword, setOldPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setMessage(null);

    if (newPassword !== confirmPassword) {
      setMessage({ ok: false, text: 'كلمتا المرور الجديدتان غير متطابقتين' });
      return;
    }
    if (newPassword.length < 6) {
      setMessage({ ok: false, text: 'كلمة المرور يجب أن تكون 6 أحرف على الأقل' });
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

  return (
    <div className="max-w-md mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">تغيير كلمة المرور</h2>
      <form onSubmit={submit} className="bg-white rounded-xl shadow p-6">
        {message && (
          <div
            className={`border rounded-lg p-3 mb-4 text-sm ${
              message.ok
                ? 'bg-green-50 text-green-700 border-green-200'
                : 'bg-red-50 text-red-700 border-red-200'
            }`}
          >
            {message.text}
          </div>
        )}

        <label className="block text-sm font-medium text-gray-700 mb-1">كلمة المرور الحالية</label>
        <input
          type="password"
          value={oldPassword}
          onChange={(e) => setOldPassword(e.target.value)}
          required
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-4 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />

        <label className="block text-sm font-medium text-gray-700 mb-1">كلمة المرور الجديدة</label>
        <input
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          required
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-4 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />

        <label className="block text-sm font-medium text-gray-700 mb-1">تأكيد كلمة المرور</label>
        <input
          type="password"
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
          required
          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 mb-6 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        />

        <button
          type="submit"
          disabled={busy}
          className="w-full bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white font-bold rounded-lg py-2.5 transition-colors min-h-11"
        >
          {busy ? 'جارِ الحفظ...' : 'حفظ'}
        </button>
      </form>
    </div>
  );
}
