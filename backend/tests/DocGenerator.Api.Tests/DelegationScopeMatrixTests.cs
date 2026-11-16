using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-02d — مصفوفة عزل الإنابة (قراءة) — المورد الثالث في `P2-02`.
/// <para>
/// العقد المقيس: قائمة إنابات المستند يراها مالك المستند والرئيس/المدير/المشرف، بينما
/// محامٍ آخر (نفس الفرع أو فرع آخر) يحصل على `403`؛ وقائمة `pending` مخصّصة للرئيس
/// وحده في هذا القياس (المدير/المشرف ممنوعان اليوم — انظر الملاحظة أدناه).
/// </para>
/// <para>
/// <b>ملاحظة ثغرة مكتشفة (موثّقة لا مُصلحة هنا):</b> محاولة حذف/تعديل إنابة ليست للمستخدم
/// تُرجع `400` لا `403`، لأن `EnsurePendingByOwner` يرمي `ArgumentException` (يُعيَّن إلى
/// `400`). تغييره إلى `UnauthorizedAccessException` يمسّ عشرات الاختبارات ويستحق تغييرًا
/// مستقلًا موسومًا (مسجّل في خارطة الطريق).
/// </para>
/// </summary>
public class DelegationScopeMatrixTests
{
    [Fact]
    public async Task DelegationReadPaths_ScopeMatrix_MatchesPolicy()
    {
        using var factory = new ApiFactory();
        var ownerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var owner = factory.ClientWithToken(ownerToken);

        var documentId = await factory.CreateDocumentAsync(
            ownerToken, borrowerName: UniqueName("deleg_matrix"), withEstate: true, registered: true);

        int assetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            assetId = db.Assets.Single(a => a.DocumentId == documentId).Id;
        }

        var created = await owner.PostAsJsonAsync($"/api/documents/{documentId}/delegations", new
        {
            delegatedCourt = "محكمة دمشق",
            isExternal = false,
            externalBranchId = (int?)null,
            delegationDate = "1/8/2026",
            delegationText = "إنابة اختبار المصفوفة",
            depositBookNumber = "قيد-مصفوفة",
            depositBookDate = "2/8/2026",
            assetIds = new[] { assetId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(await created.Content.ReadFromJsonAsync<DelegationDto>());

        var damascusBranchId = await factory.GetBranchIdAsync("DAM");
        var aleppoBranchId = await factory.GetBranchIdAsync("ALP");
        var sameBranchLawyer = await factory.CreateUserAsync(
            UniqueName("dg_same"), UserRole.Lawyer, damascusBranchId);
        var otherBranchLawyer = await factory.CreateUserAsync(
            UniqueName("dg_other"), UserRole.Lawyer, aleppoBranchId);

        var failures = new List<string>();

        async Task ExpectDocumentListAsync(
            string actor, HttpClient client, HttpStatusCode expected, bool expectDelegation)
        {
            var response = await client.GetAsync($"/api/documents/{documentId}/delegations");
            if (response.StatusCode != expected)
            {
                failures.Add($"{actor}: قائمة إنابات المستند={response.StatusCode} والمتوقع {expected}");
                return;
            }

            if (!expectDelegation)
            {
                return;
            }

            using var items = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (items.RootElement.ValueKind != JsonValueKind.Array || items.RootElement.GetArrayLength() == 0)
            {
                failures.Add($"{actor}: قائمة الإنابات فارغة رغم توقع عنصر واحد");
            }
        }

        await ExpectDocumentListAsync("owner", owner, HttpStatusCode.OK, expectDelegation: true);
        await ExpectDocumentListAsync("head", factory.AuthorizedClient("head1"), HttpStatusCode.OK, expectDelegation: true);
        await ExpectDocumentListAsync("manager", factory.AuthorizedClient("manager"), HttpStatusCode.OK, expectDelegation: true);
        await ExpectDocumentListAsync("admin", factory.AuthorizedClient("admin"), HttpStatusCode.OK, expectDelegation: true);
        await ExpectDocumentListAsync(
            "same-branch lawyer", factory.AuthorizedClient(sameBranchLawyer.Username), HttpStatusCode.Forbidden, expectDelegation: false);
        await ExpectDocumentListAsync(
            "other-branch lawyer", factory.AuthorizedClient(otherBranchLawyer.Username), HttpStatusCode.Forbidden, expectDelegation: false);

        // pending: عقد قائم — الرئيس وحده في هذا القياس.
        async Task ExpectPendingAsync(string actor, HttpClient client, HttpStatusCode expected)
        {
            var pending = await client.GetAsync("/api/delegations/pending?perPage=20");
            if (pending.StatusCode != expected)
            {
                failures.Add($"{actor}: pending={pending.StatusCode} والمتوقع {expected}");
            }

            var count = await client.GetAsync("/api/delegations/pending-count");
            if (count.StatusCode != expected)
            {
                failures.Add($"{actor}: pending-count={count.StatusCode} والمتوقع {expected}");
            }
        }

        await ExpectPendingAsync("head", factory.AuthorizedClient("head1"), HttpStatusCode.OK);
        await ExpectPendingAsync("owner(lawyer)", owner, HttpStatusCode.Forbidden);
        await ExpectPendingAsync("same-branch lawyer", factory.AuthorizedClient(sameBranchLawyer.Username), HttpStatusCode.Forbidden);
        await ExpectPendingAsync("manager", factory.AuthorizedClient("manager"), HttpStatusCode.Forbidden);
        await ExpectPendingAsync("admin", factory.AuthorizedClient("admin"), HttpStatusCode.Forbidden);

        Assert.True(
            failures.Count == 0,
            "انحرافات مصفوفة الإنابة:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
}
