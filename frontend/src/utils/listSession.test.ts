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

  it('يحفظ «آخر ملف فُتح» سجلًا موقّعًا بالمستخدم ويستعيده للمالك نفسه', () => {
    expect(loadLastViewedDocumentId(1)).toBeNull();
    saveLastViewedDocumentId(7, 1);
    expect(JSON.parse(sessionStorage.getItem('lastViewedDocument') ?? '{}')).toEqual({
      version: 2,
      userId: 1,
      documentId: 7,
    });
    expect(loadLastViewedDocumentId(1)).toBe(7);
  });

  it('يُعيد null ويُمسح السجل عند القيمة الفارغة أو غياب هوية المالك', () => {
    saveLastViewedDocumentId(7, 1);
    saveLastViewedDocumentId(null, 1);
    expect(loadLastViewedDocumentId(1)).toBeNull();

    saveLastViewedDocumentId(7, 1);
    saveLastViewedDocumentId(7, null);
    expect(loadLastViewedDocumentId(1)).toBeNull();
    expect(sessionStorage.getItem('lastViewedDocument')).toBeNull();
  });

  it('يمسح آخر ملف مفتوح عند معرّف غير رقمي بدل تخزين قمامة في الجلسة', () => {
    saveLastViewedDocumentId(7, 1);
    saveLastViewedDocumentId(Number('abc'), 1);
    expect(loadLastViewedDocumentId(1)).toBeNull();
    expect(sessionStorage.getItem('lastViewedDocument')).toBeNull();
  });

  it('يتجاهل سجل مستخدم آخر بلا حذف (القراءة بلا أثر جانبي)', () => {
    saveLastViewedDocumentId(7, 1);
    expect(loadLastViewedDocumentId(2)).toBeNull();
    expect(sessionStorage.getItem('lastViewedDocument')).not.toBeNull();
    expect(loadLastViewedDocumentId(1)).toBe(7);
  });

  it('يُعيد null لسجل تالف الشكل أو إصدار مخالف أو معرف غير صالح دون حذف', () => {
    sessionStorage.setItem('lastViewedDocument', '{not-json');
    expect(loadLastViewedDocumentId(1)).toBeNull();

    sessionStorage.setItem('lastViewedDocument', JSON.stringify({ version: 1, userId: 1, documentId: 7 }));
    expect(loadLastViewedDocumentId(1)).toBeNull();

    sessionStorage.setItem('lastViewedDocument', JSON.stringify({ version: 2, userId: 'x', documentId: 7 }));
    expect(loadLastViewedDocumentId(1)).toBeNull();

    sessionStorage.setItem('lastViewedDocument', JSON.stringify({ version: 2, userId: 1, documentId: -3 }));
    expect(loadLastViewedDocumentId(1)).toBeNull();

    expect(sessionStorage.getItem('lastViewedDocument')).not.toBeNull();
  });

  it('ينظّف المفتاح القديم الرقمي عند أول كتابة بالمفتاح الجديد (لا يُقرأ أبدًا)', () => {
    sessionStorage.setItem('lastViewedDocumentId', '7');
    saveLastViewedDocumentId(9, 1);
    expect(loadLastViewedDocumentId(1)).toBe(9);
    expect(sessionStorage.getItem('lastViewedDocumentId')).toBeNull();
    // القيمة الرقمية المتبقية في المفتاح القديم لا تُقرأ عبر الدالة الجديدة.
    sessionStorage.setItem('lastViewedDocumentId', '7');
    expect(loadLastViewedDocumentId(1)).toBe(9);
  });

  it('لا يقرأ أي شيء بلا هوية مالك (حتى لو وُجد سجل)', () => {
    saveLastViewedDocumentId(7, 1);
    expect(loadLastViewedDocumentId(null)).toBeNull();
  });
});