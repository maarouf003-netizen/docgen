import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { FloatingFocusManager } from '@floating-ui/react';
import { Link } from 'react-router-dom';
import { useFloatingMenu } from '../../hooks/useFloatingMenu';
import { executedTitle, identityFileNumber } from '../../components/view/viewFormat';
import type { AppealDirection, DocumentResponse } from '../../types';

/** حقل هوية أخضر مضغوط للجوال: القيمة وحدها مع تسمية مخفية لقارئ الشاشة. */
function MobileIdentityField({
  label,
  value,
  numeric = false,
}: {
  label: string;
  value: string;
  numeric?: boolean;
}) {
  return (
    <div className="inline-flex min-w-0 items-baseline rounded-lg bg-emerald-50 border border-emerald-200 px-2.5 py-1.5">
      <dt className="sr-only">{label}</dt>
      <dd
        dir={numeric ? 'ltr' : 'auto'}
        className={`text-gray-800 font-semibold break-words ${numeric ? 'tabular-nums isolate' : ''}`}
      >
        {value || '—'}
      </dd>
    </div>
  );
}

interface DocumentViewHeaderProps {
  doc: DocumentResponse;
  id: string | undefined;
  isMobile: boolean;
  isExecuted: boolean;
  isDelegationExecuted: boolean;
  debtorFullName: string;
  statusBadge: { text: string; cls: string };
  fileIdentity: { number: string; year: string };
  fileType: string;
  courtDisplay: string;
  showBranch: boolean;
  canEdit: boolean;
  canCreateAppeal: boolean;
  canTransfer: boolean;
  canDirectAlert: boolean;
  onOpenActions: () => void;
  onOpenGeneration: () => void;
  onOpenAlert: () => void;
  onOpenTransfer: () => void;
  onStartAppeal: (variant: AppealDirection) => void;
}

/**
 * ترويسة صفحة عرض المستند: الشارة والعنوان، أزرار سطح المكتب، قائمة «المزيد»
 * للجوال، وشريط الهوية اللاصق. منقولة حرفيًا من `DocumentView` مع احتفاظها
 * بقوائمها العائمة وحالة القائمة الفرعية محليًا (بلا حالة مرفوعة للصفحة).
 */
