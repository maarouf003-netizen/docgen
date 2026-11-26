import type { DocumentAnnexDto, DocumentResponse } from '../../types';

/** صف ملحق جديد فارغ — الترتيب يُحسب عند الحفظ. */
export function emptyAnnex(): DocumentAnnexDto {
  return { sequence: 0, annexType: '', annexNumber: '', annexDate: '' };
}

/** توافق خلفي: الملحق المفرد القديم يُقرأ كصف واحد عند غياب القائمة. */
export function legacyToAnnexes(d: Pick<DocumentResponse, 'annexType' | 'annexNumber' | 'annexDate'>): DocumentAnnexDto[] {
  if (![d.annexType, d.annexNumber, d.annexDate].some((v) => (v ?? '').trim())) return [];
  return [{ sequence: 0, annexType: d.annexType ?? '', annexNumber: d.annexNumber ?? '', annexDate: d.annexDate ?? '' }];
}
