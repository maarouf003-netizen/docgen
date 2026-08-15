import { describe, it, expect, beforeEach } from 'vitest';
import {
  loadDocumentsListPosition,
  saveDocumentsListPosition,
  loadLastViewedDocumentId,
  saveLastViewedDocumentId,
} from './listSession';

describe('listSession', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('يحفظ ويستعيد موضع القائمة كاملًا', () => {
    saveDocumentsListPosition({
      query: 'محمود',
      status: 'منفذ',
      applicant: 'المدعي',
      court: 'دمشق',
      lawyer: 'المحامي سامر',
      administrativeBranch: 'الفرع الرئيسي - دمشق',
      executedEntity: 'المصرف العقاري',
      publicEntityBranch: 'فرع المزة',
      page: 3,
    });

    expect(loadDocumentsListPosition()).toEqual({
      query: 'محمود',
      status: 'منفذ',
      applicant: 'المدعي',
      court: 'دمشق',
      lawyer: 'المحامي سامر',
      administrativeBranch: 'الفرع الرئيسي - دمشق',
      executedEntity: 'المصرف العقاري',
      publicEntityBranch: 'فرع المزة',
      page: 3,
    });
  });

  it('يُعيد null عند غياب موضع محفوظ أو تلفه أو صفحة غير صالحة', () => {
    expect(loadDocumentsListPosition()).toBeNull();
    sessionStorage.setItem('documentsListPosition', '{not-json');
    expect(loadDocumentsListPosition()).toBeNull();
    sessionStorage.setItem('documentsListPosition', JSON.stringify({ page: 0, query: 5 }));
    const restored = loadDocumentsListPosition();
    expect(restored).toEqual({
      query: '',
      status: '',
      applicant: '',
      court: '',
      lawyer: '',
      administrativeBranch: '',
      executedEntity: '',
      publicEntityBranch: '',
      page: 1,
    });
  });

  it('يُسجّل ويستعيد آخر ملف مفتوح، ويُمسح عند القيمة الفارغة', () => {
    expect(loadLastViewedDocumentId()).toBeNull();
    saveLastViewedDocumentId(7);
    expect(loadLastViewedDocumentId()).toBe(7);
    saveLastViewedDocumentId(null);
    expect(loadLastViewedDocumentId()).toBeNull();
  });

  it('يمسح آخر ملف مفتوح عند معرّف غير رقمي بدل تخزين قمامة في الجلسة', () => {
    saveLastViewedDocumentId(7);
    saveLastViewedDocumentId(Number('abc'));
    expect(loadLastViewedDocumentId()).toBeNull();
    expect(sessionStorage.getItem('lastViewedDocumentId')).toBeNull();
  });
});
