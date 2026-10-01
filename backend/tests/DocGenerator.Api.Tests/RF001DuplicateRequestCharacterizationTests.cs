using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات توصيف RF-001 للسلوك الحالي لإعادة الإرسال (INT-007): لا مفاتيح عدم تكرار،
/// فإعادة الطلب تُكرر الأثر. خضراء على الكود الحالي عمدًا — توثّق الواقع الذي ستغيّره RF-011.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF001DuplicateRequestCharacterizationTests
{
    private readonly ApiFactory _factory;

    public RF001DuplicateRequestCharacterizationTests(ApiFactory factory) => _factory = factory;

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private async Task<LawyerListItemDto> CreateLawyerAsync(string branchCode, string fullName)
    {
        var admin = _factory.AuthorizedClient("admin");
        var username = $"l_{Guid.NewGuid():N}"[..20];
        var response = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username,
            fullName,
            password = "123456",
            branchId = await BranchIdAsync(branchCode),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LawyerListItemDto>())!;
    }

    private sealed record TransferAllResponse(int TransferredCount);

    [Fact]
    public async Task CreateDocument_SamePayloadTwice_CreatesTwoFiles_CurrentBehavior()
    {
        // السلوك الحالي (INT-007): بلا بصمة طلب — الإرسال المكرر ملفٌ ثانٍ.
        // بعد RF-011 يجب أن تُعكَس: نفس المفتاح ملفٌ واحد.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var payload = new
        {
            documentType = "بيان دعوى",
            borrowerName = $"مكرر {Guid.NewGuid():N}"[..30],
            applicant = "المدعي",
            contractType = "تعهد",
            amountNumeric = 100,
        };

        var first = await client.PostAsJsonAsync("/api/documents", payload);
        var second = await client.PostAsJsonAsync("/api/documents", payload);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task TransferAll_TwiceInARow_SecondTransfersZero_CurrentBehavior()
    {
        // النقل الجماعي آمن نسبيًا اليوم: التنفيذ الثاني يجد المصدر فارغًا فيعيد صفرًا
        // (DocumentService.cs:450-452) — لكن نقرة أثناء الانتظار قد تُدقق مرتين (:459-466).
        var source = await CreateLawyerAsync("DAM", "محامي المصدر المكرر");
        var token = (await _factory.LoginAsync(source.Username, "123456"))!.Token!;
        await _factory.CreateDocumentAsync(token, borrowerName: "نقل مكرر 1");
        await _factory.CreateDocumentAsync(token, borrowerName: "نقل مكرر 2");
        var target = await CreateLawyerAsync("DAM", "محامي الهدف المكرر");

        // رئيس جديد (لا head1 المشترك) حتى لا تتداخل صفوف التدقيق مع اختبارات أخرى في القاعدة المشتركة.
        var freshHead = await _factory.CreateUserAsync(NewName("head_dup"), UserRole.Head, await BranchIdAsync("DAM"));
        var head = _factory.AuthorizedClient(freshHead.Username);
        var body = new { sourceLawyerId = source.Id, targetLawyerId = target.Id };

        var first = await head.PostAsJsonAsync("/api/documents/transfer-all", body);
        first.EnsureSuccessStatusCode();
        Assert.Equal(2, (await first.Content.ReadFromJsonAsync<TransferAllResponse>())!.TransferredCount);

        var second = await head.PostAsJsonAsync("/api/documents/transfer-all", body);
        second.EnsureSuccessStatusCode();
        Assert.Equal(0, (await second.Content.ReadFromJsonAsync<TransferAllResponse>())!.TransferredCount);
    }
}
