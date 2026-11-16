using DocGenerator.Application.Common;
using Microsoft.AspNetCore.Authorization;

namespace DocGenerator.Api.Authorization.Documents;

/// <summary>متطلب وصول إلى مستند واحد بعملية محددة (`P2-01`، `ADR-0007`).</summary>
public sealed record DocumentAccessRequirement(DocumentAction Action) : IAuthorizationRequirement;

/// <summary>
/// مورد التقييم: الإسقاط الخفيف + مهلة الطلب. حمل المهلة هنا ضروري لأن
/// <see cref="IAuthorizationService.AuthorizeAsync"/> لا يقبل <see cref="CancellationToken"/>،
/// وسؤالا الإحالة/المتابعة استعلامان حقيقيان يجب أن يتوقفا مع انقطاع الطلب.
/// </summary>
public sealed record DocumentAccessContext(DocumentAccessInfo Document, CancellationToken CancellationToken);
