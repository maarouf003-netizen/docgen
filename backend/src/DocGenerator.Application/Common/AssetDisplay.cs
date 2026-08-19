using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// تسمية قراءة لأصل (عقار/مركبة/متجر/متجر غير مسجل) — مصدر الحقيقة الوحيد لوصف الأصل
/// في النصوص والإنابات (مثال: «عقار رقم 77 — المزة»). يُستخدم لتقط لقطة أصول الإنابة.
/// </summary>
public static class AssetDisplay
{
    public static string Label(Asset a)
    {
        var kind = a.AssetKind;
        if (kind == AssetKindCatalog.RealEstate)
        {
            var property = (a.Property ?? string.Empty).Trim();
            if (property.Length > 0) return property;
            return string.IsNullOrWhiteSpace(a.PropertyNumber) ? $"عقار {a.Id}" : $"عقار رقم {a.PropertyNumber}";
        }
        if (kind == AssetKindCatalog.Vehicle)
        {
            var type = (a.VehicleType ?? string.Empty).Trim();
            var plate = (a.PlateNumber ?? string.Empty).Trim();
            if (type.Length > 0 && plate.Length > 0) return $"مركبة {type} — لوحة {plate}";
            if (plate.Length > 0) return $"مركبة لوحة {plate}";
            return type.Length > 0 ? $"مركبة {type}" : $"مركبة {a.Id}";
        }
        if (kind == AssetKindCatalog.Shop)
        {
            var reg = (a.RegisterNumber ?? string.Empty).Trim();
            if (reg.Length > 0) return $"متجر سجل رقم {reg}";
            return string.IsNullOrWhiteSpace(a.ShopDescription) ? $"متجر {a.Id}" : a.ShopDescription.Trim();
        }
        if (kind == AssetKindCatalog.UnregisteredShop)
        {
            var license = (a.LicenseNumber ?? string.Empty).Trim();
            if (license.Length > 0) return $"متجر غير مسجل ترخيص رقم {license}";
            return $"متجر غير مسجل {a.Id}";
        }
        return string.Empty;
    }
}
