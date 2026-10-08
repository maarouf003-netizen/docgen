using System.Reflection;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس بنية عقود رئيس الشعبة بقوائم إدراج كاملة لا منع أسماء (مرآة
/// `CorrespondenceContractTests` — قرار §2/حارس 8): أي حقل جديد أو محذوف
/// أو مُعاد تسميته في هوية المستخدم أو الاستئناف أو الإنابة أو إحصاء الدوائر
/// يُفشل الحارس عمدًا — فالعقد سلكي مشترك مع الواجهة (`types/index.ts`)
/// وبيان `contracts/subhead-contracts.json`، لا يُغيَّر بصمت.
/// </summary>
public class SubHeadContractTests
{
    private static IReadOnlyList<string> Names(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();

    private static readonly string[] UserAuthMembers =
    [
        "Id", "Username", "FullName", "Role",
        "BranchId", "BranchName", "SectionId", "SectionName",
    ];

    private static readonly string[] AppealMembers =
    [
        "Id", "DocumentId", "DocumentLabel", "FileNumber", "FileType",
        "FileYear", "Court", "Direction", "DirectionLabel", "Status",
        "StatusLabel", "AppealTypeLabel", "Appellants", "Appellees",
        "AppealedDecisionText", "AppealedDecisionSummary", "AppealedDecisionDate",
        "InspectionBookNumber", "InspectionBookDate", "GroundsSummary",
        "NoticeNumber", "NoticeDate", "AppellateCourt", "AppealBaseNumber",
        "AppealYear", "DepositBookNumber", "DepositBookDate", "DefenseOpinion",
        "RegistrationDate", "DecisionNumber", "DecisionDate", "DecisionRuling",
        "Outcome", "OutcomeLabel", "StruckOffDate", "StruckOffDecisionNumber",
        "Notes", "NeedsRotation", "CurrentBaseNumber", "AssignedLawyerId",
        "AssignedLawyerName", "CreatedAt", "CreatedByName", "CreatedById",
        "DocumentEffectiveNumber", "DocumentEffectiveYear", "PartiesDegraded",
        "ForwardState", "SectionId", "ExecutionCircuitId", "Version",
    ];

    private static readonly string[] DelegationMembers =
    [
        "Id", "SourceDocumentId", "SourceDocumentLabel", "SourceFileNumber",
        "SourceFileYear", "TargetDocumentId", "DelegatedCourt", "DelegatedCircuitId",
        "IsExternal", "ExternalBranchId", "ExternalBranchName", "DelegationDate",
        "DelegationText", "DepositBookNumber", "DepositBookDate", "AssignedLawyerId",
        "AssignedLawyerName", "ReturnDate", "Status", "CreatedAt", "CreatedByName",
        "CreatedById", "Assets", "SaleCoversFullDebt", "TargetFileNumber",
        "TargetFileYear", "SourceFileType", "TargetExecStatus", "SourceCourt",
        "BlocksAssets", "TargetTerminal", "RejectReason", "RedirectedToSectionId",
        "RedirectedToSectionName", "TargetBranchId", "TargetBranchName",
        "TargetLawyerName", "Version",
    ];

    private static readonly string[] CircuitStatsMembers =
    [
        "CircuitId", "CircuitName", "BranchId", "BranchName", "IsActive",
        "FileCount", "LawyerCount", "PendingCount", "SectionId", "SectionName",
        "Version",
    ];

    [Fact]
    public void UserAuthContract_ExposesExactlyTheDeclaredMembers()
        => Assert.Equal(
            UserAuthMembers.OrderBy(n => n),
            Names(typeof(UserDto)).OrderBy(n => n));

    [Fact]
    public void AppealContract_ExposesExactlyTheDeclaredMembers()
        => Assert.Equal(
            AppealMembers.OrderBy(n => n),
            Names(typeof(AppealDto)).OrderBy(n => n));

    [Fact]
    public void DelegationContract_ExposesExactlyTheDeclaredMembers()
        => Assert.Equal(
            DelegationMembers.OrderBy(n => n),
            Names(typeof(DelegationDto)).OrderBy(n => n));

    [Fact]
    public void CircuitStatsContract_ExposesExactlyTheDeclaredMembers()
        => Assert.Equal(
            CircuitStatsMembers.OrderBy(n => n),
            Names(typeof(CircuitStatsDto)).OrderBy(n => n));
}
