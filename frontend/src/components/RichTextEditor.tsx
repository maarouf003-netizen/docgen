import type { ReactNode } from 'react';
import { useMemo } from 'react';
import { useEditor, EditorContent, type Editor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import { TextStyle } from '@tiptap/extension-text-style';
import Color from '@tiptap/extension-color';
import Placeholder from '@tiptap/extension-placeholder';

const TEXT_COLOR_PRESETS = [
  { label: 'نص أحمر', value: '#dc2626' },
  { label: 'نص أزرق', value: '#2563eb' },
  { label: 'نص أخضر', value: '#16a34a' },
  { label: 'نص أسود', value: '#111827' },
] as const;

const FONT_FAMILY_PRESETS = [
  { label: 'الخط الافتراضي', value: '' },
  { label: 'كايرو', value: "'Cairo', Tahoma, sans-serif" },
  { label: 'سيمبليفايد عربي', value: "'Simplified Arabic', Tahoma, sans-serif" },
  { label: 'تقليدي عربي', value: "'Traditional Arabic', 'Times New Roman', serif" },
  { label: 'تايمز نيو رومان', value: "'Times New Roman', serif" },
] as const;

const FONT_SIZE_PRESETS = [
  { label: 'الحجم الافتراضي', value: '' },
  { label: 'صغير (12px)', value: '12px' },
  { label: 'عادي (14px)', value: '14px' },
  { label: 'متوسط (16px)', value: '16px' },
  { label: 'كبير (18px)', value: '18px' },
  { label: 'ضخم (24px)', value: '24px' },
] as const;

/** امتداد TextStyle بخصائص الخط وحجمه — يُحفظان ضمن style للـ span. */
const StyledTextStyle = TextStyle.extend({
  addAttributes() {
    return {
      ...this.parent?.(),
      fontFamily: {
        default: null,
        parseHTML: (element) => element.style.fontFamily || null,
        renderHTML: (attributes) =>
          attributes.fontFamily ? { style: `font-family: ${attributes.fontFamily}` } : {},
      },
      fontSize: {
        default: null,
        parseHTML: (element) => element.style.fontSize || null,
        renderHTML: (attributes) =>
          attributes.fontSize ? { style: `font-size: ${attributes.fontSize}` } : {},
      },
    };
  },
});

function ToolbarButton({
  active,
  onClick,
  disabled,
  children,
  label,
  className = '',
}: {
  active?: boolean;
  onClick: () => void;
  disabled?: boolean;
  children: ReactNode;
  label: string;
  className?: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      aria-pressed={active}
      className={`min-h-11 min-w-11 px-3 rounded-lg border text-sm font-medium transition-colors ${
        active
          ? 'bg-gray-800 text-white border-gray-800'
          : 'border-gray-300 text-gray-700 hover:bg-gray-50'
      } disabled:opacity-50 ${className}`}
    >
      {children}
    </button>
  );
}

function ToolbarSelect({
  label,
  ariaLabel,
  value,
  options,
  onChange,
}: {
  label: string;
  ariaLabel: string;
  value: string;
  options: ReadonlyArray<{ label: string; value: string }>;
  onChange: (value: string) => void;
}) {
  return (
    <label className="flex items-center gap-1 text-xs text-gray-500">
      <span>{label}</span>
      <select
        aria-label={ariaLabel}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="min-h-11 rounded-lg border border-gray-300 bg-white px-2 py-1.5 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-emerald-500"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}

export default function RichTextEditor({
  value,
  onChange,
  placeholder,
  onReady,
}: {
  value: string;
  onChange: (html: string) => void;
  placeholder?: string;
  onReady?: (editor: Editor) => void;
}) {
  const extensions = useMemo(
    () => [
      StarterKit.configure({
        heading: false,
        blockquote: false,
        codeBlock: false,
        code: false,
        horizontalRule: false,
        bulletList: false,
        orderedList: false,
        listItem: false,
        link: false,
      }),
      StyledTextStyle.configure(),
      Color.configure({ types: ['textStyle'] }),
      Placeholder.configure({ placeholder: placeholder ?? 'اكتب هنا...' }),
    ],
    [placeholder],
  );

  const editor = useEditor(
    {
      extensions,
      content: value,
      immediatelyRender: false,
      shouldRerenderOnTransaction: true,
      onCreate: ({ editor }) => onReady?.(editor),
      onUpdate: ({ editor }) => onChange(editor.getHTML()),
      editorProps: {
        attributes: {
          class:
            'rich-text-editor w-full min-h-32 max-h-64 overflow-y-auto rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500',
          'aria-label': placeholder ?? 'محرر النص',
        },
      },
    },
    [extensions],
  );

  if (!editor) return null;

  const textStyleAttributes = editor.getAttributes('textStyle');
  const currentColor = textStyleAttributes.color as string | undefined;
  const currentFontFamily = (textStyleAttributes.fontFamily as string | undefined) ?? '';
  const currentFontSize = (textStyleAttributes.fontSize as string | undefined) ?? '';

  /**
   * تطبيق خصائص النمط على العلامة الحالية مع دمجها مع خصائصها القائمة
   * (تغيير الخط لا يلغي اللون والعكس)، وإزالة الخاصية عند القيمة الفارغة.
   */
  const applyTextStyleAttrs = (attrs: Record<string, string | null>) => {
    const merged: Record<string, string> = { ...editor.getAttributes('textStyle') };
    for (const [name, next] of Object.entries(attrs)) {
      if (next === null || next === '') delete merged[name];
      else merged[name] = next;
    }
    editor.chain().focus().setMark('textStyle', merged).run();
  };

  return (
    <div>
      <div className="flex flex-wrap items-center gap-1.5 mb-1.5" role="group" aria-label="أدوات التنسيق">
        <ToolbarButton
          label="تراجع"
          disabled={!editor.can().undo()}
          onClick={() => editor.chain().focus().undo().run()}
        >
          <span aria-hidden="true">↩</span>
        </ToolbarButton>
        <ToolbarButton
          label="إعادة"
          disabled={!editor.can().redo()}
          onClick={() => editor.chain().focus().redo().run()}
        >
          <span aria-hidden="true">↪</span>
        </ToolbarButton>
        <ToolbarButton
          label="عريض"
          active={editor.isActive('bold')}
          onClick={() => editor.chain().focus().toggleBold().run()}
        >
          <b>B</b>
        </ToolbarButton>
        <ToolbarSelect
          label="الخط:"
          ariaLabel="نوع الخط"
          value={currentFontFamily}
          options={[...FONT_FAMILY_PRESETS]}
          onChange={(next) => applyTextStyleAttrs({ fontFamily: next })}
        />
        <ToolbarSelect
          label="الحجم:"
          ariaLabel="حجم الخط"
          value={currentFontSize}
          options={[...FONT_SIZE_PRESETS]}
          onChange={(next) => applyTextStyleAttrs({ fontSize: next })}
        />
        <div className="flex items-center gap-1.5" role="group" aria-label="لون النص">
          {TEXT_COLOR_PRESETS.map((c) => (
            <ToolbarButton
              key={c.value}
              label={c.label}
              active={currentColor === c.value}
              onClick={() => applyTextStyleAttrs({ color: c.value })}
            >
              <span
                className="inline-block w-4 h-4 rounded-full border border-gray-300"
                style={{ backgroundColor: c.value }}
                aria-hidden="true"
              />
            </ToolbarButton>
          ))}
        </div>
        <ToolbarButton
          label="مسح التنسيق"
          onClick={() => editor.chain().focus().unsetAllMarks().clearNodes().run()}
        >
          <span aria-hidden="true">{'A\u0336'}</span>
        </ToolbarButton>
      </div>
      <EditorContent editor={editor} />
    </div>
  );
}
