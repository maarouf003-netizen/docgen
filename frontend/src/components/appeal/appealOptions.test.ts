import { describe, expect, it } from 'vitest';
import { buildAppellantOptions } from './appealOptions';
import { APPEAL_DIRECTION_AGAINST_US, APPEAL_DIRECTION_APPELLANTS } from '../../utils/appealStatus';
import { makeDocument } from '../../test/factories';
import type { DocumentResponse } from '../../types';

describe('buildAppellantOptions', () => {
  it('في وضع «طالبة تنفيذ» ومستأنِفين: الجهات العامة الطالبة فقط', () => {
    const doc = {
      ...makeDocument(),
      generalEntitySide: 'applicant',
      applicantPublicEntities: [
        { id: 11, name: 'جهة أ' },
        { id: 12, name: 'جهة ب' },
      ],
    } as unknown as DocumentResponse;

    const options = buildAppellantOptions(doc, APPEAL_DIRECTION_APPELLANTS);
    expect(options.map((o) => `${o.kind}:${o.partyId}`)).toEqual(['applicant-entity:11', 'applicant-entity:12']);
    expect(options[0].name).toBe('جهة أ');
  });

  it('في وضع «طالبة تنفيذ» ومستأنف علينا: المقترض والكفلاء وورثته', () => {
    const doc = {
      ...makeDocument({
        borrowerName: 'أحمد',
        borrowerFather: 'خالد',
        borrowerFamily: 'الخطيب',
      }),
      generalEntitySide: 'applicant',
      guarantors: [{ id: 5, name: 'كفيل', father: 'أ', family: 'ب' }],
      borrowerHeirs: [{ id: 7, name: 'وارث', father: 'أحمد', family: 'الخطيب' }],
    } as unknown as DocumentResponse;

    const options = buildAppellantOptions(doc, APPEAL_DIRECTION_AGAINST_US);
    expect(options.map((o) => o.kind)).toEqual(['borrower', 'guarantor', 'heir']);
    expect(options[0].name).toBe('أحمد خالد الخطيب');
    expect(options[0].partyId).toBe(doc.id);
  });

  it('في وضع «منفذ عليه»: الطبيعيون ثم الجهات ثم كل الورثة (من الطبيعيين وطالبي التنفيذ)', () => {
    const doc = {
      ...makeDocument(),
      generalEntitySide: 'executed',
      executedNaturalPersons: [
        { id: 21, name: 'سامر', father: 'نبيل', family: 'الحلبي', heirs: [{ id: 31, heirName: 'وريث سامر' }] },
      ],
      executedPublicEntities: [{ id: 41, entityName: 'شركة المياه' }],
      executionApplicants: [
        { id: 61, name: 'طالب', father: 'تنفيذ', family: 'عام', heirs: [{ id: 71, heirName: 'وريث طالب' }] },
      ],
    } as unknown as DocumentResponse;

    const options = buildAppellantOptions(doc, APPEAL_DIRECTION_AGAINST_US);
    expect(options.map((o) => o.kind)).toEqual([
      'executed-natural',
      'executed-public',
      'executed-heir',
      'executed-heir',
    ]);
    expect(options.map((o) => o.partyId)).toEqual([21, 41, 71, 31]);
  });

  it('في وضع «منفذ عليه» ومستأنِفين: طالبو التنفيذ', () => {
    const doc = {
      ...makeDocument(),
      generalEntitySide: 'deposit',
      executionApplicants: [{ id: 51, name: 'طالب', father: 'تنفيذ', family: 'عام' }],
    } as unknown as DocumentResponse;

    const options = buildAppellantOptions(doc, APPEAL_DIRECTION_APPELLANTS);
    expect(options.map((o) => o.kind)).toEqual(['execution-applicant']);
    expect(options[0].name).toBe('طالب تنفيذ عام');
  });
});
