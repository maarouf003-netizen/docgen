import { useState } from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect } from 'vitest';
import type { ExecutedHeirDto, HeirDto } from '../../types';
import { emptyExecutedHeir, emptyHeir } from './documentFormConstants';
import { ExecutedHeirsEditor, HeirsEditor } from './HeirsEditors';

function HeirsHarness({
  initial,
  allowAdd = true,
  locked = false,
}: {
  initial: HeirDto[];
  allowAdd?: boolean;
  locked?: boolean;
}) {
  const [heirs, setHeirs] = useState(initial);
  return (
    <HeirsEditor
      idPrefix="test"
      heirs={heirs}
      onSet={(i, key, value) =>
        setHeirs((hs) => hs.map((h, idx) => (idx === i ? { ...h, [key]: value } : h)))
      }
      onAdd={() => setHeirs((hs) => [...hs, emptyHeir()])}
      onRemove={(i) => setHeirs((hs) => hs.filter((_, idx) => idx !== i))}
      allowAdd={allowAdd}
      locked={locked}
    />
  );
}

function ExecutedHeirsHarness({
  initial,
  allowAdd = true,
}: {
  initial: ExecutedHeirDto[];
  allowAdd?: boolean;
}) {
  const [heirs, setHeirs] = useState(initial);
  return (
    <ExecutedHeirsEditor
      idPrefix="executed"
      heirs={heirs}
      onSet={(i, key, value) =>
        setHeirs((hs) => hs.map((h, idx) => (idx === i ? { ...h, [key]: value } : h)))
      }
      onAdd={() => setHeirs((hs) => [...hs, emptyExecutedHeir()])}
      onRemove={(i) => setHeirs((hs) => hs.filter((_, idx) => idx !== i))}
      allowAdd={allowAdd}
    />
  );
}

describe('HeirsEditor · الممثل الشرعي لكل وريث', () => {
  it('يفعّل ممثلًا لوريث واحد ويحذفه دون حذف الوريث', async () => {
    const user = userEvent.setup();
    render(<HeirsHarness initial={[{ ...emptyHeir(), name: 'محمود' }]} />);

    const heirCard = screen.getByText('اسم الوريث').closest('.rounded-lg') as HTMLElement;
    expect(within(heirCard).queryByLabelText('اسم الممثل الشرعي')).not.toBeInTheDocument();

    await user.click(within(heirCard).getByRole('button', { name: 'إضافة ممثل شرعي لهذا الوريث' }));
    expect(within(heirCard).getByLabelText('اسم الممثل الشرعي')).toBeInTheDocument();
    await user.type(within(heirCard).getByLabelText('اسم الممثل الشرعي'), 'سمير');

    await user.click(within(heirCard).getByRole('button', { name: '✖ حذف الممثل' }));
    expect(within(heirCard).queryByLabelText('اسم الممثل الشرعي')).not.toBeInTheDocument();
    expect(screen.getByDisplayValue('محمود')).toBeInTheDocument();
  });

  it('يمنع إضافة ورثة عند وجود ممثل للطرف', () => {
    render(<HeirsHarness initial={[]} allowAdd={false} />);
    expect(screen.queryByRole('button', { name: '＋ إضافة وريث' })).not.toBeInTheDocument();
    expect(screen.getByText(/الممثل الشرعي للطرف قائم/)).toBeInTheDocument();
  });

  it('يُبقي زر ممثل الوريث ظاهرًا رغم وجود ورثة للطرف', () => {
    render(<HeirsHarness initial={[{ ...emptyHeir(), name: 'محمود' }]} />);
    expect(screen.getByRole('button', { name: 'إضافة ممثل شرعي لهذا الوريث' })).toBeInTheDocument();
  });

  it('يقفل ممثل الوريث المخزَّن على الملف المناب', () => {
    render(
      <HeirsHarness
        initial={[
          {
            ...emptyHeir(),
            id: 7,
            name: 'محمود',
            representativeName: 'سمير',
            representativeFather: 'حسن',
            representativeFamily: 'علي',
            representativeCapacity: 'ولي',
          },
        ]}
        locked
      />,
    );

    expect(screen.getByLabelText('اسم الممثل الشرعي')).toHaveAttribute('readonly');
    expect(screen.queryByRole('button', { name: '✖ حذف الممثل' })).not.toBeInTheDocument();
  });

  it('لا يقفل الصف الجديد (بلا id) — تعبئته الأولى مسموحة خلفيًا', () => {
    render(<HeirsHarness initial={[{ ...emptyHeir(), name: 'سامر' }]} locked />);

    expect(screen.getByLabelText('اسم الوريث')).not.toHaveAttribute('readonly');
    expect(screen.getByRole('button', { name: 'إضافة ممثل شرعي لهذا الوريث' })).toBeInTheDocument();
  });

  it('يُبقي العنوان ظاهرًا عند تفعيل الممثل بصفة وحدها (غير مخزَّن)', () => {
    // صفة «ولي» وحدها لا تُحفظ خلفيًا؛ إخفاء العنوان حينها يُفقد قيمة المستخدم دون أثر.
    const { container } = render(
      <HeirsHarness
        initial={[{ ...emptyHeir(), id: 1, name: 'أحمد', representativeCapacity: 'ولي' }]}
      />,
    );

    // معرّفات الحقول تتبع هوية الوريث (`id1`) لا موضعه — تبقى صحيحة بعد الحذف/إعادة الترتيب.
    expect(container.querySelector('#test-heir-address-id1')).toBeInTheDocument();
    expect(container.querySelector('#test-heir-type-id1')).toBeInTheDocument();
    // محرر الممثل ظاهر بالتفعيل (صفة) — لكن العنوان لم يُخفَ بعد لأنه غير مخزَّن.
    expect(container.querySelector('#test-heir-rep-id1-rep-name')).toBeInTheDocument();
  });

  it('يُخفي عنوان الوريث عند وجود اسم ممثل مخزَّن فعلًا', () => {
    const { container } = render(
      <HeirsHarness
        initial={[{ ...emptyHeir(), id: 2, name: 'محمود', representativeName: 'سمير' }]}
      />,
    );

    expect(container.querySelector('#test-heir-address-id2')).toBeNull();
    expect(container.querySelector('#test-heir-type-id2')).toBeNull();
  });

  it('يعطي الصف الجديد (بلا هوية) معرّفات موضعية كالسابق', () => {
    // بلا `id` لا توجد هوية مستقرة بعد؛ يبقى الموضع هو المفتاح حتى يُحفظ الوريث.
    const { container } = render(<HeirsHarness initial={[{ ...emptyHeir(), name: 'سامر' }]} />);

    expect(container.querySelector('#test-heir-name-0')).toBeInTheDocument();
    expect(container.querySelector('#test-heir-name-id1')).toBeNull();
  });
});

