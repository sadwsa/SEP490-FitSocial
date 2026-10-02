using System;
using System.Collections.Generic;
using FitSocial.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Data;

public partial class FitSocialDbContext : DbContext
{
    public FitSocialDbContext(DbContextOptions<FitSocialDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<BodyMeasurementDetail> BodyMeasurementDetails { get; set; }
    public virtual DbSet<BodyMetricLog> BodyMetricLogs { get; set; }
    public virtual DbSet<Cart> Carts { get; set; }
    public virtual DbSet<CoachBankAccount> CoachBankAccounts { get; set; }
    public virtual DbSet<CoachCertificate> CoachCertificates { get; set; }
    public virtual DbSet<CoachEkycVerification> CoachEkycVerifications { get; set; }
    public virtual DbSet<CoachProfile> CoachProfiles { get; set; }
    public virtual DbSet<CoachSubscriptionPlan> CoachSubscriptionPlans { get; set; }
    public virtual DbSet<CoachUpgrade> CoachUpgrades { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<Conversation> Conversations { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<MealPlan> MealPlans { get; set; }
    public virtual DbSet<MealPlanItem> MealPlanItems { get; set; }
    public virtual DbSet<Message> Messages { get; set; }
    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderDetail> OrderDetails { get; set; }
    public virtual DbSet<Otplog> Otplogs { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<PaymentGatewayConfig> PaymentGatewayConfigs { get; set; }
    public virtual DbSet<Payout> Payouts { get; set; }
    public virtual DbSet<PayoutItem> PayoutItems { get; set; }
    public virtual DbSet<Post> Posts { get; set; }
    public virtual DbSet<PostInteraction> PostInteractions { get; set; }
    public virtual DbSet<PostMedium> PostMedia { get; set; }
    public virtual DbSet<Price> Prices { get; set; }
    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
    public virtual DbSet<RefundRequest> RefundRequests { get; set; }
    public virtual DbSet<Report> Reports { get; set; }
    public virtual DbSet<ReportMedium> ReportMedia { get; set; }
    public virtual DbSet<Review> Reviews { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<TermsAndPolicy> TermsAndPolicies { get; set; }
    public virtual DbSet<TrainingPackage> TrainingPackages { get; set; }
    public virtual DbSet<TrainingPlan> TrainingPlans { get; set; }
    public virtual DbSet<TrainingPlanExercise> TrainingPlanExercises { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserAgreement> UserAgreements { get; set; }
    public virtual DbSet<UserBlock> UserBlocks { get; set; }
    public virtual DbSet<VideoTutorial> VideoTutorials { get; set; }
    public virtual DbSet<WorkoutCompletionLog> WorkoutCompletionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("uuid-ossp");

        // 1. Roles
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleCode).HasName("Roles_pkey");
            entity.ToTable("Roles");
            entity.Property(e => e.RoleCode).HasMaxLength(8).HasColumnName("RoleCode");
            entity.Property(e => e.RoleName).HasMaxLength(50).HasColumnName("RoleName");
        });

        // 2. Locations
        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("Locations_pkey");
            entity.ToTable("Locations");
            entity.Property(e => e.LocationId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("LocationID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.LocationName).HasMaxLength(200);
        });

        // 3. TermsAndPolicies
        modelBuilder.Entity<TermsAndPolicy>(entity =>
        {
            entity.HasKey(e => e.TermId).HasName("TermsAndPolicies_pkey");
            entity.ToTable("TermsAndPolicies");
            entity.Property(e => e.TermId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("TermID");
            entity.Property(e => e.Content).HasColumnType("text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.EffectiveDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.Version).HasMaxLength(50);
        });

        // 4. PaymentGatewayConfigs
        modelBuilder.Entity<PaymentGatewayConfig>(entity =>
        {
            entity.HasKey(e => e.GatewayId).HasName("PaymentGatewayConfigs_pkey");
            entity.ToTable("PaymentGatewayConfigs");
            entity.Property(e => e.GatewayId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("GatewayID");
            entity.Property(e => e.ClientId).HasMaxLength(255).HasColumnName("ClientID");
            entity.Property(e => e.EncryptedApiKey).HasColumnType("bytea").HasColumnName("EncryptedApiKey");
            entity.Property(e => e.EncryptedChecksumKey).HasColumnType("bytea").HasColumnName("EncryptedChecksumKey");
            entity.Property(e => e.GatewayName).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
            entity.Property(e => e.WebhookUrl).HasMaxLength(2048);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentGatewayConfigs)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("fk_gateway_updatedby");
        });

        // 5. Price
        modelBuilder.Entity<Price>(entity =>
        {
            entity.HasKey(e => e.PriceId).HasName("Price_pkey");
            entity.ToTable("Price");
            entity.Property(e => e.PriceId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("PriceID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
        });

        // 6. Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("Users_pkey");
            entity.ToTable("Users");
            entity.HasIndex(e => e.Email, "Users_Email_key").IsUnique();
            entity.HasIndex(e => e.GoogleProviderId, "Users_GoogleProviderID_key").IsUnique();
            entity.HasIndex(e => e.PhoneNumber, "Users_PhoneNumber_key").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("UserID");
            entity.Property(e => e.AvatarUrl).HasMaxLength(2048);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(6);
            entity.Property(e => e.GoogleProviderId)
                .HasMaxLength(255)
                .HasColumnName("GoogleProviderID");
            entity.Property(e => e.IsInternal).HasDefaultValue(false);
            entity.Property(e => e.IsLocked).HasDefaultValue(false);
            entity.Property(e => e.LastActiveAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.LockedBy).HasColumnName("LockedBy");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.RoleCode)
                .HasMaxLength(8)
                .HasColumnName("RoleCode");
            entity.Property(e => e.TokenVersion).HasDefaultValue(1).HasColumnName("TokenVersion");
            entity.Property(e => e.WarningCount).HasDefaultValue(0).HasColumnName("WarningCount");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleCode)
                .HasConstraintName("Users_RoleCode_fkey");

            entity.HasOne(d => d.LockedByNavigation).WithMany(p => p.InverseLockedByNavigation)
                .HasForeignKey(d => d.LockedBy)
                .HasConstraintName("Users_LockedBy_fkey");
        });

        // 7. CoachProfiles
        modelBuilder.Entity<CoachProfile>(entity =>
        {
            entity.HasKey(e => e.CoachId).HasName("CoachProfiles_pkey");
            entity.ToTable("CoachProfiles");
            entity.Property(e => e.CoachId)
                .ValueGeneratedNever()
                .HasColumnName("CoachID");
            entity.Property(e => e.ApprovalStatus).HasMaxLength(50);
            entity.Property(e => e.CertificateUrl).HasMaxLength(2048);
            entity.Property(e => e.IdentityCardUrl).HasMaxLength(2048);
            entity.Property(e => e.Bio).HasColumnType("text");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.CoachProfileApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("CoachProfiles_ApprovedBy_fkey");

            entity.HasOne(d => d.Coach).WithOne(p => p.CoachProfileCoach)
                .HasForeignKey<CoachProfile>(d => d.CoachId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("CoachProfiles_CoachID_fkey");

            entity.HasMany(d => d.Locations).WithMany(p => p.Coaches)
                .UsingEntity<Dictionary<string, object>>(
                    "CoachLocation",
                    r => r.HasOne<Location>().WithMany().HasForeignKey("LocationId").HasConstraintName("CoachLocations_LocationID_fkey"),
                    l => l.HasOne<CoachProfile>().WithMany().HasForeignKey("CoachId").HasConstraintName("CoachLocations_CoachID_fkey"),
                    j =>
                    {
                        j.HasKey("CoachId", "LocationId").HasName("CoachLocations_pkey");
                        j.ToTable("CoachLocations");
                        j.IndexerProperty<Guid>("CoachId").HasColumnName("CoachID");
                        j.IndexerProperty<Guid>("LocationId").HasColumnName("LocationID");
                    });
        });

        // 8. CoachBankAccounts
        modelBuilder.Entity<CoachBankAccount>(entity =>
        {
            entity.HasKey(e => e.BankId).HasName("CoachBankAccounts_pkey");
            entity.ToTable("CoachBankAccounts");
            entity.Property(e => e.BankId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("BankID");
            entity.Property(e => e.AccountName).HasMaxLength(255);
            entity.Property(e => e.EncryptedAccountNumber).HasColumnType("bytea").HasColumnName("EncryptedAccountNumber");
            entity.Property(e => e.BankCode).HasMaxLength(50);
            entity.Property(e => e.BankName).HasMaxLength(255);
            entity.Property(e => e.Branch).HasMaxLength(255);
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.CoachBankAccounts)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("CoachBankAccounts_CoachID_fkey");
        });

        // 9. CoachEkycVerifications
        modelBuilder.Entity<CoachEkycVerification>(entity =>
        {
            entity.HasKey(e => e.EkycId).HasName("CoachEkycVerifications_pkey");
            entity.ToTable("CoachEkycVerifications");
            entity.Property(e => e.EkycId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("EkycID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.EncryptedIdCardNumber).HasColumnType("bytea").HasColumnName("EncryptedIdCardNumber");
            entity.Property(e => e.FullNameOnCard).HasMaxLength(100);
            entity.Property(e => e.DateOfBirthOnCard);
            entity.Property(e => e.Sex).HasMaxLength(10);
            entity.Property(e => e.Nationality).HasMaxLength(50);
            entity.Property(e => e.Ethnicity).HasMaxLength(50);
            entity.Property(e => e.Religion).HasMaxLength(50);
            entity.Property(e => e.Birthplace).HasMaxLength(255);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Province).HasMaxLength(100);
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.Ward).HasMaxLength(100);
            entity.Property(e => e.ProvinceCode).HasMaxLength(10);
            entity.Property(e => e.DistrictCode).HasMaxLength(10);
            entity.Property(e => e.WardCode).HasMaxLength(10);
            entity.Property(e => e.Street).HasMaxLength(255);
            entity.Property(e => e.DocumentType).HasMaxLength(50);
            entity.Property(e => e.IssueDate).HasMaxLength(20);
            entity.Property(e => e.Expiry).HasMaxLength(20);
            entity.Property(e => e.IssueBy).HasColumnType("text");
            entity.Property(e => e.Feature).HasColumnType("text");
            entity.Property(e => e.FrontCardUrl).HasMaxLength(2048);
            entity.Property(e => e.BackCardUrl).HasMaxLength(2048);
            entity.Property(e => e.FaceImageUrl).HasMaxLength(2048);
            entity.Property(e => e.LivenessScore).HasPrecision(5, 2);
            entity.Property(e => e.FaceMatchConfidence).HasPrecision(5, 2);
            entity.Property(e => e.VerificationStatus).HasMaxLength(50);
            entity.Property(e => e.EncryptedRawInformationJson).HasColumnType("bytea").HasColumnName("EncryptedRawInformationJson");
            entity.Property(e => e.FailureReason).HasColumnType("text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.CoachEkycVerifications)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("CoachEkycVerifications_CoachID_fkey");
        });

        // 10. CoachCertificates
        modelBuilder.Entity<CoachCertificate>(entity =>
        {
            entity.HasKey(e => e.CertificateId).HasName("CoachCertificates_pkey");
            entity.ToTable("CoachCertificates");
            entity.Property(e => e.CertificateId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("CertificateID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CertificateName).HasMaxLength(255);
            entity.Property(e => e.CertificateUrl).HasMaxLength(2048);
            entity.Property(e => e.IssuedBy).HasMaxLength(255);
            entity.Property(e => e.IssuedDate);
            entity.Property(e => e.ExpiryDate);
            entity.Property(e => e.VerificationStatus).HasMaxLength(50);
            entity.Property(e => e.VerifiedBy).HasColumnName("VerifiedBy");
            entity.Property(e => e.VerifiedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.RejectedReason).HasColumnType("text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.CoachCertificates)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("CoachCertificates_CoachID_fkey");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.CoachCertificatesVerified)
                .HasForeignKey(d => d.VerifiedBy)
                .HasConstraintName("CoachCertificates_VerifiedBy_fkey");
        });

        // 11. RefreshTokens
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("RefreshTokens_pkey");
            entity.ToTable("RefreshTokens");
            entity.HasIndex(e => e.TokenHash, "RefreshTokens_TokenHash_key").IsUnique();

            entity.Property(e => e.TokenId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("TokenID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.DeviceInfo).HasMaxLength(255);
            entity.Property(e => e.ExpiresAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.RevokedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.TokenHash).HasMaxLength(255);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("RefreshTokens_UserID_fkey");
        });

        // 12. OTPLogs
        modelBuilder.Entity<Otplog>(entity =>
        {
            entity.HasKey(e => e.OtpId).HasName("OTPLogs_pkey");
            entity.ToTable("OTPLogs");
            entity.Property(e => e.OtpId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("OtpID");
            entity.Property(e => e.AttemptCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.Property(e => e.ExpiresAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.OtpHash).HasMaxLength(255);
            entity.Property(e => e.Purpose).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.VerifiedAt).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.User).WithMany(p => p.Otplogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("OTPLogs_UserID_fkey");
        });

        // 13. UserAgreements
        modelBuilder.Entity<UserAgreement>(entity =>
        {
            entity.HasKey(e => e.AgreementId).HasName("UserAgreements_pkey");
            entity.ToTable("UserAgreements");
            entity.Property(e => e.AgreementId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("AgreementID");
            entity.Property(e => e.AcceptedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.TermId).HasColumnName("TermID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Term).WithMany(p => p.UserAgreements)
                .HasForeignKey(d => d.TermId)
                .HasConstraintName("UserAgreements_TermID_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserAgreements)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("UserAgreements_UserID_fkey");
        });

        // 14. UserBlocks
        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("UserBlocks_pkey");
            entity.ToTable("UserBlocks");
            entity.HasIndex(e => new { e.BlockerId, e.BlockedId }, "UserBlocks_BlockerID_BlockedID_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ID");
            entity.Property(e => e.BlockedId).HasColumnName("BlockedID");
            entity.Property(e => e.BlockerId).HasColumnName("BlockerID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Blocked).WithMany(p => p.UserBlockBlockeds)
                .HasForeignKey(d => d.BlockedId)
                .HasConstraintName("UserBlocks_BlockedID_fkey");

            entity.HasOne(d => d.Blocker).WithMany(p => p.UserBlockBlockers)
                .HasForeignKey(d => d.BlockerId)
                .HasConstraintName("UserBlocks_BlockerID_fkey");
        });

        // 15. Posts
        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Posts_pkey");
            entity.ToTable("Posts");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.Content).HasColumnType("text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Author).WithMany(p => p.Posts)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("Posts_AuthorID_fkey");

            entity.HasOne(d => d.Location).WithMany(p => p.Posts)
                .HasForeignKey(d => d.LocationId)
                .HasConstraintName("Posts_LocationID_fkey");
        });

        // 16. PostMedia
        modelBuilder.Entity<PostMedium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PostMedia_pkey");
            entity.ToTable("PostMedia");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.MediaType).HasMaxLength(20);
            entity.Property(e => e.MediaUrl)
                .HasMaxLength(2048)
                .HasColumnName("MediaURL");
            entity.Property(e => e.PostId).HasColumnName("PostID");

            entity.HasOne(d => d.Post).WithMany(p => p.PostMedia)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("PostMedia_PostID_fkey");
        });

        // 17. Comments
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Comments_pkey");
            entity.ToTable("Comments");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.Content).HasMaxLength(500);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.ParentCommentId).HasColumnName("ParentCommentID");
            entity.Property(e => e.PostId).HasColumnName("PostID");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Author).WithMany(p => p.Comments)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("Comments_AuthorID_fkey");

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment)
                .HasForeignKey(d => d.ParentCommentId)
                .HasConstraintName("Comments_ParentCommentID_fkey");

            entity.HasOne(d => d.Post).WithMany(p => p.Comments)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("Comments_PostID_fkey");
        });

