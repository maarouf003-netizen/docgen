import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { UsersListTable } from './UsersListTable';
import type { UserListItem } from '../../types';

function user(overrides: Partial<UserListItem> = {}): UserListItem {
  return {
    id: 1,
    fullName: 'أحمد محمد الخطيب',
    username: 'أحمد محمد الخطيب',
    role: 'lawyer',
    isActive: true,
    ...overrides,
  } as UserListItem;
}

describe('UsersListTable', () => {
  it('يعرض الجدول على المكتبي بحالة «مفعّل» وزر تعديل', () => {
    const onEdit = vi.fn();
    render(
      <UsersListTable
        users={[user()]}
        loading={false}
        isMobile={false}
        isManager={false}
        onEdit={onEdit}
      />,
    );
    const table = screen.getByRole('table');
    expect(within(table).getByText('مفعّل')).toBeInTheDocument();
    expect(within(table).getByRole('button', { name: 'تعديل' })).toBeInTheDocument();
  });

  it('يعرض بطاقات الجوال ويحجب تعديل المشرف عن المدير', () => {
    render(
      <UsersListTable
        users={[user({ role: 'admin' })]}
        loading={false}
        isMobile
        isManager
        onEdit={vi.fn()}
      />,
    );
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(screen.getByText('مشرف')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
  });

  it('يعرض الحالة الفارغة بلا انهيار', () => {
    render(
      <UsersListTable users={[]} loading={false} isMobile={false} isManager={false} onEdit={vi.fn()} />,
    );
    expect(screen.getByText('لا يوجد مستخدمون')).toBeInTheDocument();
  });
});
