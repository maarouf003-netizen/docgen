# مولد المستندات التنفيذية — نسخة الويب (React + .NET)

دليل تشغيل مفصّل خطوة بخطوة: [`RUN_GUIDE.md`](./RUN_GUIDE.md)

إعادة بناء كاملة لتطبيق مولد المستندات التنفيذية (النسخة المكتبية PyQt5) كتطبيق ويب:

- **الواجهة:** Vite + React + TypeScript (واجهة عربية RTL، Cairo font، Tailwind CSS)
- **الخلفية:** ASP.NET Core Web API + EF Core 8 (SQLite محلياً، PostgreSQL للإنتاج)
- **المصادقة:** JWT Bearer (PBKDF2-SHA256، 200k تكرار) — نفس حسابات تطبيق Flask المرجعي
- **التوثيق:** Swagger على `/swagger`
- **توليد Word:** عبر `DocumentFormat.OpenXml` على الخادم بتعبئة القوالب الأصلية `bin/*.docx`

## المتطلبات

- .NET SDK 8
- Node.js 20+ / npm

## التشغيل

### بكبسة زر (الأسهل)

انقر نقراً مزدوجاً على `start-app.bat`:

- يفتح نافذة الخلفية (API على `http://localhost:5199`) ونافذة الواجهة (Vite على `http://localhost:5173`) في نافذتين جديدتين.
- ينتظر 15 ثانية ثم يفتح المتصفح على `http://localhost:5173`.
- عند الانتهاء انقر `stop-app.bat` لإيقاف الخدمتين (أو أغلق النافذتين).

> يتطلب وجود `.NET SDK 8` و`Node.js 20+` مثبّتين في المسارات الافتراضية.

### الخلفية (API)

```powershell
cd backend
dotnet run --project src/DocGenerator.Api --urls http://localhost:5199
```

عند أول تشغيل: تُنشأ قاعدة SQLite `docgen.db` تلقائياً (migrations + seeding).

حسابات دخول مبدئية (كلمة السر للجميع: `123456`):

| المستخدم  | الدور       | الفرع          |
|-----------|-------------|----------------|
| `admin`   | مشرف نظام   | —              |
| `manager` | مدير        | —              |
| `head1`   | رئيس قسم    | الفرع الرئيسي - دمشق |
| `lawyer1` | محامي       | الفرع الرئيسي - دمشق |

### الواجهة (Vite)

```powershell
cd frontend
npm install
npm run dev
```

يفتح المتصفح على `http://localhost:5173`، ويُمرر طلبات `/api` إلى الخلفية على المنفذ `5199`
(يمكن تغييره عبر متغير البيئة `VITE_API_TARGET`).

## توليد مستندات Word

تُولَّد مستندات Word خادمياً عبر حزمة `DocumentFormat.OpenXml` الرسمية (لا يتطلب تثبيت
Microsoft Word)، بتعبئة القوالب الأصلية من مجلد `WordTemplates` (نُسخ مطابقة لقوالب
`bin/*.docx` في تطبيق سطح المكتب).

- القوالب المدعومة (في `appsettings.json` تحت `WordTemplates`):
  `001` استدعاء تنفيذي (`summon.docx`)، `002` محضر تنفيذي (`record.docx`)،
  `003` إخطار تنفيذي (`notice.docx`)، `004` حجز عقاري (`Seizure.docx`)،
  `005` إعلان عقار (`property.docx`).
- تعبئة الـ placeholders بصيغ `{{key}}` / `{{ key }}` / `{{r key}}` بما يحاكي سلوك `docxtpl`
  (تجميع نصوص الـ runs المنقسمة داخل الفقرة، وإدراج RichText بالاسم العريض).
- الواجهة: زر «تنزيل» لكل نوع في صفحة تفاصيل المستند
  (`GET /api/documents/{id}/generate?template=001..005`).

## الاختبارات

الخلفية:

```powershell
cd backend
dotnet test DocGenerator.sln
```

يشمل: اختبارات الوحدات (54) واختبارات التكامل (29) عبر `WebApplicationFactory` بقاعدة
مؤقتة معزولة — بما فيها توليد Word فعلي من القوالب الحقيقية والتحقق من `PK` الزميل و
`[Content_Types].xml` واستبدال الـ placeholders.

الواجهة:

```powershell
cd frontend
npm test
```

اختبارات الواجهة (Vitest + Testing Library، 17 اختباراً) تغطي: نموذج إدخال الملف
(تبديل «عادي/مصرفي»، الحقول، ترقيم الكفلاء)، تفاصيل المستند (البيانات، الحالات الأربع،
تنزيل Word)، وحالة المستند الموحّدة.

## الإنتاج

في `backend/src/DocGenerator.Api/appsettings.json`:
- فعّل `Database:UsePostgres = true` واضبط `ConnectionStrings:DefaultConnection` على PostgreSQL.
- املأ `Jwt:Secret` بمفتاح قوي (إلزامي خارج بيئة التطوير).
- `Swagger:Enabled` — في غير بيئة التطوير يُغلق Swagger افتراضياً؛ فعّله صراحةً عند الحاجة.
- `RateLimiting:MaxLoginAttempts` / `RateLimiting:WindowMinutes` — إعدادات تحديد محاولات الدخول
  (مخزّنة في جدول `LoginAttempts` لتكون مشتركة بين عقد النشر).

## هيكل الحل

```
react-dotnet-app/
├─ backend/
│  ├─ DocGenerator.sln
│  └─ src/
│     ├─ DocGenerator.Domain        # الكيانات والـ enums
│     ├─ DocGenerator.Application   # الخدمات وDTOs والواجهات
│     ├─ DocGenerator.Infrastructure# EF Core، المستودعات، التشفير، JWT، seeding
│     └─ DocGenerator.Api           # Program.cs + Controllers + Swagger + WordTemplates/
│  └─ tests/
│     ├─ DocGenerator.Application.Tests   # اختبارات الوحدات
│     └─ DocGenerator.Api.Tests           # اختبارات التكامل (WebApplicationFactory)
└─ frontend/
   └─ src/
      ├─ api/        # عميل Axios مع JWT
      ├─ auth/       # AuthContext
      ├─ components/ # Layout
      ├─ pages/      # Login, Dashboard, DocumentsList, DocumentForm, DocumentView,
      │              # UsersActivity, AuditLogs, ChangePassword
      └─ types/      # أنواع TypeScript
```
