import MultiAmountEditor from './MultiAmountEditor';
import { FieldInput, SelectInput } from './form/FormInputs';
import { assetDisplayName } from '../utils/assetDisplay';
import { isAuctionableKind } from './form/documentFormConstants';
import {
  EXEC_STATUS_DEFERRED,
  EXEC_STATUS_FORCIBLY,
  EXEC_STATUS_REFERRED_TO_START,
  EXEC_STATUS_SETTLED,
  EXEC_STATUS_STRUCK_OFF,
  STATUS_ACTION_COMPLETE_SALE,
  STATUS_ACTION_RETURN_CIRCULATING,
  STATUS_ACTION_RETURN_PARTIAL,
  STATUS_ACTION_REVERT,
  SUB_STATUS_FULL,
  SUB_STATUS_PARTIAL,
} from '../utils/documentStatus';
import { COLLECTED_AMOUNT_KEYS, COLLECTED_CURRENCY_KEYS, DATE_PLACEHOLDER } from './statusChangeModel';
import type { StatusFields } from './statusChangeModel';
import type { AssetDto, DocumentResponse } from '../types';

interface StatusChangeTargetFieldsProps {
  target: string;
  fields: StatusFields;
  set: <K extends keyof StatusFields>(key: K, value: StatusFields[K]) => void;
  collectedSlots: number;
  setCollectedSlots: (n: number) => void;
  doc: DocumentResponse;
  toggleEstate: (id: number) => void;
}

