using System.Net;
using DocGenerator.Domain.Enums;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-02 — مصفوفة صلاحيات موثّقة لمسارات المستندات (قراءة + حذف).
/// <para>
/// العقد المرجعي: مورد **موجود** خارج نطاق المستخدم يعيد <c>403</c> لا <c>404</c>
/// (قرار `DocumentAccessProjectionTests`/RF-003: الفصل بين «غير موجود» و«غير مسموح»
/// مقصود، والواجهة تعرض رسالة صلاحية عربية). القوائم تُرشّح بالنطاق فتُعيد 200 بقائمة
/// فارغة بدل 403.
/// </para>
/// <para>
/// تُنفَّذ كل الخلايا في اختبار واحد لأن التهيئة (مستخدمون + مستند) مكلفة، ولأن الهدف
/// مقارنة كاملة لا عزل خلية؛ الفشل يجمع كل الانحرافات في رسالة واحدة. أي تغيير في
/// العقد يجب أن يُقره اختبار صريح لا انحراف صامت.
/// </para>
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AuthorizationMatrixTests
{
    private readonly ApiFactory _factory;

    public AuthorizationMatrixTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Documents_ReadAndDeleteMatrix_MatchesPolicy()
    {
        var ownerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var documentId = await _factory.CreateDocumentAsync(
            ownerToken, borrowerName: UniqueName("matrix_owner"));

        var damascusBranchId = await _factory.GetBranchIdAsync("DAM");
        var aleppoBranchId = await _factory.GetBranchIdAsync("ALP");
        var sameBranchLawyer = await _factory.CreateUserAsync(
            UniqueName("mx_same"), UserRole.Lawyer, damascusBranchId);
        var otherBranchLawyer = await _factory.CreateUserAsync(
            UniqueName("mx_other"), UserRole.Lawyer, aleppoBranchId);

        var failures = new List<string>();

        async Task ExpectReadAsync(string actor, string username, HttpStatusCode expectedDetail, bool expectedInList)
        {
            var client = _factory.AuthorizedClient(username);

            var detail = await client.GetAsync($"/api/documents/{documentId}");
            if (detail.StatusCode != expectedDetail)
            {
                failures.Add($"{actor}: تفاصيل={detail.StatusCode} والمتوقع {expectedDetail}");
            }

            var list = await client.GetAsync("/api/documents?perPage=50");
            if (list.StatusCode != HttpStatusCode.OK)
            {
                failures.Add($"{actor}: القائمة={list.StatusCode} والمتوقع OK");
                return;
            }

            var containsDocument = (await list.Content.ReadAsStringAsync())
                .Contains($"\"id\":{documentId},", StringComparison.Ordinal);
            if (containsDocument != expectedInList)
            {
                failures.Add($"{actor}: القائمة تحتوي المستند={containsDocument} والمتوقع {expectedInList}");
            }
        }

        // المالك يرى مستنده، ورؤساء/مدير/مشرف يرون كل شيء ضمن نطاقهم.
        await ExpectReadAsync("owner", "lawyer1", HttpStatusCode.OK, expectedInList: true);
        await ExpectReadAsync("head", "head1", HttpStatusCode.OK, expectedInList: true);
        await ExpectReadAsync("manager", "manager", HttpStatusCode.OK, expectedInList: true);
        await ExpectReadAsync("admin", "admin", HttpStatusCode.OK, expectedInList: true);

        // محامٍ آخر (نفس الفرع أو فرع آخر) لا يرى تفاصيل مستند غيره، لكن القائمة تعمل
        // وتُرشّح الصف بصمت (200 بقائمة لا تحتويه) — لا 403 على القائمة.
        await ExpectReadAsync("same-branch lawyer", sameBranchLawyer.Username, HttpStatusCode.Forbidden, expectedInList: false);
        await ExpectReadAsync("other-branch lawyer", otherBranchLawyer.Username, HttpStatusCode.Forbidden, expectedInList: false);

        // الحذف فعل مالك/إداري فقط: المحامي الغريب يُرفض ولا يُحذف شيء.
        foreach (var (actor, username) in new (string, string)[]
                 {
                     ("same-branch lawyer", sameBranchLawyer.Username),
                     ("other-branch lawyer", otherBranchLawyer.Username),
                 })
        {
            var delete = await _factory.AuthorizedClient(username).DeleteAsync($"/api/documents/{documentId}");
            if (delete.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{actor}: حذف={delete.StatusCode} والمتوقع Forbidden");
            }
        }

        // المستند ما زال موجودًا للمالك بعد محاولات الحذف المرفوضة.
        var ownerDetail = await _factory.ClientWithToken(ownerToken).GetAsync($"/api/documents/{documentId}");
        if (ownerDetail.StatusCode != HttpStatusCode.OK)
        {
            failures.Add($"owner بعد المحاولات: تفاصيل={ownerDetail.StatusCode} والمتوقع OK");
        }

        // مورد غير موجود = 404 لا 403 (عقد الفصل بين الحالتين).
        var missing = await _factory.ClientWithToken(ownerToken).GetAsync("/api/documents/999999");
        if (missing.StatusCode != HttpStatusCode.NotFound)
        {
            failures.Add($"missing document: تفاصيل={missing.StatusCode} والمتوقع NotFound");
        }

        Assert.True(
            failures.Count == 0,
            "انحرافات مصفوفة الصلاحيات:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
}
