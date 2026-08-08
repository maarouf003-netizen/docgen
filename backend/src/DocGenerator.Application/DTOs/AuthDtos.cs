namespace DocGenerator.Application.DTOs;

public record LoginRequest(string Username, string Password, int? BranchId = null);

public record UserDto(
    int Id,
    string Username,
    string FullName,
    string Role,
    int? BranchId,
    string? BranchName);

public record LoginResponse(
    string Token,
    UserDto User);

/// <summary>
/// نتيجة محاولة الدخول؛ تُفرّق بين النجاح وبيانات الدخول الخاطئة
/// والحساب المقفل، والحاجة إلى اختيار الفرع عند تكرار الاسم الثلاثي
/// عبر فروع مختلفة — ليستجيب المتحكّم برسالة واضحة.
/// </summary>
public enum LoginStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
    BranchSelectionRequired,
}

/// <summary>
/// خيار فرع معروض للمستخدم عند تكرار الاسم الثلاثي بين فروع مختلفة.
/// branchId يُرسل مرة أخرى مع الطلب (0 يعني الحساب الذي لا يتبع فرعاً).
/// </summary>
public record LoginBranchChoiceDto(int? BranchId, string? BranchName);

public record LoginResult(
    LoginStatus Status,
    LoginResponse? Response,
    List<LoginBranchChoiceDto>? Branches = null);

public record ChangePasswordRequest(string OldPassword, string NewPassword);

public record RegisterRequest(string Username, string Password, string FullName, string RegistrationCode);