/** حقول الإجراء المختار في نافذة تغيير الحالة (منقولة حرفيًا من `StatusChangeModal`). */
export function StatusChangeTargetFields({
  target,
  fields,
  set,
  collectedSlots,
  setCollectedSlots,
  doc,
  toggleEstate,
}: StatusChangeTargetFieldsProps) {
  return (
    <>
      {target === EXEC_STATUS_DEFERRED && (
        <div className="grid sm:grid-cols-2 gap-3">
          <FieldInput id="tarithNumber" label="رقم كتاب التريث" value={fields.tarithNumber} onChange={(v) => set('tarithNumber', v)} />
          <FieldInput id="tarithDate" label="تاريخ كتاب التريث" value={fields.tarithDate} onChange={(v) => set('tarithDate', v)} placeholder={DATE_PLACEHOLDER} />
          <FieldInput id="tarithRegNumber" label="رقم ورود كتاب التريث" value={fields.tarithRegNumber} onChange={(v) => set('tarithRegNumber', v)} />
          <FieldInput id="tarithRegDate" label="تاريخ ورود كتاب التريث" value={fields.tarithRegDate} onChange={(v) => set('tarithRegDate', v)} placeholder={DATE_PLACEHOLDER} />
        </div>
      )}

      {target === EXEC_STATUS_SETTLED && (
        <div className="grid gap-4">
          <div className="grid sm:grid-cols-2 gap-3">
            <FieldInput id="baraetNumber" label="رقم كتاب براءة الذمة" value={fields.baraetNumber} onChange={(v) => set('baraetNumber', v)} />
            <FieldInput id="baraetDate" label="تاريخ كتاب براءة الذمة" value={fields.baraetDate} onChange={(v) => set('baraetDate', v)} placeholder={DATE_PLACEHOLDER} />
            <FieldInput id="baraetRegNumber" label="رقم ورود كتاب براءة الذمة" value={fields.baraetRegNumber} onChange={(v) => set('baraetRegNumber', v)} />
            <FieldInput id="baraetRegDate" label="تاريخ ورود كتاب براءة الذمة" value={fields.baraetRegDate} onChange={(v) => set('baraetRegDate', v)} placeholder={DATE_PLACEHOLDER} />
          </div>
          <MultiAmountEditor
            idPrefix="status-collected"
            amountKeys={COLLECTED_AMOUNT_KEYS}
            currencyKeys={COLLECTED_CURRENCY_KEYS}
            values={fields}
            onSet={(key, value) => set(key as keyof StatusFields, value as never)}
            slots={collectedSlots}
            onSlotsChange={setCollectedSlots}
            firstLabel="المبلغ المحصل"
            otherLabel={(i) => `المبلغ المحصل ${i + 1}`}
          />
        </div>
      )}

      {target === EXEC_STATUS_FORCIBLY && (
        <div className="grid gap-4">
          <SelectInput
            id="execSubStatus"
            label="نوع التنفيذ"
            value={fields.execSubStatus}
            onChange={(v) => set('execSubStatus', v)}
            options={[SUB_STATUS_PARTIAL, SUB_STATUS_FULL]}
          />
          <FieldInput
            id="forcedExecutionDate"
            label="تاريخ قرار الإحالة القطعية"
            value={fields.forcedExecutionDate}
            onChange={(v) => set('forcedExecutionDate', v)}
            placeholder={DATE_PLACEHOLDER}
          />
          <MultiAmountEditor
            idPrefix="status-collected"
            amountKeys={COLLECTED_AMOUNT_KEYS}
            currencyKeys={COLLECTED_CURRENCY_KEYS}
            values={fields}
            onSet={(key, value) => set(key as keyof StatusFields, value as never)}
            slots={collectedSlots}
            onSlotsChange={setCollectedSlots}
            firstLabel="المبلغ المحصل"
            otherLabel={(i) => `المبلغ المحصل ${i + 1}`}
          />
          <div>
            <p className="block text-xs font-bold text-gray-600 mb-1">
              الأموال المباعة بالمزاد العلني
            </p>
            {(() => {
              const auctionable = (doc.assets ?? []).filter(
                (r): r is AssetDto & { id: number } => r.id != null && isAuctionableKind(r.assetKind),
              );
              return auctionable.length === 0 ? (
                <p className="text-gray-400 text-sm">لا توجد أموال قابلة للبيع في الملف</p>
              ) : (
                <div className="border border-gray-300 rounded-lg p-3 space-y-2">
                  {auctionable.map((r) => {
                    const checked = fields.soldAssetIds.includes(r.id);
                    return (
                      <label key={r.id} className="flex items-center gap-2 min-h-11 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleEstate(r.id)}
                          className="w-4 h-4"
                        />
                        <span className="text-sm text-gray-800">{assetDisplayName(r)}</span>
                      </label>
                    );
                  })}
                </div>
              );
            })()}
          </div>
        </div>
      )}

      {target === STATUS_ACTION_COMPLETE_SALE && (
        <div className="grid sm:grid-cols-2 gap-3">
          <FieldInput
            id="forcedTransferDate"
            label="تاريخ تحويل بدل المبيع للجهة العامة"
            value={fields.forcedTransferDate}
            onChange={(v) => set('forcedTransferDate', v)}
            placeholder={DATE_PLACEHOLDER}
          />
          <FieldInput
            id="forcedTransferNoticeNumber"
            label="رقم إشعار التحويل (اختياري)"
            value={fields.forcedTransferNoticeNumber}
            onChange={(v) => set('forcedTransferNoticeNumber', v)}
          />
        </div>
      )}

      {target === EXEC_STATUS_STRUCK_OFF && (
        <FieldInput id="struckOffDate" label="تاريخ الشطب" value={fields.struckOffDate} onChange={(v) => set('struckOffDate', v)} placeholder={DATE_PLACEHOLDER} />
      )}

      {target === STATUS_ACTION_REVERT && (
        <div className="grid sm:grid-cols-2 gap-3">
          <FieldInput id="sayerNumber" label="رقم كتاب الجهة العامة بالسير بالملف" value={fields.sayerNumber} onChange={(v) => set('sayerNumber', v)} />
          <FieldInput id="sayerDate" label="تاريخ كتاب الجهة العامة بالسير بالملف" value={fields.sayerDate} onChange={(v) => set('sayerDate', v)} placeholder={DATE_PLACEHOLDER} />
          <FieldInput id="sayerRegNumber" label="رقم ورود كتاب بالسير بالملف" value={fields.sayerRegNumber} onChange={(v) => set('sayerRegNumber', v)} />
          <FieldInput id="sayerRegDate" label="تاريخ ورود كتاب بالسير بالملف" value={fields.sayerRegDate} onChange={(v) => set('sayerRegDate', v)} placeholder={DATE_PLACEHOLDER} />
        </div>
      )}

      {target === EXEC_STATUS_REFERRED_TO_START && (
        <div className="grid gap-4">
          <div className="grid sm:grid-cols-2 gap-3">
            <FieldInput
              id="noFundsDemandNumber"
              label="رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها"
              value={fields.noFundsDemandNumber}
              onChange={(v) => set('noFundsDemandNumber', v)}
            />
            <FieldInput
              id="noFundsDemandDate"
              label="تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها"
              value={fields.noFundsDemandDate}
              onChange={(v) => set('noFundsDemandDate', v)}
              placeholder={DATE_PLACEHOLDER}
            />
            <FieldInput
              id="startReferralNumber"
              label="رقم كتاب الإحالة (اختياري)"
              value={fields.startReferralNumber}
              onChange={(v) => set('startReferralNumber', v)}
            />
            <FieldInput
              id="startReferralDate"
              label="تاريخ كتاب الإحالة (اختياري)"
              value={fields.startReferralDate}
              onChange={(v) => set('startReferralDate', v)}
              placeholder={DATE_PLACEHOLDER}
            />
          </div>
          {(() => {
            const auctionable = (doc.assets ?? []).filter(
              (r): r is AssetDto & { id: number } => r.id != null && isAuctionableKind(r.assetKind),
            );
            return auctionable.length === 0 ? null : (
              <div
                role="note"
                className="rounded-lg bg-amber-50 border border-amber-200 px-3 py-2 text-sm text-amber-800"
              >
                وجد في الملف أموال قابلة للبيع بالمزاد العلني — تأكد من عدم وجود أموال
                للتنفيذ عليها قبل الإحالة إلى البداية.
              </div>
            );
          })()}
        </div>
      )}

      {(target === STATUS_ACTION_RETURN_CIRCULATING
        || target === STATUS_ACTION_RETURN_PARTIAL) && (
        <div className="grid gap-4">
          <div
            role="note"
            className="rounded-lg bg-purple-50 border border-purple-200 px-3 py-2 text-sm text-purple-800"
          >
            {target === STATUS_ACTION_RETURN_CIRCULATING
              ? 'أعيد السير بالملف إلى المتداول بعد موافاة قسم التنفيذ بأموال للتنفيذ عليها.'
              : 'يعود الملف إلى «منفذ جبريا» محافظًا على المبلغ المحصل العائد — أكمل بيانات التنفيذ الجبري عند الحاجة.'}
          </div>
          <div className="grid sm:grid-cols-2 gap-3">
            <FieldInput
              id="renewalFileNumber"
              label="رقم الملف الجديد (اختياري)"
              value={fields.renewalFileNumber}
              onChange={(v) => set('renewalFileNumber', v)}
            />
            <FieldInput
              id="renewalYear"
              label="سنة الإعادة (اختياري)"
              value={fields.renewalYear}
              onChange={(v) => set('renewalYear', v)}
              placeholder="مثال: 2026"
            />
            <FieldInput
              id="renewalDate"
              label="تاريخ التجديد (اختياري)"
              value={fields.renewalDate}
              onChange={(v) => set('renewalDate', v)}
              placeholder={DATE_PLACEHOLDER}
            />
            <FieldInput
              id="renewalFileType"
              label="نوع الملف الجديد (اختياري)"
              value={fields.renewalFileType}
              onChange={(v) => set('renewalFileType', v)}
            />
            {target === STATUS_ACTION_RETURN_CIRCULATING && (
              <FieldInput
                id="struckOffDate"
                label="تاريخ شطب الملف السابق (اختياري)"
                value={fields.struckOffDate}
                onChange={(v) => set('struckOffDate', v)}
                placeholder={DATE_PLACEHOLDER}
              />
            )}
          </div>
        </div>
      )}
    </>
  );
}