        // 18. PostInteractions
        modelBuilder.Entity<PostInteraction>(entity =>
        {
            entity.HasKey(e => new { e.PostId, e.UserId }).HasName("PostInteractions_pkey");
            entity.ToTable("PostInteractions");
            entity.Property(e => e.PostId).HasColumnName("PostID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Post).WithMany(p => p.PostInteractions)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("PostInteractions_PostID_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.PostInteractions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("PostInteractions_UserID_fkey");
        });

        // 19. Conversations
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("Conversations_pkey");
            entity.ToTable("Conversations");
            entity.Property(e => e.ConversationId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ConversationID");
            entity.Property(e => e.User1Id).HasColumnName("User1ID");
            entity.Property(e => e.User2Id).HasColumnName("User2ID");
            entity.Property(e => e.LastMessageContent).HasColumnType("text");
            entity.Property(e => e.LastMessageSenderId).HasColumnName("LastMessageSenderID");
            entity.Property(e => e.LastMessageAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.User1DeletedHistoryAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.User2DeletedHistoryAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.User1LastReadMessageId).HasColumnName("User1LastReadMessageID");
            entity.Property(e => e.User2LastReadMessageId).HasColumnName("User2LastReadMessageID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.Ignore(e => e.Id);

            entity.HasOne(d => d.User1).WithMany(p => p.ConversationsAsUser1)
                .HasForeignKey(d => d.User1Id)
                .HasConstraintName("Conversations_User1ID_fkey");

            entity.HasOne(d => d.User2).WithMany(p => p.ConversationsAsUser2)
                .HasForeignKey(d => d.User2Id)
                .HasConstraintName("Conversations_User2ID_fkey");

            entity.HasOne(d => d.LastMessageSender).WithMany(p => p.ConversationsLastMessageSender)
                .HasForeignKey(d => d.LastMessageSenderId)
                .HasConstraintName("Conversations_LastMessageSenderID_fkey");

            entity.HasOne(d => d.User1LastReadMessage).WithMany()
                .HasForeignKey(d => d.User1LastReadMessageId)
                .HasConstraintName("fk_user1_lastmsg");

            entity.HasOne(d => d.User2LastReadMessage).WithMany()
                .HasForeignKey(d => d.User2LastReadMessageId)
                .HasConstraintName("fk_user2_lastmsg");
        });

        // 20. Messages
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("Messages_pkey");
            entity.ToTable("Messages");
            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("MessageID");
            entity.Property(e => e.ConversationId).HasColumnName("ConversationID");
            entity.Property(e => e.SenderId).HasColumnName("SenderID");
            entity.Property(e => e.Content).HasColumnType("text");
            entity.Property(e => e.MessageType).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.Ignore(e => e.Id);

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("Messages_ConversationID_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("Messages_SenderID_fkey");
        });

