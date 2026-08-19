import type { DocumentResponse } from '../../types';
import { DocumentGenerationSection } from './DocumentGenerationSection';

export default function DocumentGenerationModal({
  doc,
  id,
  onClose,
}: {
  doc: DocumentResponse;
  id: string | undefined;
  onClose: () => void;
}) {
  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="توليد المستندات التنفيذية"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-5xl flex flex-col max-h-[85vh]">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h3 className="text-lg font-bold text-emerald-800">توليد المستندات التنفيذية</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4 overflow-y-auto">
          <DocumentGenerationSection doc={doc} id={id} />
        </div>
      </div>
    </div>
  );
}