import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useDocumentViewModals } from './useDocumentViewModals';
import type { AppealDto, DelegationDto } from '../../types';

const appeal: AppealDto = {
  id: 1,
  documentId: 10,
  direction: 'appellants',
  directionLabel: 'مستأنِفين',
  status: 'pending',
  statusLabel: 'منظور',
  appellants: [],
  appellees: [],
  needsRotation: false,
  createdAt: '2026-11-01',
  createdById: 1,
  partiesDegraded: false,
};

const delegation: DelegationDto = {
  id: 5,
  sourceDocumentId: 10,
  targetDocumentId: 11,
  isExternal: false,
  status: 'assigned',
  createdAt: '2026-11-26',
  createdById: 1,
  assets: [],
};

describe('useDocumentViewModals', () => {
  it('يبدأ كل النوافذ مغلقة بلا استئناف محدد', () => {
    const { result } = renderHook(() => useDocumentViewModals('10'));
    expect(result.current.actionsOpen).toBe(false);
    expect(result.current.statusOpen).toBe(false);
    expect(result.current.generationOpen).toBe(false);
    expect(result.current.delegationFormOpen).toBe(false);
    expect(result.current.appealFormVariant).toBeNull();
    expect(result.current.infoAppeal).toBeNull();
    expect(result.current.deleteTarget).toBeNull();
  });

  it('يفتح ويغلق النوافذ المستقلة عبر setters نفسها', () => {
    const { result } = renderHook(() => useDocumentViewModals('10'));
    act(() => {
      result.current.setActionsOpen(true);
      result.current.setGenerationOpen(true);
      result.current.setAppealFormVariant('against-us');
      result.current.setInfoAppeal(appeal);
    });
    expect(result.current.actionsOpen).toBe(true);
    expect(result.current.generationOpen).toBe(true);
    expect(result.current.appealFormVariant).toBe('against-us');
    expect(result.current.infoAppeal).toEqual(appeal);

    act(() => {
      result.current.setActionsOpen(false);
      result.current.setGenerationOpen(false);
      result.current.setAppealFormVariant(null);
      result.current.setInfoAppeal(null);
    });
    expect(result.current.actionsOpen).toBe(false);
    expect(result.current.generationOpen).toBe(false);
    expect(result.current.appealFormVariant).toBeNull();
    expect(result.current.infoAppeal).toBeNull();
  });

  it('عند تبديل الملف يُغلق الاستئناف فقط ويبقى ما عداه كما هو', () => {
    const { result, rerender } = renderHook(
      ({ id }: { id: string }) => useDocumentViewModals(id),
      { initialProps: { id: '10' } },
    );
    act(() => {
      result.current.setActionsOpen(true);
      result.current.setAppealFormVariant('appellants');
      result.current.setInfoAppeal(appeal);
      result.current.setDeleteTarget(delegation);
    });

    rerender({ id: '11' });

    expect(result.current.appealFormVariant).toBeNull();
    expect(result.current.infoAppeal).toBeNull();
    // لا تُغلق بقية النوافذ عند تبديل الملف — سلوك الصفحة الأصلي نفسه.
    expect(result.current.actionsOpen).toBe(true);
    expect(result.current.deleteTarget).not.toBeNull();
  });
});