        // 21. MessageAttachments
        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.AttachmentId).HasName("MessageAttachments_pkey");
            entity.ToTable("MessageAttachments");
            entity.Property(e => e.AttachmentId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("AttachmentID");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.MediaUrl).HasMaxLength(2048);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(2048);
            entity.Property(e => e.MediaType).HasMaxLength(50);
            entity.Property(e => e.FileSize);
            entity.Property(e => e.DurationSeconds);
            entity.Property(e => e.Width);
            entity.Property(e => e.Height);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Message).WithMany(p => p.MessageAttachments)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("MessageAttachments_MessageID_fkey");
        });

        // 22. Reports
        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("Reports_pkey");
            entity.ToTable("Reports");
            entity.Property(e => e.ReportId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ReportID");
            entity.Property(e => e.ReporterId).HasColumnName("ReporterID");
            entity.Property(e => e.ReportedUserId).HasColumnName("ReportedUserID");
            entity.Property(e => e.ReportedPostId).HasColumnName("ReportedPostID");
            entity.Property(e => e.Type).HasMaxLength(30);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.ResolvedBy).HasColumnName("ResolvedBy");
            entity.Property(e => e.ResolvedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.AppealStatus).HasMaxLength(30);
            entity.Property(e => e.AppealContent).HasColumnType("text");
            entity.Property(e => e.AppealedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.AppealReviewedBy).HasColumnName("AppealReviewedBy");
            entity.Property(e => e.AppealReviewedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.AppealReviewNote).HasColumnType("text");
            entity.Property(e => e.NotifiedReportedUserAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.ReportedPost).WithMany(p => p.Reports)
                .HasForeignKey(d => d.ReportedPostId)
                .HasConstraintName("Reports_ReportedPostID_fkey");

            entity.HasOne(d => d.ReportedUser).WithMany(p => p.ReportReportedUsers)
                .HasForeignKey(d => d.ReportedUserId)
                .HasConstraintName("Reports_ReportedUserID_fkey");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .HasConstraintName("Reports_ReporterID_fkey");

            entity.HasOne(d => d.ResolvedByNavigation).WithMany(p => p.ReportResolvedByNavigations)
                .HasForeignKey(d => d.ResolvedBy)
                .HasConstraintName("Reports_ResolvedBy_fkey");

            entity.HasOne(d => d.AppealReviewedByNavigation).WithMany(p => p.ReportAppealReviewedByNavigations)
                .HasForeignKey(d => d.AppealReviewedBy)
                .HasConstraintName("Reports_AppealReviewedBy_fkey");
        });

        // 23. ReportMedia
        modelBuilder.Entity<ReportMedium>(entity =>
        {
            entity.HasKey(e => e.MediaId).HasName("ReportMedia_pkey");
            entity.ToTable("ReportMedia");
            entity.Property(e => e.MediaId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("MediaID");
            entity.Property(e => e.ReportId).HasColumnName("ReportID");
            entity.Property(e => e.MediaUrl).HasMaxLength(2048);
            entity.Property(e => e.MediaType).HasMaxLength(10);
            entity.Property(e => e.MediaFor).HasMaxLength(10);
            entity.Property(e => e.SortOrder);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Report).WithMany(p => p.ReportMedia)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("ReportMedia_ReportID_fkey");
        });

        // 24. CoachSubscriptionPlans
        modelBuilder.Entity<CoachSubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.CoachSubscriptionPlansId).HasName("CoachSubscriptionPlans_pkey");
            entity.ToTable("CoachSubscriptionPlans");
            entity.Property(e => e.CoachSubscriptionPlansId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("CoachSubscriptionPlansID");
            entity.Property(e => e.PriceId).HasColumnName("PriceID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SubscriptionDuration);
            entity.Property(e => e.TrainingPackageDuration);
            entity.Property(e => e.ImageUrl).HasMaxLength(2048);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Price).WithMany(p => p.CoachSubscriptionPlans)
                .HasForeignKey(d => d.PriceId)
                .HasConstraintName("CoachSubscriptionPlans_PriceID_fkey");
        });

        // 25. TrainingPackages
        modelBuilder.Entity<TrainingPackage>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("TrainingPackages_pkey");
            entity.ToTable("TrainingPackages");
            entity.Property(e => e.PackageId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("PackageID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.DurationDays);
            entity.Property(e => e.SessionCount);
            entity.Property(e => e.MinAge);
            entity.Property(e => e.TargetAudience).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.TrainingPackages)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("TrainingPackages_CoachID_fkey");
        });

        // 26. Orders
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("Orders_pkey");
            entity.ToTable("Orders");
            entity.Property(e => e.OrderId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("OrderID");
            entity.Property(e => e.BuyerId).HasColumnName("BuyerID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.OrderStatus).HasMaxLength(50);
            entity.Property(e => e.OrderType).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Buyer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.BuyerId)
                .HasConstraintName("Orders_BuyerID_fkey");

            entity.HasOne(d => d.Coach).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("Orders_CoachID_fkey");
        });

        // 27. OrderDetails
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailsId).HasName("OrderDetails_pkey");
            entity.ToTable("OrderDetails");
            entity.Property(e => e.OrderDetailsId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("OrderDetailsID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.PackageId).HasColumnName("PackageID");
            entity.Property(e => e.CoachSubscriptionPlansId).HasColumnName("CoachSubscriptionPlansID");
            entity.Property(e => e.PackagePrice).HasPrecision(18, 2);
            entity.Property(e => e.PackageTitle).HasMaxLength(255);
            entity.Property(e => e.PackageDurationDays);
            entity.Property(e => e.CoachName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("OrderDetails_OrderID_fkey");

            entity.HasOne(d => d.Package).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.PackageId)
                .HasConstraintName("OrderDetails_PackageID_fkey");

            entity.HasOne(d => d.CoachSubscriptionPlan).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.CoachSubscriptionPlansId)
                .HasConstraintName("OrderDetails_CoachSubscriptionPlansID_fkey");
        });

        // 28. Payments
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("Payments_pkey");
            entity.ToTable("Payments");
            entity.Property(e => e.PaymentId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("PaymentID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.GatewayId).HasColumnName("GatewayID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.Method).HasMaxLength(50);
            entity.Property(e => e.TransactionRef).HasMaxLength(100);
            entity.Property(e => e.TransactionType).HasMaxLength(30);
            entity.Property(e => e.GatewayTransactionId)
                .HasMaxLength(100)
                .HasColumnName("GatewayTransactionID");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.FailureReason).HasColumnType("text");
            entity.Property(e => e.ProcessedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("Payments_OrderID_fkey");

            entity.HasOne(d => d.Gateway).WithMany(p => p.Payments)
                .HasForeignKey(d => d.GatewayId)
                .HasConstraintName("Payments_GatewayID_fkey");
        });

        // 29. Carts
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("Carts_pkey");
            entity.ToTable("Carts");
            entity.Property(e => e.CartId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("CartID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.PackageId).HasColumnName("PackageID");
            entity.Property(e => e.Quantity);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Package).WithMany(p => p.Carts)
                .HasForeignKey(d => d.PackageId)
                .HasConstraintName("Carts_PackageID_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Carts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Carts_UserID_fkey");
        });

        // 30. CoachUpgrades
        modelBuilder.Entity<CoachUpgrade>(entity =>
        {
            entity.HasKey(e => e.UpgradeId).HasName("CoachUpgrades_pkey");
            entity.ToTable("CoachUpgrades");
            entity.HasIndex(e => e.OrderId, "CoachUpgrades_OrderID_key").IsUnique();

            entity.Property(e => e.UpgradeId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("UpgradeID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CoachSubscriptionPlansId).HasColumnName("CoachSubscriptionPlansID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.EndDay).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.CoachUpgrades)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("CoachUpgrades_CoachID_fkey");

            entity.HasOne(d => d.CoachSubscriptionPlan).WithMany(p => p.CoachUpgrades)
                .HasForeignKey(d => d.CoachSubscriptionPlansId)
                .HasConstraintName("CoachUpgrades_CoachSubscriptionPlansID_fkey");

            entity.HasOne(d => d.Order).WithOne(p => p.CoachUpgrade)
                .HasForeignKey<CoachUpgrade>(d => d.OrderId)
                .HasConstraintName("CoachUpgrades_OrderID_fkey");
        });

        // 31. VideoTutorial
        modelBuilder.Entity<VideoTutorial>(entity =>
        {
            entity.HasKey(e => e.VideoTutorialId).HasName("VideoTutorial_pkey");
            entity.ToTable("VideoTutorial");
            entity.Property(e => e.VideoTutorialId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("VideoTutorialID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.VideoUrl).HasMaxLength(2048);

            entity.HasOne(d => d.Coach).WithMany(p => p.VideoTutorials)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("VideoTutorial_CoachID_fkey");
        });

        // 32. TrainingPlan
        modelBuilder.Entity<TrainingPlan>(entity =>
        {
            entity.HasKey(e => e.TrainingPlanId).HasName("TrainingPlan_pkey");
            entity.ToTable("TrainingPlan");
            entity.Property(e => e.TrainingPlanId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("TrainingPlanID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EndDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.OrderDetailsId).HasColumnName("OrderDetailsID");
            entity.Property(e => e.PublishedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.StartDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.TrainingPlans)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("TrainingPlan_CoachID_fkey");

            entity.HasOne(d => d.OrderDetails).WithMany(p => p.TrainingPlans)
                .HasForeignKey(d => d.OrderDetailsId)
                .HasConstraintName("TrainingPlan_OrderDetailsID_fkey");
        });

        // 33. TrainingPlanExercise
        modelBuilder.Entity<TrainingPlanExercise>(entity =>
        {
            entity.HasKey(e => e.TrainingPlanExerciseId).HasName("TrainingPlanExercise_pkey");
            entity.ToTable("TrainingPlanExercise");
            entity.Property(e => e.TrainingPlanExerciseId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("TrainingPlanExerciseID");
            entity.Property(e => e.DurationMinutes);
            entity.Property(e => e.ExerciseName).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Reps);
            entity.Property(e => e.RestSeconds);
            entity.Property(e => e.Sets);
            entity.Property(e => e.SortOrder);
            entity.Property(e => e.TrainingPlanId).HasColumnName("TrainingPlanID");
            entity.Property(e => e.VideoTutorialId).HasColumnName("VideoTutorialID");

            entity.HasOne(d => d.TrainingPlan).WithMany(p => p.TrainingPlanExercises)
                .HasForeignKey(d => d.TrainingPlanId)
                .HasConstraintName("TrainingPlanExercise_TrainingPlanID_fkey");

            entity.HasOne(d => d.VideoTutorial).WithMany(p => p.TrainingPlanExercises)
                .HasForeignKey(d => d.VideoTutorialId)
                .HasConstraintName("TrainingPlanExercise_VideoTutorialID_fkey");
        });

        // 34. MealPlan
        modelBuilder.Entity<MealPlan>(entity =>
        {
            entity.HasKey(e => e.MealPlanId).HasName("MealPlan_pkey");
            entity.ToTable("MealPlan");
            entity.Property(e => e.MealPlanId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("MealPlanID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EndDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.OrderDetailsId).HasColumnName("OrderDetailsID");
            entity.Property(e => e.PublishedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.StartDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.MealPlans)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("MealPlan_CoachID_fkey");

            entity.HasOne(d => d.OrderDetails).WithMany(p => p.MealPlans)
                .HasForeignKey(d => d.OrderDetailsId)
                .HasConstraintName("MealPlan_OrderDetailsID_fkey");
        });

        // 35. MealPlanItem
        modelBuilder.Entity<MealPlanItem>(entity =>
        {
            entity.HasKey(e => e.MealPlanItemId).HasName("MealPlanItem_pkey");
            entity.ToTable("MealPlanItem");
            entity.Property(e => e.MealPlanItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("MealPlanItemID");
            entity.Property(e => e.Calories);
            entity.Property(e => e.FoodDescription).HasMaxLength(500);
            entity.Property(e => e.MealPlanId).HasColumnName("MealPlanID");
            entity.Property(e => e.MealType).HasMaxLength(100);
            entity.Property(e => e.SortOrder);

            entity.HasOne(d => d.MealPlan).WithMany(p => p.MealPlanItems)
                .HasForeignKey(d => d.MealPlanId)
                .HasConstraintName("MealPlanItem_MealPlanID_fkey");
        });

        // 36. BodyMetricLog
        modelBuilder.Entity<BodyMetricLog>(entity =>
        {
            entity.HasKey(e => e.BodyMetricLogId).HasName("BodyMetricLog_pkey");
            entity.ToTable("BodyMetricLog");
            entity.Property(e => e.BodyMetricLogId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("BodyMetricLogID");
            entity.Property(e => e.BodyWeightKg).HasPrecision(5, 2);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.LogDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.TraineeId).HasColumnName("TraineeID");

            entity.HasOne(d => d.Trainee).WithMany(p => p.BodyMetricLogs)
                .HasForeignKey(d => d.TraineeId)
                .HasConstraintName("BodyMetricLog_TraineeID_fkey");
        });

        // 37. BodyMeasurementDetail
        modelBuilder.Entity<BodyMeasurementDetail>(entity =>
        {
            entity.HasKey(e => e.BodyMeasurementDetailId).HasName("BodyMeasurementDetail_pkey");
            entity.ToTable("BodyMeasurementDetail");
            entity.Property(e => e.BodyMeasurementDetailId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("BodyMeasurementDetailID");
            entity.Property(e => e.BodyMetricLogId).HasColumnName("BodyMetricLogID");
            entity.Property(e => e.MeasurementType).HasMaxLength(200);
            entity.Property(e => e.ValueCm).HasPrecision(5, 2);

            entity.HasOne(d => d.BodyMetricLog).WithMany(p => p.BodyMeasurementDetails)
                .HasForeignKey(d => d.BodyMetricLogId)
                .HasConstraintName("BodyMeasurementDetail_BodyMetricLogID_fkey");
        });

        // 38. WorkoutCompletionLog
        modelBuilder.Entity<WorkoutCompletionLog>(entity =>
        {
            entity.HasKey(e => e.WorkoutCompletionLogId).HasName("WorkoutCompletionLog_pkey");
            entity.ToTable("WorkoutCompletionLog");
            entity.Property(e => e.WorkoutCompletionLogId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("WorkoutCompletionLogID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.LogDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Notes).HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.TraineeId).HasColumnName("TraineeID");
            entity.Property(e => e.TrainingPlanExerciseId).HasColumnName("TrainingPlanExerciseID");

            entity.HasOne(d => d.Trainee).WithMany(p => p.WorkoutCompletionLogs)
                .HasForeignKey(d => d.TraineeId)
                .HasConstraintName("WorkoutCompletionLog_TraineeID_fkey");

            entity.HasOne(d => d.TrainingPlanExercise).WithMany(p => p.WorkoutCompletionLogs)
                .HasForeignKey(d => d.TrainingPlanExerciseId)
                .HasConstraintName("WorkoutCompletionLog_TrainingPlanExerciseID_fkey");
        });

        // 39. Reviews
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("Reviews_pkey");
            entity.ToTable("Reviews");
            entity.Property(e => e.ReviewId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ReviewID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.Comment).HasColumnType("text");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Rating);
            entity.Property(e => e.Reply).HasColumnType("text");
            entity.Property(e => e.TraineeId).HasColumnName("TraineeID");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("Reviews_CoachID_fkey");

            entity.HasOne(d => d.Trainee).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.TraineeId)
                .HasConstraintName("Reviews_TraineeID_fkey");
        });

        // 40. Payouts
        modelBuilder.Entity<Payout>(entity =>
        {
            entity.HasKey(e => e.PayoutId).HasName("Payouts_pkey");
            entity.ToTable("Payouts");
            entity.HasIndex(e => new { e.CoachId, e.PayoutMonth, e.PayoutYear }, "Payouts_CoachID_PayoutMonth_PayoutYear_key").IsUnique();

            entity.Property(e => e.PayoutId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("PayoutID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.NetPayoutAmount).HasPrecision(18, 2);
            entity.Property(e => e.ProcessedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.ProcessedBy).HasColumnName("ProcessedBy");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.SystemCommissionAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalGrossAmount).HasPrecision(18, 2);

            entity.HasOne(d => d.Coach).WithMany(p => p.Payouts)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("Payouts_CoachID_fkey");

            entity.HasOne(d => d.ProcessedByNavigation).WithMany(p => p.PayoutsProcessed)
                .HasForeignKey(d => d.ProcessedBy)
                .HasConstraintName("Payouts_ProcessedBy_fkey");
        });

        // 41. PayoutItems
        modelBuilder.Entity<PayoutItem>(entity =>
        {
            entity.HasKey(e => e.PayoutItemId).HasName("PayoutItems_pkey");
            entity.ToTable("PayoutItems");
            entity.Property(e => e.PayoutItemId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("PayoutItemID");
            entity.Property(e => e.CommissionAmount).HasPrecision(18, 2);
            entity.Property(e => e.CommissionRate).HasPrecision(5, 2);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.GrossAmount).HasPrecision(18, 2);
            entity.Property(e => e.NetAmount).HasPrecision(18, 2);
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.PayoutId).HasColumnName("PayoutID");
            entity.Property(e => e.RefundAdjustment).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);

            entity.HasOne(d => d.Order).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("PayoutItems_OrderID_fkey");

            entity.HasOne(d => d.Payout).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.PayoutId)
                .HasConstraintName("PayoutItems_PayoutID_fkey");
        });

        // 42. RefundRequests
        modelBuilder.Entity<RefundRequest>(entity =>
        {
            entity.HasKey(e => e.RefundRequestId).HasName("RefundRequests_pkey");
            entity.ToTable("RefundRequests");
            entity.Property(e => e.RefundRequestId)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("RefundRequestID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.RequestedBy).HasColumnName("RequestedBy");
            entity.Property(e => e.TermId).HasColumnName("TermID");
            entity.Property(e => e.RequestedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ApprovedAmount).HasPrecision(18, 2);
            entity.Property(e => e.Reason).HasColumnType("text");
            entity.Property(e => e.EvidenceUrls).HasColumnType("text");
            entity.Property(e => e.Status).HasMaxLength(30);
            entity.Property(e => e.ReviewedBy).HasColumnName("ReviewedBy");
            entity.Property(e => e.ReviewedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.StaffNote).HasColumnType("text");
            entity.Property(e => e.RefundTransactionRef).HasMaxLength(100);
            entity.Property(e => e.RequestedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.RefundedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Order).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("RefundRequests_OrderID_fkey");

            entity.HasOne(d => d.Payment).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.PaymentId)
                .HasConstraintName("RefundRequests_PaymentID_fkey");

            entity.HasOne(d => d.RequestedByNavigation).WithMany(p => p.RefundRequestsRequested)
                .HasForeignKey(d => d.RequestedBy)
                .HasConstraintName("RefundRequests_RequestedBy_fkey");

            entity.HasOne(d => d.Term).WithMany(p => p.RefundRequests)
                .HasForeignKey(d => d.TermId)
                .HasConstraintName("RefundRequests_TermID_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.RefundRequestsReviewed)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("RefundRequests_ReviewedBy_fkey");
        });

        // 43. Notifications
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Notifications_pkey");
            entity.ToTable("Notifications");
            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("ID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.ActorId).HasColumnName("ActorID");
            entity.Property(e => e.ReferenceId).HasColumnName("ReferenceID");
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("Desciption");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Notifications_UserID_fkey");

            entity.HasOne(d => d.Actor).WithMany(p => p.ActorNotifications)
                .HasForeignKey(d => d.ActorId)
                .HasConstraintName("Notifications_ActorID_fkey");
        });

        // 44. AuditLogs
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("AuditLogs_pkey");
            entity.ToTable("AuditLogs");
            entity.Property(e => e.AuditLogId).HasColumnName("AuditLogID");
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.ActorAccountId).HasColumnName("ActorAccountID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.EntityId)
                .HasMaxLength(100)
                .HasColumnName("EntityID");
            entity.Property(e => e.EntityType).HasMaxLength(100);
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(45)
                .HasColumnName("IPAddress");
            entity.Property(e => e.NewValue).HasColumnType("jsonb");
            entity.Property(e => e.OldValue).HasColumnType("jsonb");

            entity.HasOne(d => d.ActorAccount).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.ActorAccountId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("AuditLogs_ActorAccountID_fkey");
        });

        // 45. Normalize all table and column names to lowercase to seamlessly match PostgreSQL unquoted catalog
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entity.SetTableName(tableName.ToLowerInvariant());
            }

            foreach (var property in entity.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrEmpty(columnName))
                {
                    property.SetColumnName(columnName.ToLowerInvariant());
                }
            }
        }
    }
}

