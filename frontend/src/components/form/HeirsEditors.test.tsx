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
  allowRepresentative = true,
  locked = false,
}: {
  initial: HeirDto[];
  allowAdd?: boolean;
  allowRepresentative?: boolean;
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
      allowRepresentative={allowRepresentative}
      locked={locked}
    />
  );
}

function ExecutedHeirsHarness({
  initial,
  allowAdd = true,
  allowRepresentative = true,
}: {
  initial: ExecutedHeirDto[];
  allowAdd?: boolean;
  allowRepresentative?: boolean;
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
      allowRepresentative={allowRepresentative}
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

  it('يمنع إضافة ممثل عند وجود ورثة للطرف', () => {
    render(<HeirsHarness initial={[{ ...emptyHeir(), name: 'محمود' }]} allowRepresentative={false} />);
    expect(screen.queryByRole('button', { name: 'إضافة ممثل شرعي لهذا الوريث' })).not.toBeInTheDocument();
    expect(screen.getByText(/لا يجتمع الممثل الشرعي مع الورثة/)).toBeInTheDocument();
  });

  it('يقفل ممثل الوريث المخزَّن على الملف المناب', () => {
    render(
      <HeirsHarness
        initial={[
          {
            ...emptyHeir(),
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
});
