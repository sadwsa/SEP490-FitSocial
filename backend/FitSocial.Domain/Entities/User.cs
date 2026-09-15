using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class User
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? PasswordHash { get; set; }

    public string? GoogleProviderId { get; set; }

    public string? FullName { get; set; }

    public string? RoleCode { get; set; }

    public bool? IsInternal { get; set; }

    public string? AvatarUrl { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public bool? IsLocked { get; set; }

    public int TokenVersion { get; set; } = 1;

    public Guid? LockedBy { get; set; }

    public DateTime? LastActiveAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<BodyMetricLog> BodyMetricLogs { get; set; } = new List<BodyMetricLog>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CoachProfile> CoachProfileApprovedByNavigations { get; set; } = new List<CoachProfile>();

    public virtual CoachProfile? CoachProfileCoach { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public virtual ICollection<Friendship> FriendshipAddressees { get; set; } = new List<Friendship>();

    public virtual ICollection<Friendship> FriendshipRequesters { get; set; } = new List<Friendship>();

    public virtual ICollection<User> InverseLockedByNavigation { get; set; } = new List<User>();

    public virtual User? LockedByNavigation { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Otplog> Otplogs { get; set; } = new List<Otplog>();

    public virtual ICollection<Participant> Participants { get; set; } = new List<Participant>();

    public virtual ICollection<PaymentGatewayConfig> PaymentGatewayConfigs { get; set; } = new List<PaymentGatewayConfig>();

    public virtual ICollection<Payout> Payouts { get; set; } = new List<Payout>();

    public virtual ICollection<PostInteraction> PostInteractions { get; set; } = new List<PostInteraction>();

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<Report> ReportReportedUsers { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportReporters { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportResolvedByNavigations { get; set; } = new List<Report>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<UserBlock> UserBlockBlockeds { get; set; } = new List<UserBlock>();

    public virtual ICollection<UserBlock> UserBlockBlockers { get; set; } = new List<UserBlock>();

    public virtual ICollection<WorkoutCompletionLog> WorkoutCompletionLogs { get; set; } = new List<WorkoutCompletionLog>();

    public virtual ICollection<Sport> Sports { get; set; } = new List<Sport>();
}
