namespace FitSocial.Client.Models.Auth;

/// <summary>
/// Định nghĩa các Roles trong hệ thống FitSocial
/// Bạn có thể đổi tên role cho khớp chính xác với Backend Database/Identity
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string PersonalTrainer = "PT";
    public const string GymOwner = "GymOwner";
    public const string Member = "Member";

    public static readonly string[] All = { Admin, PersonalTrainer, GymOwner, Member };

    public static string GetDisplayName(string role) => role switch
    {
        Admin => "Quản trị viên (Admin)",
        PersonalTrainer => "Huấn luyện viên (PT)",
        GymOwner => "Chủ phòng Gym",
        Member => "Hội viên / Người dùng",
        _ => role
    };
}
