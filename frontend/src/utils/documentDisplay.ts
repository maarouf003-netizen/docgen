import type { DocumentResponse } from '../types';

/** الاسم الثلاثي من مكوناته، متجاهلًا أي مكوّن فارغ. */
export function tripleName(name?: string, father?: string, family?: string): string {
  return [name, father, family].filter(Boolean).join(' ');
}

/** رقم الملف بضم نوعه إن وُجد. */
export function fileNumberLabel(number: string | undefined, type: string | undefined): string {
  const num = number ?? '';
  return type ? `${num} ${type}`.trim() : num;
}

/**
 * اسم أول منفذٍ عليه في وضع «منفذ عليه»: الشخص الطبيعي الأول (اسم ثلاثي)،
 * ثم أول جهة عامة، ثم طالب التنفيذ — ويُستخدم لعرض عمود «المنفذ عليه».
 */
export function executedFullName(d: DocumentResponse): string {
  const person = d.executedNaturalPersons?.[0];
  const personName = person ? tripleName(person.name, person.father, person.family) : '';
  const entity = d.executedPublicEntities?.[0]?.entityName ?? '';
  const applicant = d.applicant ?? '';
  return personName || entity || applicant || '';
}

/** هل الملف من عائلة وضع «منفذ عليه» (executed أو deposit)؟ */
export function isExecutedLike(side?: string): boolean {
  return side === 'executed' || side === 'deposit';
}

/**
 * اسم منفذٍ عليه المعروض: في عائلة وضع «منفذ عليه» اسم أول منفذٍ عليه (شخصًا أو جهة)،
 * وإلا اسم المقترض الثلاثي.
 */
export function fullName(d: DocumentResponse) {
  if (isExecutedLike(d.generalEntitySide)) return executedFullName(d);
  return tripleName(d.borrowerName, d.borrowerFather, d.borrowerFamily);
}

/** اسم طالب التنفيذ/العرض: في عائلة وضع «منفذ عليه» يُؤخذ من أول «طالب تنفيذ/عرض» (اسم ثلاثي)، وإلا من الحقل المباشر. */
export function applicantName(d: DocumentResponse): string {
  if (isExecutedLike(d.generalEntitySide)) {
    const a = d.executionApplicants?.[0];
    const name = a ? tripleName(a.name, a.father, a.family) : '';
    if (name) return name;
  }
  return d.applicant ?? '';
}

/**
 * فرع الجهة العامة المعروض في قائمة الملفات التنفيذية: في عائلة وضع «منفذ عليه» فرع أول
 * جهة عامة منفذ عليها (public)، وإلا فرع أول جهة عامة طالبة للتنفيذ. يُرجع فارغًا إن غاب.
 */
export function publicEntityBranch(d: DocumentResponse): string {
  if (isExecutedLike(d.generalEntitySide)) {
    return d.executedPublicEntities?.find((e) => e.nature === 'public')?.entityBranch ?? '';
  }
  return d.applicantPublicEntities?.[0]?.branch ?? '';
}

/** رقم الملف المعروض: المسودة بلا رقم، والنوع يُضمّن إلى الرقم عند وجوده. */
export function displayFileNumber(d: DocumentResponse) {
  if (d.isDraft) return '';
  return fileNumberLabel(d.displayFileNumber ?? d.fileNumber, d.fileType);
}