/** حذف صفّ من القائمة كما يحدث عند تحديثها من الخادم — لكشف قفلٍ مرتبط بالموضع لا بالهوية. */
function LockShiftHarness() {
  const [heirs, setHeirs] = useState<HeirDto[]>([
    {
      ...emptyHeir(),
      id: 1,
      name: 'أحمد',
      representativeName: 'سمير',
      representativeFather: 'حسن',
      representativeFamily: 'علي',
    },
    { ...emptyHeir(), id: 2, name: 'محمود' },
  ]);
  return (
    <div>
      <button type="button" onClick={() => setHeirs((hs) => hs.filter((h) => h.id !== 1))}>
        محاكاة حذف الصف الأول
      </button>
      <HeirsEditor
        idPrefix="shift"
        heirs={heirs}
        onSet={(i, key, value) =>
          setHeirs((hs) => hs.map((h, idx) => (idx === i ? { ...h, [key]: value } : h)))
        }
        onAdd={() => undefined}
        onRemove={() => undefined}
        locked
      />
    </div>
  );
}

describe('HeirsEditor · ثبات قفل الممثل عبر الهوية', () => {
  it('يبقى ممثل الصف المنشأ حديثًا قابلًا للتحرير بعد انزياح المواضع', async () => {
    const user = userEvent.setup();
    render(<LockShiftHarness />);

    const mahmoudCard = screen.getByDisplayValue('محمود').closest('div.rounded-lg') as HTMLElement;
    // تفعيل ممثل جديد لوريث مخزَّن: يُقفل حتى يُحفظ، ويُتابَع بـ id لا بالموضع.
    await user.click(within(mahmoudCard).getByRole('button', { name: 'إضافة ممثل شرعي لهذا الوريث' }));
    const draftInput = within(mahmoudCard).getByLabelText('اسم الممثل الشرعي');
    expect(draftInput).not.toHaveAttribute('readonly');

    await user.click(screen.getByRole('button', { name: 'محاكاة حذف الصف الأول' }));

    const shiftedCard = screen.getByDisplayValue('محمود').closest('div.rounded-lg') as HTMLElement;
    const shifted = within(shiftedCard).getByLabelText('اسم الممثل الشرعي');
    expect(shifted).not.toHaveAttribute('readonly');
    expect(shifted).not.toBeDisabled();
  });
});

describe('ExecutedHeirsEditor · الممثل الشرعي لكل وريث', () => {
  it('يفعّل ممثلًا لوريث وضع «منفذ عليه» ويحفظ حقوله', async () => {
    const user = userEvent.setup();
    render(<ExecutedHeirsHarness initial={[{ ...emptyExecutedHeir(), heirName: 'محمود' }]} />);

    const heirCard = screen.getByText('الاسم').closest('.rounded-lg') as HTMLElement;
    await user.click(within(heirCard).getByRole('button', { name: 'إضافة ممثل شرعي لهذا الوريث' }));
    expect(within(heirCard).getByLabelText('اسم الممثل الشرعي')).toBeInTheDocument();
    expect(within(heirCard).getByLabelText('صفة الممثل الشرعي')).toBeInTheDocument();
  });

  it('يُخفي عنوان الوريث عند اسم ممثل مخزَّن ويُبقيه عند صفة وحدها', () => {
    const { container, unmount } = render(
      <ExecutedHeirsHarness
        initial={[{ ...emptyExecutedHeir(), id: 3, heirName: 'أحمد', representativeCapacity: 'ولي' }]}
      />,
    );
    expect(container.querySelector('#executed-heir-address-id3')).toBeInTheDocument();
    unmount();

    const { container: stored } = render(
      <ExecutedHeirsHarness
        initial={[{ ...emptyExecutedHeir(), id: 4, heirName: 'محمود', representativeName: 'سمير' }]}
      />,
    );
    expect(stored.querySelector('#executed-heir-address-id4')).toBeNull();
  });
});
