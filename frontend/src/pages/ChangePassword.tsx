import { ChangePasswordForm } from '../components/ChangePasswordForm';

export default function ChangePassword() {
  return (
    <div className="max-w-md mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">تغيير كلمة المرور</h2>
      <div className="bg-white rounded-xl shadow p-6">
        <ChangePasswordForm />
      </div>
    </div>
  );
}
