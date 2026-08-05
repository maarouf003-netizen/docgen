namespace DocGenerator.Application.DTOs;

public record LoginRequest(string Username, string Password);

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
/// والحساب المقفل ليستجيب المتحكّم برسالة واضحة.
/// </summary>
public enum LoginStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
}

public record LoginResult(
    LoginStatus Status,
    LoginResponse? Response);

public record ChangePasswordRequest(string OldPassword, string NewPassword);

public record RegisterRequest(string Username, string Password, string FullName, string RegistrationCode);
