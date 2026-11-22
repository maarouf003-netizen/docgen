# `P4-02a` — تفكيك `DocumentRepository` إلى ملفات جزئية — 2026-11-22

> **النطاق:** تفكيك `DocumentRepository` (1564 سطرًا) إلى `partial class` موزّعة
> بحسب المسؤولية، **بلا تغيير سلوكي ولا توقيع عام**. الهدف: الاقتراب من حد
> `P4-02` (لا ملف >600 سطر) وتسهيل المراجعة.

## 1. التقسيم

| الملف | المحتوى | الأسطر |
|---|---|---:|
| `DocumentRepository.cs` | usings + التعريف + المنشئ + قراءات الوصول المفردة | **92** |
| `DocumentRepository.Search.cs` | البحث/القوائم المُسقَطة/التصدير/الفلاتر | **534** |
| `DocumentRepository.Owner.cs` | المحذوفات، التزامن، النقل، ملفات المالك | **251** |
| `DocumentRepository.Circuits.cs` | الدوائر، التدوير، العدّادات | **321** |
| `DocumentRepository.Archive.cs` | المشطوبة/المنفذة/المحالة/تذكيرات الشطب | **422** |

- كل الملفات `public partial class DocumentRepository : Repository<Document>, IDocumentRepository`.
- لا تغيير في `IDocumentRepository` ولا في أي استعلام/ترتيب/إسقاط.
- الأعضاء الخاصة تبقى مشتركة عبر `partial` (لا نسخ ولا وسطاء).

## 2. التحقق

| البوابة | النتيجة |
|---|---|
| `dotnet build -warnaserror` | 0/0 |
| `dotnet format --verify-no-changes` | نظيف |
| `Architecture` | **29/29** |
| `Application` / `Api` | نفس الاختبارات القائمة (لا تغيير سلوكي) |
| أقصى ملف في المستودع | **534** سطرًا (دون 600) |

## 3. الهجرات

**لا هجرة جديدة.** القائمة المعلّقة تبقى **6**.
