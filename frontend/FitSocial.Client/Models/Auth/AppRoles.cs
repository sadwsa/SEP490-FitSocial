namespace FitSocial.Client.Models.Auth;

/// <summary>
/// Định nghĩa các Roles trong hệ thống FitSocial (Đồng bộ chuẩn Backend)
/// </summary>
public static class AppRoles
{
    public const string Trainee = "TRAINEE";
    public const string Coach = "COACH";
    public const string Staff = "STAFF";
    public const string Admin = "ADMIN";

    public static readonly string[] All = { Trainee, Coach, Staff, Admin };

    public static string GetDisplayName(string role) => role switch
    {
        Admin => "Quản trị viên (Admin)",
        Coach => "Huấn luyện viên (Coach)",
        Staff => "Nhân viên (Staff)",
        Trainee => "Hội viên (Trainee)",
        _ => role
    };
}