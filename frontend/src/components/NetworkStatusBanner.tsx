import { useEffect, useState } from 'react';

export default function NetworkStatusBanner() {
  const [online, setOnline] = useState(navigator.onLine);

  useEffect(() => {
    const handleOnline = () => setOnline(true);
    const handleOffline = () => setOnline(false);
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  if (online) return null;

  return (
    <div className="bg-red-600 text-white text-center text-sm py-2 px-4" role="alert">
      ⚠️ لا يوجد اتصال بالإنترنت — لن تتمكن من تحميل البيانات أو حفظها حتى عودة الاتصال
    </div>
  );
}
