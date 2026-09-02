import { describe, it, expect } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createPortal } from 'react-dom';
import type { ReactNode } from 'react';
import { useFloatingMenu } from './useFloatingMenu';

function Probe(): ReactNode {
  const menu = useFloatingMenu();
  const refName = 'افتح';
  return (
    <>
      <button
        ref={menu.refs.setReference}
        {...menu.getReferenceProps()}
        aria-haspopup="menu"
        aria-expanded={menu.open}
      >
        {refName}
      </button>
      {menu.open && (
        <div
          ref={menu.refs.setFloating}
          role="menu"
          data-testid="float"
          style={menu.floatingStyles}
          {...menu.getFloatingProps()}
        >
          <button role="menuitem">الخيار أ</button>
        </div>
      )}
    </>
  );
}

function ProbePortal(): ReactNode {
  const menu = useFloatingMenu();
  const refName = 'افتح بوابة';
  return (
    <>
      <button
        ref={menu.refs.setReference}
        {...menu.getReferenceProps()}
        aria-haspopup="menu"
        aria-expanded={menu.open}
      >
        {refName}
      </button>
      {menu.open &&
        createPortal(
          <div
            ref={menu.refs.setFloating}
            role="menu"
            data-testid="float"
            style={menu.floatingStyles}
            {...menu.getFloatingProps()}
          >
            <button role="menuitem">بند</button>
          </div>,
          document.body,
        )}
    </>
  );
}

describe('useFloatingMenu', () => {
  it('القائمة مغلقة افتراضيًا، وتفتح بالنقر على المرجع', async () => {
    const user = userEvent.setup();
    render(<Probe />);
    expect(screen.queryByRole('menu', { name: 'افتح' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'افتح' }));
    expect(screen.getByRole('menu', { name: 'افتح' })).toBeInTheDocument();
  });

  it('toggle: النقر مرة ثانية يُغلق القائمة', async () => {
    const user = userEvent.setup();
    render(<Probe />);
    const btn = screen.getByRole('button', { name: 'افتح' });
    await user.click(btn);
    expect(screen.queryByRole('menu', { name: 'افتح' })).toBeInTheDocument();
    await user.click(btn);
    expect(screen.queryByRole('menu', { name: 'افتح' })).not.toBeInTheDocument();
  });

  it('يغلق القائمة بمفتاح Escape', async () => {
    const user = userEvent.setup();
    render(<Probe />);
    await user.click(screen.getByRole('button', { name: 'افتح' }));
    expect(screen.getByRole('menu', { name: 'افتح' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('menu', { name: 'افتح' })).not.toBeInTheDocument();
  });

  it('يغلق القائمة عند النقر خارجها', async () => {
    const user = userEvent.setup();
    render(
      <>
        <Probe />
        <div data-testid="outside">خارج</div>
      </>,
    );
    await user.click(screen.getByRole('button', { name: 'افتح' }));
    expect(screen.getByRole('menu', { name: 'افتح' })).toBeInTheDocument();
    await user.click(screen.getByTestId('outside'));
    expect(screen.queryByRole('menu', { name: 'افتح' })).not.toBeInTheDocument();
  });

  it('setOpen يفتح ويغلق القائمة من البره', () => {
    let set: ReturnType<typeof useFloatingMenu>['setOpen'] | undefined;
    function Holder() {
      const m = useFloatingMenu();
      set = m.setOpen;
      return (
        <>
          <button ref={m.refs.setReference} {...m.getReferenceProps()}>افتح بره</button>
          {m.open && <div ref={m.refs.setFloating} role="menu" aria-label="ب" data-testid="m" style={m.floatingStyles} {...m.getFloatingProps()} />}
        </>
      );
    }
    render(<Holder />);
    act(() => set?.(true));
    expect(screen.getByTestId('m')).toBeInTheDocument();
    act(() => set?.(false));
    expect(screen.queryByTestId('m')).not.toBeInTheDocument();
  });

  it('تعمل داخل createPortal ويغلقها Escape', async () => {
    const user = userEvent.setup();
    render(<ProbePortal />);
    await user.click(screen.getByRole('button', { name: 'افتح بوابة' }));
    expect(screen.getByRole('menu', { name: 'افتح بوابة' })).toBeInTheDocument();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('menu', { name: 'افتح بوابة' })).not.toBeInTheDocument();
  });
});