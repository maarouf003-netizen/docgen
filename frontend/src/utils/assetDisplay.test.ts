import { describe, it, expect } from 'vitest';
import { ASSET_KINDS } from '../components/form/documentFormConstants';
import type { AssetDto } from '../types';
import { assetDisplayName } from './assetDisplay';

function asset(overrides: Partial<AssetDto> & { id?: number }): AssetDto {
  return { id: 5, assetKind: ASSET_KINDS.realEstate, ...overrides } as AssetDto;
}

/**
 * تثبيت تكافؤ التسمية مع الخلفية (E2 — بلا تغيير سلوكي): كل حالة مرآة حرفية
 * لـ AssetDisplay.Label (Application/Common/AssetDisplay.cs) عندما يكون id حاضرًا
 * (وهو حاضر دائمًا في مسارات الإنابة). حالة غياب id موثقة حدًّا لا إصلاحًا.
 */
describe('assetDisplayName — parity with backend AssetDisplay.Label', () => {
  it('عقار: الوصف ثم الرقم ثم المعرف', () => {
    expect(assetDisplayName(asset({ property: 'بيت المزة' }))).toBe('بيت المزة');
    expect(assetDisplayName(asset({ property: '  ', propertyNumber: '77' }))).toBe('عقار رقم 77');
    expect(assetDisplayName(asset({ propertyNumber: '  ' }))).toBe('عقار 5');
  });

  it('E2: رقم عقاري بمسافات محيطة يُطبَّع (مرآة التقليم الخلفي)', () => {
    expect(assetDisplayName(asset({ propertyNumber: ' 77 ' }))).toBe('عقار رقم 77');
    expect(assetDisplayName(asset({ propertyNumber: '  78  ' }))).toBe('عقار رقم 78');
  });

  it('مركبة: النوع واللوحة ثم اللوحة ثم النوع ثم المعرف', () => {
    expect(
      assetDisplayName(asset({ assetKind: ASSET_KINDS.vehicle, vehicleType: 'سيارة', plateNumber: '123' })),
    ).toBe('مركبة سيارة — لوحة 123');
    expect(assetDisplayName(asset({ assetKind: ASSET_KINDS.vehicle, plateNumber: '123' }))).toBe(
      'مركبة لوحة 123',
    );
    expect(
      assetDisplayName(asset({ assetKind: ASSET_KINDS.vehicle, vehicleType: 'سيارة' })),
    ).toBe('مركبة سيارة');
    expect(assetDisplayName(asset({ assetKind: ASSET_KINDS.vehicle }))).toBe('مركبة 5');
  });

  it('متجر: رقم السجل ثم الوصف ثم المعرف', () => {
    expect(assetDisplayName(asset({ assetKind: ASSET_KINDS.shop, registerNumber: '888' }))).toBe(
      'متجر سجل رقم 888',
    );
    expect(
      assetDisplayName(asset({ assetKind: ASSET_KINDS.shop, shopDescription: 'متجر أقمشة' })),
    ).toBe('متجر أقمشة');
    expect(assetDisplayName(asset({ assetKind: ASSET_KINDS.shop }))).toBe('متجر 5');
  });

  it('متجر غير مسجل: الترخيص ثم المعرف', () => {
    expect(
      assetDisplayName(asset({ assetKind: ASSET_KINDS.unregisteredShop, licenseNumber: 'L-9' })),
    ).toBe('متجر غير مسجل ترخيص رقم L-9');
    expect(assetDisplayName(asset({ assetKind: ASSET_KINDS.unregisteredShop }))).toBe(
      'متجر غير مسجل 5',
    );
  });

  it('كفالة رواتب: عرض أمامي خالص (الخلفية ترفض الإنابة عليها قبل التسمية)', () => {
    expect(
      assetDisplayName(asset({ assetKind: ASSET_KINDS.salaryGuarantee, publicEntity: 'مؤسسة المياه' })),
    ).toBe('كفالة رواتب — مؤسسة المياه');
    expect(assetDisplayName(asset({ assetKind: ASSET_KINDS.salaryGuarantee }))).toBe('كفالة رواتب');
  });

  it('حدّ موثق: غياب id يسقط الرقم (لا يقع في مسارات الإنابة)', () => {
    const noId = { assetKind: ASSET_KINDS.vehicle } as AssetDto;
    expect(assetDisplayName(noId)).toBe('مركبة');
  });
});
