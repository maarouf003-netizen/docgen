# ملحق متغيّرات البيئة — دفعة `W0-CSP-followup` — 2026‑11‑04

> **الغرض:** استكمال `ENV-VARIABLES-final.md` (لقطة مجنَّدة بتاريخ `2026-10-16`) بالمتغيّرات
> التي أُضيفت أو تغيّر معناها في `W0-CSP`، دون تحرير اللقطة نفسها (سياسة `docs/audit`).
>
> **النطاق:** توثيق فقط. لا تغيير في `API`/`DTO`، ولا هجرات `EF`.

---

## 1) جدول الاستكمال — قسم «4) الأمن»

| المتغير | الوسم | الافتراضي | الوصف المحدَّث |
|---|---|---|---|
| `Security__HstsEnabled` | 🔴 على المخدّم الخاص | `false` | `true` بعد تأكيد `HTTPS` كامل. لا تُرسَل الترويسة خلف وكيل ينهي `TLS` إلا إذا كان الوكيل معلنًا في `Security__KnownProxies` ويمرّر `X-Forwarded-Proto: https`. |
| `Security__HttpsRedirectionEnabled` | 🔴 على المخدّم الخاص | `false` | `true` على المخدّم الخاص؛ ويخضع لنفس شرط الوكيل الموثوق. |
| `Security__HstsMaxAgeDays` | ⚪ | `365` | مدة `HSTS` بالأيام. **جديد:** يُرفض الإقلاع إذا كانت `≤ 0` أو `> 3650` (سقف عشر سنوات يمنع فيض `TimeSpan` وقيمة غير قابلة للمراجعة). |
| `Security__HstsPreload` | ⚪ | `false` | مستقل عن `HstsEnabled`؛ يشترط `HstsEnabled=true` و`HstsMaxAgeDays ≥ 365`. **`preload` إشارة أهلية فقط**: بعد النشر يجب تقديم النطاق في `https://hstspreload.org` — لا إدراج تلقائي. |
| `Security__HstsIncludeSubDomains` | ⚪ | `false` | **جديد:** إضافة `includeSubDomains` صراحةً. يُفرَض `true` تلقائيًا عند `HstsPreload=true` (متطلب القوائم). لا يُفعَّل قبل تأكيد `HTTPS` على **كل** النطاقات الفرعية. |
| `Security__CspReportOnly` | 🟠 | `true` | `false` بعد جولة مراقبة على بيئة `Production` حقيقية. الفرض قرار نشر لا تغيير كود. |
| `Security__CspReportUri` | ⚪ | — | ما زال بلا جامع مركزي. تقييده بمسار `/`-نسبي فقط ضمن دفعة `W1`. |

## 2) تحقق ما بعد النشر — إضافة `HSTS`

```bash
# HSTS (بعد تفعيله، وخلف وكيل يمرّر X-Forwarded-Proto)
curl -sI https://<host>/ | grep -i '^strict-transport-security'
# ⇒ max-age=<HstsMaxAgeDays*86400>[; includeSubDomains][; preload]

# غياب الترويسة مع نجاح HTTPS = الوكيل غير موثوق أو لا يمرّر X-Forwarded-Proto
curl -sI https://<host>/api/meta/current-year | grep -i '^cache-control'   # ⇒ no-store
```

## 3) ما لم يتغيّر

- `AllowedHosts` و`Security__KnownProxies` كما في اللقطة الأصلية؛ الإضافة هنا شرح أثر الوكيل على
  `HSTS` فقط.
- `Security__CspReportOnly=true` يبقى الافتراضي، و`CspReportUri` بلا جامع حتى `W1`.
