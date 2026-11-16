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
/// P2-02d/e — مصفوفة عزل الإنابة (قراءة + رفض كتابة) — المورد الثالث في `P2-02`.
/// <para>
/// العقد المقيس: قائمة إنابات المستند يراها مالك المستند والرئيس/المدير/المشرف، بينما
/// محامٍ آخر (نفس الفرع أو فرع آخر) يحصل على `403`؛ وقائمة `pending` مخصّصة للرئيس
/// وحده في هذا القياس (المدير/المشرف ممنوعان اليوم — قرار قائم يُراجع مع `P2-01`).
/// </para>
/// <para>
/// P2-02e: تعديل/حذف إنابة ليست للمستخدم يعيد **`403`** (كان `400` لأن
/// `EnsurePendingByOwner` يرمي `ArgumentException`)، وإنابة غير موجودة تعيد **`404`**
/// (كانت `400`). فحص الملكية صار قبل فحص الحالة فلا يكشف حالة إنابة الغير.
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
        var delegation = await created.Content.ReadFromJsonAsync<DelegationDto>();
        Assert.NotNull(delegation);

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

        // 5) P2-02e: الكتابة على إنابة الغير = 403 (لا 400)، والمفقود = 404.
        var updatePayload = new
        {
            delegatedCourt = "محكمة دمشق",
            isExternal = false,
            externalBranchId = (int?)null,
            delegationDate = "1/8/2026",
            delegationText = "محاولة تعديل غير مصرّح",
            depositBookNumber = "قيد-مصفوفة",
            depositBookDate = "2/8/2026",
            assetIds = new[] { assetId },
        };

        foreach (var (actor, username) in new (string, string)[]
                 {
                     ("same-branch lawyer", sameBranchLawyer.Username),
                     ("other-branch lawyer", otherBranchLawyer.Username),
                 })
        {
            var foreignClient = factory.AuthorizedClient(username);
            var update = await foreignClient.PutAsJsonAsync($"/api/delegations/{delegation!.Id}", updatePayload);
            if (update.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{actor}: تعديل إنابة الغير={update.StatusCode} والمتوقع Forbidden");
            }

            var delete = await foreignClient.DeleteAsync($"/api/delegations/{delegation.Id}");
            if (delete.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{actor}: حذف إنابة الغير={delete.StatusCode} والمتوقع Forbidden");
            }
        }

        var missingUpdate = await owner.PutAsJsonAsync("/api/delegations/999999", updatePayload);
        if (missingUpdate.StatusCode != HttpStatusCode.NotFound)
        {
            failures.Add($"missing update: {missingUpdate.StatusCode} والمتوقع NotFound");
        }

        var missingDelete = await owner.DeleteAsync("/api/delegations/999999");
        if (missingDelete.StatusCode != HttpStatusCode.NotFound)
        {
            failures.Add($"missing delete: {missingDelete.StatusCode} والمتوقع NotFound");
        }

        // الإنابة ما زالت موجودة للمالك بعد محاولات الغير.
        await ExpectDocumentListAsync("owner after denials", owner, HttpStatusCode.OK, expectDelegation: true);

        Assert.True(
            failures.Count == 0,
            "انحرافات مصفوفة الإنابة:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
}
