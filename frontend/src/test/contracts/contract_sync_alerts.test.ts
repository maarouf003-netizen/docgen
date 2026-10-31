import { describe, expect, expectTypeOf, it } from 'vitest';
import manifest from './head-alert-contracts.json';
import type {
  CreateHeadAlertRequest,
  HeadAlertDto,
  HeadAlertSummaryDto,
  PagedResult,
} from '../../types';

/**
 * حارس التطابق السلكي C#↔TS للتنبيهات عبر البيان المشترك
 * `head-alert-contracts.json` (يقرأه الخلفي `HeadAlertContractTests`).
 *
 * القوائم أدناه مكتوبة بيد بـ`satisfies readonly (keyof T)[]`: أي مفتاح مكتوب
 * لا وجود له في النوع يُفشل `tsc -b`، وأي مفتاح في النوع غير مذكور يُكشف عبر
 * `Exclude<keyof T, …>` مع `expectTypeOf(...).toEqualTypeOf<never>()` (لا مقارنة
 * كائنات كاملة وقت التشغيل — الحقول الاختيارية كانت ستُسقط الحارس بصمت).
 *
 * الاتجاه الآخر يُثبته تشغيل `Object.keys` على البيان نفسه: قائمة البيان = القائمة
 * المكتوبة حرفيًا، والخلفي يثبت أن البيان = أسماء أعضاء السجلات الفعلية.
 */
const summaryKeys = [
  'id',
  'kind',
  'messagePreview',
  'messageLength',
  'previewTruncated',
  'targetType',
  'documentId',
  'documentTitle',
  'targetLawyerId',
  'targetLawyerName',
  'isRead',
  'recipientCount',
  'unreadCount',
  'createdAt',
  'lastContentChangedAt',
  'createdByName',
  'appealId',
  'reviewLetterId',
  'delegationId',
  'executionCircuitId',
  'isArchived',
  'archivedAt',
] as const satisfies readonly (keyof HeadAlertSummaryDto)[];

const detailKeys = [
  'id',
  'kind',
  'message',
  'targetType',
  'documentId',
  'documentTitle',
  'targetLawyerId',
  'targetLawyerName',
  'delegationId',
  'appealId',
  'reviewLetterId',
  'publicEntityId',
  'executionCircuitId',
  'createdAt',
  'lastContentChangedAt',
  'isArchived',
  'archivedAt',
  'createdByName',
  'isRead',
  'canViewRecipients',
  // محتفظ بهما مؤقتًا حتى PR-6 (كتلة `retainedHeadAlertDetail` في البيان).
  'recipientCount',
  'unreadCount',
] as const satisfies readonly (keyof HeadAlertDto)[];

const pagedKeys = [
  'items',
  'page',
  'perPage',
  'totalCount',
  'totalPages',
] as const satisfies readonly (keyof PagedResult<unknown>)[];

const createKeys = [
  'targetType',
  'documentId',
  'targetLawyerId',
  'message',
] as const satisfies readonly (keyof CreateHeadAlertRequest)[];

type SummaryKey = keyof HeadAlertSummaryDto;
type DetailKey = keyof HeadAlertDto;

describe('contract_sync_alerts — type contract', () => {
  it('مفاتيح `HeadAlertSummaryDto` = كتلة `headAlertSummary` حرفيًا (لا زيادة ولا نقصان)', () => {
    expectTypeOf<Exclude<SummaryKey, (typeof summaryKeys)[number]>>().toEqualTypeOf<never>();
    expect([...summaryKeys].sort()).toEqual([...manifest.headAlertSummary].sort());
  });

  it('مفاتيح `HeadAlertDto` = `headAlertDetail` + `retainedHeadAlertDetail` حرفيًا', () => {
    expectTypeOf<Exclude<DetailKey, (typeof detailKeys)[number]>>().toEqualTypeOf<never>();
    expect([...detailKeys].sort()).toEqual(
      [...manifest.headAlertDetail, ...manifest.retainedHeadAlertDetail].sort(),
    );
  });

  it('`PagedResult<T>` يُستورد من `types/common.ts` ومفاتيحه = بيان `pagedResult`', () => {
    expectTypeOf<Exclude<keyof PagedResult<unknown>, (typeof pagedKeys)[number]>>().toEqualTypeOf<never>();
    expect([...pagedKeys].sort()).toEqual([...manifest.pagedResult].sort());
  });

  it('حمولة `CreateHeadAlertRequest` = كتلة `createHeadAlertRequest` (لا حقول دورة حياة)', () => {
    expectTypeOf<Exclude<keyof CreateHeadAlertRequest, (typeof createKeys)[number]>>().toEqualTypeOf<never>();
    expect([...createKeys].sort()).toEqual([...manifest.createHeadAlertRequest].sort());
  });
});

describe('contract_sync_alerts — forbidden fields', () => {
  it('لا نص كامل في الملخص: `message`/`hasFullMessage` ممنوعان نوعًا وبيانًا', () => {
    expectTypeOf<Extract<'message' | 'hasFullMessage', SummaryKey>>().toEqualTypeOf<never>();
    expect(
      manifest.forbidden.headAlertSummary.filter((key) => (summaryKeys as readonly string[]).includes(key)),
    ).toEqual([]);
    // الملخص يحمل المعاينة وحدها — وهذا ما تعتمده الواجهة فعليًا.
    expect(summaryKeys).toContain('messagePreview');
    expect(summaryKeys).toContain('previewTruncated');
  });

  it('لا `recipients` في التفاصيل (تُطلب من سطحها المستقل)', () => {
    expectTypeOf<Extract<'recipients', DetailKey>>().toEqualTypeOf<never>();
    expect(
      manifest.forbidden.headAlertDetail.filter((key) => (detailKeys as readonly string[]).includes(key)),
    ).toEqual([]);
  });

  it('لا `hasNextPage`/`pageSize` في `PagedResult` (الاسم المعتمد `perPage`)', () => {
    expectTypeOf<
      Extract<'hasNextPage' | 'pageSize', keyof PagedResult<unknown>>
    >().toEqualTypeOf<never>();
    expect(
      manifest.forbidden.pagedResult.filter((key) => (pagedKeys as readonly string[]).includes(key)),
    ).toEqual([]);
  });
});