export function DocumentViewHeader({
  doc,
  id,
  isMobile,
  isExecuted,
  isDelegationExecuted,
  debtorFullName,
  statusBadge,
  fileIdentity,
  fileType,
  courtDisplay,
  showBranch,
  canEdit,
  canCreateAppeal,
  canTransfer,
  canDirectAlert,
  onOpenActions,
  onOpenGeneration,
  onOpenAlert,
  onOpenTransfer,
  onStartAppeal,
}: DocumentViewHeaderProps) {
  const appealMenu = useFloatingMenu();
  // قائمة «المزيد» للجوال: خطاف عائم مستقل + حالة اللوحة المتداخلة (استئناف)
  // — مستقلة عن `appealMenu` الخاص بسطح المكتب عمدًا. ملاحة البنود بالأسهم
  // مفعّلة (`listNavigation`) مع `FloatingFocusManager` حول اللوحة أدناه.
  const moreMenu = useFloatingMenu({ listNavigation: true });
  const [appealSubOpen, setAppealSubOpen] = useState(false);

  // تصفير اللوحة المتداخلة عند إغلاق القائمة الأم بأي مسار (اختيار بند أو
  // Escape أو نقر خارج) — حتى لا تُعاد الفتحة التالية موسّعة.
  useEffect(() => {
    if (!moreMenu.open) setAppealSubOpen(false);
  }, [moreMenu.open]);

  // الانتقال بين ملفين يعيد استخدام نفس الصفحة: تُغلق قوائم الترويسة المعلّقة
  // حتى لا تبقى مفتوحة على محتوى ملف سابق.
  // setOpen ثابت المرجع (مولّد من useState) فلا يستوجب إدخاله في الاعتماديات.
  useEffect(() => {
    appealMenu.setOpen(false);
    moreMenu.setOpen(false);
    setAppealSubOpen(false);
    // oxlint: لا يعيد هذا التأثير تشغيله عند تغيّر القوائم، بل عند تبديل الملف فقط (id).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // إغلاق قائمة «المزيد» مع لوحتها المتداخلة دفعةً واحدة (عند اختيار بند).
  const closeMoreMenu = () => {
    moreMenu.setOpen(false);
    setAppealSubOpen(false);
  };

  // مفاتيح بنود «المزيد» المرئية بترتيب ظهورها — فهارس مستقرة لتسجيل البنود
  // في `moreMenu.listRef` (ملاحة الأسهم)؛ تُشتق من الشروط نفسها حرفيًا.
  const moreItemKeys: string[] = [
    ...(canEdit ? ['edit'] : []),
    'correspondence',
    ...(canCreateAppeal ? ['appeal'] : []),
    ...(!isExecuted && !isDelegationExecuted ? ['generate'] : []),
    ...(canDirectAlert ? ['alert'] : []),
    ...(canTransfer ? ['transfer'] : []),
  ];
  const moreItemIndex = (key: string) => moreItemKeys.indexOf(key);

  return (
    <div className="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-gray-200 rounded-b-xl shadow-sm px-4 py-3 mb-5">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <h2 className="text-xl md:text-2xl font-bold text-gray-800 flex items-center gap-3 min-w-0">
          <span className={`rounded-full px-3 py-1 text-sm ${statusBadge.cls}`}>
            {statusBadge.text}
          </span>
          <span className="min-w-0 truncate">
            {isExecuted ? executedTitle(doc) : debtorFullName || doc.documentType || `مستند #${doc.id}`}
          </span>
        </h2>
        <div className="flex gap-2 flex-wrap">
          {/* أزرار سطح المكتب: تُخفى على الجوال وتُجمَع في قائمة «المزيد» بدلها. */}
          {canEdit && !isMobile && (
            <Link to={`/documents/${id}/edit`} className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm inline-flex items-center min-h-11">
              تعديل
            </Link>
          )}
          {/* زر «مراسلات»: انتقال لصفحة مراسلات الملف المستقلة (لا تمرير — البطاقة حُذفت). */}
          {!isMobile && (
            <Link
              to={`/documents/${id}/correspondence`}
              className="bg-sky-800 hover:bg-sky-700 text-white rounded-lg px-4 py-2 text-sm inline-flex items-center min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-700"
            >
              مراسلات
            </Link>
          )}
          {/* زر «استئناف» بقائمة منسدلة (مستأنِفين / مستأنف علينا) — محامي الملف المالك (مكتبي فقط). */}
          {canCreateAppeal && !isMobile && (
            <>
              <button
                ref={appealMenu.refs.setReference}
                type="button"
                {...appealMenu.getReferenceProps()}
                aria-haspopup="menu"
                aria-expanded={appealMenu.open}
                className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                استئناف ▾
              </button>
              {appealMenu.open &&
                createPortal(
                  <div
                    ref={appealMenu.refs.setFloating}
                    role="menu"
                    aria-label="نوع الاستئناف"
                    style={appealMenu.floatingStyles}
                    {...appealMenu.getFloatingProps()}
                    className="fixed z-50 w-44 bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden"
                  >
                    <button
                      role="menuitem"
                      type="button"
                      onClick={() => {
                        appealMenu.setOpen(false);
                        onStartAppeal('appellants');
                      }}
                      className="block w-full text-right px-4 py-3 text-sm text-gray-800 hover:bg-red-50 min-h-11 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-emerald-500"
                    >
                      مستأنِفين
                    </button>
                    <button
                      role="menuitem"
                      type="button"
                      onClick={() => {
                        appealMenu.setOpen(false);
                        onStartAppeal('against-us');
                      }}
                      className="block w-full text-right px-4 py-3 text-sm text-gray-800 hover:bg-red-50 min-h-11 border-t border-gray-100 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-emerald-500"
                    >
                      مستأنف علينا
                    </button>
                  </div>,
                  document.body,
                )}
            </>
          )}
          {!isExecuted && !isDelegationExecuted && !isMobile && (
            <button
              onClick={onOpenGeneration}
              className="bg-gray-800 hover:bg-gray-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              توليد مستندات
            </button>
          )}
          <button
            onClick={onOpenActions}
            className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11"
          >
            الإجراءات والملاحظات
          </button>
          {canDirectAlert && !isMobile && (
            <button
              onClick={onOpenAlert}
              className="bg-red-600 hover:bg-red-500 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              توجيه تنبيه
            </button>
          )}
          {canTransfer && !isMobile && (
            <button
              onClick={onOpenTransfer}
              className="bg-sky-800 hover:bg-sky-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              نقل الملف
            </button>
          )}
          {/* قائمة «المزيد» للجوال: تجمع إجراءات الترويسة، و«عودة» أيقونة بجانبها
              في مجموعة واحدة كي لا تنفصل إلى سطر جديد. */}
          {isMobile && (
            <div className="flex items-center gap-2">
              <button
                ref={moreMenu.refs.setReference}
                type="button"
                {...moreMenu.getReferenceProps()}
                className="bg-white hover:bg-gray-50 text-gray-700 border border-gray-300 rounded-lg px-4 py-2 text-sm font-medium min-h-11 inline-flex items-center gap-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                المزيد
                <svg
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  aria-hidden="true"
                  className="w-4 h-4 shrink-0 text-gray-400"
                >
                  <path
                    fillRule="evenodd"
                    d="M5.23 7.21a.75.75 0 0 1 1.06.02L10 11.06l3.71-3.83a.75.75 0 1 1 1.08 1.04l-4.25 4.39a.75.75 0 0 1-1.08 0L5.21 8.27a.75.75 0 0 1 .02-1.06Z"
                    clipRule="evenodd"
                  />
                </svg>
              </button>
              <Link
                to="/documents"
                aria-label="عودة"
                title="عودة"
                className="border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 inline-flex items-center justify-center min-h-11 min-w-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
              >
                <svg
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  aria-hidden="true"
                  className="w-5 h-5"
                >
                  <path
                    fillRule="evenodd"
                    d="M12.79 5.23a.75.75 0 0 1-.02 1.06L8.832 10l3.938 3.71a.75.75 0 1 1-1.04 1.08l-4.5-4.25a.75.75 0 0 1 0-1.08l4.5-4.25a.75.75 0 0 1 1.06.02Z"
                    clipRule="evenodd"
                  />
                </svg>
              </Link>
              {moreMenu.open &&
                createPortal(
                  // عقد `menu` متسق: الخطاف يحقن `aria-haspopup/controls/labelledby`
                  // (لا خصائص يدوية مكررة)، وكل بنود المستوى الأول `menuitem`.
                  <FloatingFocusManager context={moreMenu.context} modal={false} initialFocus={0}>
                    <div
                      ref={moreMenu.refs.setFloating}
                      {...moreMenu.getFloatingProps()}
                      role="menu"
                      style={moreMenu.floatingStyles}
                      className="fixed z-50 w-56 bg-white border border-gray-200 rounded-lg shadow-lg overflow-hidden"
                    >
                      {canEdit && (
                        <Link
                          to={`/documents/${id}/edit`}
                          {...moreMenu.getItemProps()}
                          ref={(node) => {
                            moreMenu.listRef.current[moreItemIndex('edit')] = node;
                          }}
                          role="menuitem"
                          onClick={closeMoreMenu}
                          className="block w-full text-right px-4 py-2 min-h-11 text-sm text-white bg-emerald-800 hover:bg-emerald-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-300 focus-visible:ring-inset"
                        >
                          تعديل
                        </Link>
                      )}
                      <Link
                        to={`/documents/${id}/correspondence`}
                        {...moreMenu.getItemProps()}
                        ref={(node) => {
                          moreMenu.listRef.current[moreItemIndex('correspondence')] = node;
                        }}
                        role="menuitem"
                        onClick={closeMoreMenu}
                        className="block w-full text-right px-4 py-2 min-h-11 text-sm text-white bg-sky-800 hover:bg-sky-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-300 focus-visible:ring-inset"
                      >
                        مراسلات
                      </Link>
                      {canCreateAppeal && (
                        <div role="group" aria-label="استئناف">
                          <button
                            {...moreMenu.getItemProps()}
                            ref={(node) => {
                              moreMenu.listRef.current[moreItemIndex('appeal')] = node;
                            }}
                            role="menuitem"
                            aria-haspopup="menu"
                            type="button"
                            aria-expanded={appealSubOpen}
                            aria-controls="appeal-submenu"
                            onClick={() => setAppealSubOpen((v) => !v)}
                            className="block w-full text-right px-4 py-2 min-h-11 text-sm text-white bg-[#800000] hover:bg-[#9e0e0e] focus:outline-none focus-visible:ring-2 focus-visible:ring-red-300 focus-visible:ring-inset"
                          >
                            استئناف ▾
                          </button>
                          {appealSubOpen && (
                            <div
                              id="appeal-submenu"
                              role="menu"
                              aria-label="نوع الاستئناف"
                              className="bg-white"
                            >
                              <button
                                role="menuitem"
                                type="button"
                                onClick={() => {
                                  closeMoreMenu();
                                  onStartAppeal('appellants');
                                }}
                                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-red-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
                              >
                                مستأنِفين
                              </button>
                              <button
                                role="menuitem"
                                type="button"
                                onClick={() => {
                                  closeMoreMenu();
                                  onStartAppeal('against-us');
                                }}
                                className="block w-full text-right px-4 py-2 min-h-11 text-sm text-gray-800 hover:bg-red-50 border-t border-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-inset"
                              >
                                مستأنف علينا
                              </button>
                            </div>
                          )}
                        </div>
                      )}
                      {!isExecuted && !isDelegationExecuted && (
                        <button
                          {...moreMenu.getItemProps()}
                          ref={(node) => {
                            moreMenu.listRef.current[moreItemIndex('generate')] = node;
                          }}
                          role="menuitem"
                          type="button"
                          onClick={() => {
                            closeMoreMenu();
                            onOpenGeneration();
                          }}
                          className="block w-full text-right px-4 py-2 min-h-11 text-sm text-white bg-gray-800 hover:bg-gray-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-gray-400 focus-visible:ring-inset"
                        >
                          توليد مستندات
                        </button>
                      )}
                      {canDirectAlert && (
                        <button
                          {...moreMenu.getItemProps()}
                          ref={(node) => {
                            moreMenu.listRef.current[moreItemIndex('alert')] = node;
                          }}
                          role="menuitem"
                          type="button"
                          onClick={() => {
                            closeMoreMenu();
                            onOpenAlert();
                          }}
                          className="block w-full text-right px-4 py-2 min-h-11 text-sm text-white bg-red-600 hover:bg-red-500 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-300 focus-visible:ring-inset"
                        >
                          توجيه تنبيه
                        </button>
                      )}
                      {canTransfer && (
                        <button
                          {...moreMenu.getItemProps()}
                          ref={(node) => {
                            moreMenu.listRef.current[moreItemIndex('transfer')] = node;
                          }}
                          role="menuitem"
                          type="button"
                          onClick={() => {
                            closeMoreMenu();
                            onOpenTransfer();
                          }}
                          className="block w-full text-right px-4 py-2 min-h-11 text-sm text-white bg-sky-800 hover:bg-sky-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-sky-300 focus-visible:ring-inset"
                        >
                          نقل الملف
                        </button>
                      )}
                    </div>
                  </FloatingFocusManager>,
                  document.body,
                )}
            </div>
          )}
          {!isMobile && (
            <Link to="/documents" className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 inline-flex items-center min-h-11">
              عودة
            </Link>
          )}
        </div>
      </div>

      {/* شريط الهوية: بطاقات الملف الأساسية (رقم/سنة مدمجان + نوع + دائرة/فرع) لقراءة فورية أثناء التمرير. */}
      <dl className={`mt-3 flex flex-wrap text-sm ${isMobile ? 'gap-1' : 'gap-2'}`}>
        {isMobile ? (
          <>
            <MobileIdentityField label="رقم الملف" value={fileIdentity.number} numeric />
            <MobileIdentityField label="السنة" value={fileIdentity.year} numeric />
            <MobileIdentityField label="نوع الملف" value={fileType} />
            <MobileIdentityField label="دائرة التنفيذ" value={courtDisplay} />
          </>
        ) : (
          <>
            <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-1.5">
              <dt className="text-xs text-emerald-800 font-medium">رقم الملف والسنة</dt>
              <dd dir="ltr" className="text-gray-800 font-semibold tabular-nums isolate">
                {identityFileNumber(doc)}
              </dd>
            </div>
            <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
              <dt className="text-xs text-gray-500 font-medium">نوع الملف</dt>
              <dd className="text-gray-800 font-semibold">{fileType}</dd>
            </div>
            <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
              <dt className="text-xs text-gray-500 font-medium">الدائرة</dt>
              <dd className="text-gray-800 font-semibold">{doc.court || '—'}</dd>
            </div>
          </>
        )}
        {showBranch && (
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">الفرع</dt>
            <dd className="text-gray-800 font-semibold">{doc.branchName || '—'}</dd>
          </div>
        )}
      </dl>
    </div>
  );
}
