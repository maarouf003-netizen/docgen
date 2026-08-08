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
      TextStyle.configure(),
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

  const currentColor = editor.getAttributes('textStyle').color as string | undefined;

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
        <div className="flex items-center gap-1.5" role="group" aria-label="لون النص">
          {TEXT_COLOR_PRESETS.map((c) => (
            <ToolbarButton
              key={c.value}
              label={c.label}
              active={currentColor === c.value}
              onClick={() => editor.chain().focus().setColor(c.value).run()}
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
