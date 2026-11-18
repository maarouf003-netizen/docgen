using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-02f — مصفوفة عزل الاستئناف (قراءة + رفض كتابة) — المورد الرابع في `P2-02`.
/// <para>
/// العقد المقيس: تفاصيل الاستئناف وقائمة استئنافات المستند يراها مالك المستند والرئيس
/// والمدير والمشرف (`200`)، بينما محامٍ آخر — نفس الفرع أو فرعه — يحصل على `403`.
/// </para>
/// <para>
/// P2-02f: تعديل/حذف استئناف ليس للمستخدم يعيد **`403`** (كان `400` لأن
/// `EnsurePendingByCreator` يرمي `ArgumentException`)، واستئناف غير موجود يعيد **`404`**
/// (كان `400`). فحص الملكية صار قبل فحص الحالة، و`EnsureAssignedFollower` (متابعة
/// الاستئناف) تعيد `403` أيضًا.
/// </para>
/// </summary>
public class AppealScopeMatrixTests
{
    [PostgresFact]
    public async Task AppealReadAndWriteMatrix_MatchesPolicy()
    {
        using var factory = new PostgresApiFactory();
        var ownerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var owner = factory.ClientWithToken(ownerToken);

        var documentId = await factory.CreateDocumentAsync(
            ownerToken, borrowerName: UniqueName("appeal_matrix"), registered: true);

        var created = await owner.PostAsJsonAsync($"/api/documents/{documentId}/appeals", new
        {
            direction = "against-us",
            appellants = new[] { new { kind = "borrower", partyId = documentId } },
            appealedDecisionText = "قرار اختبار مصفوفة الاستئناف",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appeal = await created.Content.ReadFromJsonAsync<AppealDto>();
        Assert.NotNull(appeal);

        var damascusBranchId = await factory.GetBranchIdAsync("DAM");
        var aleppoBranchId = await factory.GetBranchIdAsync("ALP");
        var sameBranchLawyer = await factory.CreateUserAsync(
            UniqueName("ap_same"), UserRole.Lawyer, damascusBranchId);
        var otherBranchLawyer = await factory.CreateUserAsync(
            UniqueName("ap_other"), UserRole.Lawyer, aleppoBranchId);

        var failures = new List<string>();

        async Task ExpectReadAsync(
            string actor, HttpClient client, HttpStatusCode expected, bool expectAppealInList)
        {
            var detail = await client.GetAsync($"/api/appeals/{appeal!.Id}");
            if (detail.StatusCode != expected)
            {
                failures.Add($"{actor}: تفاصيل الاستئناف={detail.StatusCode} والمتوقع {expected}");
            }

            var list = await client.GetAsync($"/api/documents/{documentId}/appeals");
            if (list.StatusCode != expected)
            {
                failures.Add($"{actor}: استئنافات المستند={list.StatusCode} والمتوقع {expected}");
                return;
            }

            if (!expectAppealInList)
            {
                return;
            }

            using var items = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
            if (items.RootElement.ValueKind != JsonValueKind.Array
                || items.RootElement.GetArrayLength() == 0)
            {
                failures.Add($"{actor}: قائمة استئنافات المستند فارغة رغم توقع عنصر");
            }
        }

        await ExpectReadAsync("owner", owner, HttpStatusCode.OK, expectAppealInList: true);
        await ExpectReadAsync("head", factory.AuthorizedClient("head1"), HttpStatusCode.OK, expectAppealInList: true);
        await ExpectReadAsync("manager", factory.AuthorizedClient("manager"), HttpStatusCode.OK, expectAppealInList: true);
        await ExpectReadAsync("admin", factory.AuthorizedClient("admin"), HttpStatusCode.OK, expectAppealInList: true);
        await ExpectReadAsync(
            "same-branch lawyer", factory.AuthorizedClient(sameBranchLawyer.Username), HttpStatusCode.Forbidden, expectAppealInList: false);
        await ExpectReadAsync(
            "other-branch lawyer", factory.AuthorizedClient(otherBranchLawyer.Username), HttpStatusCode.Forbidden, expectAppealInList: false);

        // P2-02f: الكتابة على استئناف الغير = 403، والمفقود = 404.
        var updatePayload = new
        {
            direction = "against-us",
            appellants = new[] { new { kind = "borrower", partyId = documentId } },
            appealedDecisionText = "محاولة تعديل غير مصرّح",
        };

        foreach (var (actor, username) in new (string, string)[]
                 {
                     ("same-branch lawyer", sameBranchLawyer.Username),
                     ("other-branch lawyer", otherBranchLawyer.Username),
                 })
        {
            var foreignClient = factory.AuthorizedClient(username);
            var update = await foreignClient.PutAsJsonAsync($"/api/appeals/{appeal!.Id}", updatePayload);
            if (update.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{actor}: تعديل استئناف الغير={update.StatusCode} والمتوقع Forbidden");
            }

            var delete = await foreignClient.DeleteAsync($"/api/appeals/{appeal.Id}");
            if (delete.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{actor}: حذف استئناف الغير={delete.StatusCode} والمتوقع Forbidden");
            }
        }

        var missingUpdate = await owner.PutAsJsonAsync("/api/appeals/999999", updatePayload);
        if (missingUpdate.StatusCode != HttpStatusCode.NotFound)
        {
            failures.Add($"missing update: {missingUpdate.StatusCode} والمتوقع NotFound");
        }

        var missingDelete = await owner.DeleteAsync("/api/appeals/999999");
        if (missingDelete.StatusCode != HttpStatusCode.NotFound)
        {
            failures.Add($"missing delete: {missingDelete.StatusCode} والمتوقع NotFound");
        }

        // الاستئناف ما زال موجودًا للمالك بعد محاولات الغير.
        await ExpectReadAsync("owner after denials", owner, HttpStatusCode.OK, expectAppealInList: true);

        Assert.True(
            failures.Count == 0,
            "انحرافات مصفوفة الاستئناف:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
}
