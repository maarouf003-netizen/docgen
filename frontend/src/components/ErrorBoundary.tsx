import { Component, type ErrorInfo, type ReactNode } from 'react';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

export default class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('ErrorBoundary caught an error', error, info);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="min-h-screen flex flex-col items-center justify-center gap-4 p-6 text-center">
          <div className="text-4xl">⚠️</div>
          <p className="text-lg font-bold text-gray-800">حدث خطأ غير متوقع</p>
          <p className="text-gray-600">تعذّر عرض الصفحة. أعد المحاولة أو سجّل الدخول مجدداً.</p>
          <button
            type="button"
            onClick={() => window.location.reload()}
            className="bg-blue-700 hover:bg-blue-600 text-white rounded-lg px-4 py-2 text-sm min-h-11"
          >
            إعادة تحميل الصفحة
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}
