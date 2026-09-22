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

    public virtual DbSet<CoachProfile> CoachProfiles { get; set; }

    public virtual DbSet<CoachUpgrade> CoachUpgrades { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Friendship> Friendships { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<MealPlan> MealPlans { get; set; }

    public virtual DbSet<MealPlanItem> MealPlanItems { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Otplog> Otplogs { get; set; }

    public virtual DbSet<Participant> Participants { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentGatewayConfig> PaymentGatewayConfigs { get; set; }

    public virtual DbSet<Payout> Payouts { get; set; }

    public virtual DbSet<PayoutItem> PayoutItems { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<PostInteraction> PostInteractions { get; set; }

    public virtual DbSet<PostMedium> PostMedia { get; set; }

    public virtual DbSet<Price> Prices { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Sport> Sports { get; set; }

    public virtual DbSet<TrainingPackage> TrainingPackages { get; set; }

    public virtual DbSet<TrainingPlan> TrainingPlans { get; set; }

    public virtual DbSet<TrainingPlanExercise> TrainingPlanExercises { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBlock> UserBlocks { get; set; }

    public virtual DbSet<VideoCall> VideoCalls { get; set; }

    public virtual DbSet<VideoTutorial> VideoTutorials { get; set; }

    public virtual DbSet<WorkoutCompletionLog> WorkoutCompletionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("pgcrypto")
            .HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("AuditLogs_pkey");

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

        modelBuilder.Entity<BodyMeasurementDetail>(entity =>
        {
            entity.HasKey(e => e.BodyMeasurementDetailId).HasName("BodyMeasurementDetail_pkey");

            entity.ToTable("BodyMeasurementDetail");

            entity.Property(e => e.BodyMeasurementDetailId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("BodyMeasurementDetailID");
            entity.Property(e => e.BodyMetricLogId).HasColumnName("BodyMetricLogID");
            entity.Property(e => e.MeasurementType).HasMaxLength(200);
            entity.Property(e => e.ValueCm).HasPrecision(5, 2);

            entity.HasOne(d => d.BodyMetricLog).WithMany(p => p.BodyMeasurementDetails)
                .HasForeignKey(d => d.BodyMetricLogId)
                .HasConstraintName("BodyMeasurementDetail_BodyMetricLogID_fkey");
        });

        modelBuilder.Entity<BodyMetricLog>(entity =>
        {
            entity.HasKey(e => e.BodyMetricLogId).HasName("BodyMetricLog_pkey");

            entity.ToTable("BodyMetricLog");

            entity.Property(e => e.BodyMetricLogId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("BodyMetricLogID");
            entity.Property(e => e.BodyWeightKg).HasPrecision(5, 2);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.LogDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Notes).HasColumnType("character varying");
            entity.Property(e => e.TraineeId).HasColumnName("TraineeID");

            entity.HasOne(d => d.Trainee).WithMany(p => p.BodyMetricLogs)
                .HasForeignKey(d => d.TraineeId)
                .HasConstraintName("BodyMetricLog_TraineeID_fkey");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("Carts_pkey");

            entity.HasIndex(e => new { e.UserId, e.PackageId }, "Carts_UserID_PackageID_key").IsUnique();

            entity.Property(e => e.CartId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("CartID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.PackageId).HasColumnName("PackageID");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Package).WithMany(p => p.Carts)
                .HasForeignKey(d => d.PackageId)
                .HasConstraintName("Carts_PackageID_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Carts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Carts_UserID_fkey");
        });

        modelBuilder.Entity<CoachBankAccount>(entity =>
        {
            entity.HasKey(e => e.BankId).HasName("CoachBankAccounts_pkey");

            entity.Property(e => e.BankId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("BankID");
            entity.Property(e => e.AccountName).HasMaxLength(255);
            entity.Property(e => e.AccountNumber).HasMaxLength(50);
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

        modelBuilder.Entity<CoachProfile>(entity =>
        {
            entity.HasKey(e => e.CoachId).HasName("CoachProfiles_pkey");

            entity.Property(e => e.CoachId)
                .ValueGeneratedNever()
                .HasColumnName("CoachID");
            entity.Property(e => e.ApprovalStatus).HasMaxLength(50);
            entity.Property(e => e.CertificateUrl).HasMaxLength(2048);
            entity.Property(e => e.IdentityCardUrl).HasMaxLength(2048);
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
                    r => r.HasOne<Location>().WithMany()
                        .HasForeignKey("LocationId")
                        .HasConstraintName("CoachLocations_LocationID_fkey"),
                    l => l.HasOne<CoachProfile>().WithMany()
                        .HasForeignKey("CoachId")
                        .HasConstraintName("CoachLocations_CoachID_fkey"),
                    j =>
                    {
                        j.HasKey("CoachId", "LocationId").HasName("CoachLocations_pkey");
                        j.ToTable("CoachLocations");
                        j.IndexerProperty<Guid>("CoachId").HasColumnName("CoachID");
                        j.IndexerProperty<Guid>("LocationId").HasColumnName("LocationID");
                    });

            entity.HasMany(d => d.Sports).WithMany(p => p.Coaches)
                .UsingEntity<Dictionary<string, object>>(
                    "CoachSport",
                    r => r.HasOne<Sport>().WithMany()
                        .HasForeignKey("SportId")
                        .HasConstraintName("CoachSports_SportID_fkey"),
                    l => l.HasOne<CoachProfile>().WithMany()
                        .HasForeignKey("CoachId")
                        .HasConstraintName("CoachSports_CoachID_fkey"),
                    j =>
                    {
                        j.HasKey("CoachId", "SportId").HasName("CoachSports_pkey");
                        j.ToTable("CoachSports");
                        j.IndexerProperty<Guid>("CoachId").HasColumnName("CoachID");
                        j.IndexerProperty<Guid>("SportId").HasColumnName("SportID");
                    });
        });

        modelBuilder.Entity<CoachUpgrade>(entity =>
        {
            entity.HasKey(e => e.UpgradeId).HasName("CoachUpgrades_pkey");

            entity.HasIndex(e => e.OrderId, "CoachUpgrades_OrderID_key").IsUnique();

            entity.Property(e => e.UpgradeId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("UpgradeID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.PriceId).HasColumnName("PriceID");
            entity.Property(e => e.Status).HasMaxLength(50);

            entity.HasOne(d => d.Coach).WithMany(p => p.CoachUpgrades)
                .HasForeignKey(d => d.CoachId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("CoachUpgrades_CoachID_fkey");

            entity.HasOne(d => d.Order).WithOne(p => p.CoachUpgrade)
                .HasForeignKey<CoachUpgrade>(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("CoachUpgrades_OrderID_fkey");

            entity.HasOne(d => d.Price).WithMany(p => p.CoachUpgrades)
                .HasForeignKey(d => d.PriceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("CoachUpgrades_PriceID_fkey");
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Comments_pkey");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.Content).HasMaxLength(180);
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
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("Comments_ParentCommentID_fkey");

            entity.HasOne(d => d.Post).WithMany(p => p.Comments)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("Comments_PostID_fkey");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Conversations_pkey");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatorId).HasColumnName("CreatorID");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Type).HasMaxLength(10);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Creator).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.CreatorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("Conversations_CreatorID_fkey");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => new { e.RequesterId, e.AddresseeId }).HasName("Friendships_pkey");

            entity.Property(e => e.RequesterId).HasColumnName("RequesterID");
            entity.Property(e => e.AddresseeId).HasColumnName("AddresseeID");
            entity.Property(e => e.AcceptedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.Addressee).WithMany(p => p.FriendshipAddressees)
                .HasForeignKey(d => d.AddresseeId)
                .HasConstraintName("Friendships_AddresseeID_fkey");

            entity.HasOne(d => d.Requester).WithMany(p => p.FriendshipRequesters)
                .HasForeignKey(d => d.RequesterId)
                .HasConstraintName("Friendships_RequesterID_fkey");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("Locations_pkey");

            entity.Property(e => e.LocationId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("LocationID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.LocationName).HasMaxLength(200);
        });

        modelBuilder.Entity<MealPlan>(entity =>
        {
            entity.HasKey(e => e.MealPlanId).HasName("MealPlan_pkey");

            entity.ToTable("MealPlan");

            entity.Property(e => e.MealPlanId)
                .HasDefaultValueSql("gen_random_uuid()")
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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MealPlan_CoachID_fkey");

            entity.HasOne(d => d.OrderDetails).WithMany(p => p.MealPlans)
                .HasForeignKey(d => d.OrderDetailsId)
                .HasConstraintName("MealPlan_OrderDetailsID_fkey");
        });

        modelBuilder.Entity<MealPlanItem>(entity =>
        {
            entity.HasKey(e => e.MealPlanItemId).HasName("MealPlanItem_pkey");

            entity.ToTable("MealPlanItem");

            entity.Property(e => e.MealPlanItemId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MealPlanItemID");
            entity.Property(e => e.FoodDescription).HasMaxLength(500);
            entity.Property(e => e.MealPlanId).HasColumnName("MealPlanID");
            entity.Property(e => e.MealType).HasMaxLength(100);

            entity.HasOne(d => d.MealPlan).WithMany(p => p.MealPlanItems)
                .HasForeignKey(d => d.MealPlanId)
                .HasConstraintName("MealPlanItem_MealPlanID_fkey");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Messages_pkey");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.Content).HasMaxLength(300);
            entity.Property(e => e.ConversationId).HasColumnName("ConversationID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.MessageType).HasMaxLength(5);
            entity.Property(e => e.SenderId).HasColumnName("SenderID");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("Messages_ConversationID_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("Messages_SenderID_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Notifications_pkey");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.ReferenceId).HasColumnName("ReferenceID");
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Notifications_UserID_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("Orders_pkey");

            entity.Property(e => e.OrderId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("OrderID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.OrderStatus).HasMaxLength(50);
            entity.Property(e => e.OrderType).HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.TraineeId).HasColumnName("TraineeID");

            entity.HasOne(d => d.Coach).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("Orders_CoachID_fkey");

            entity.HasOne(d => d.Trainee).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TraineeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Orders_TraineeID_fkey");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailsId).HasName("OrderDetails_pkey");

            entity.Property(e => e.OrderDetailsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("OrderDetailsID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.PackagePrice).HasPrecision(18, 2);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("OrderDetails_OrderID_fkey");
        });

        modelBuilder.Entity<Otplog>(entity =>
        {
            entity.HasKey(e => e.OtpId).HasName("OTPLogs_pkey");

            entity.ToTable("OTPLogs");

            entity.Property(e => e.OtpId)
                .HasDefaultValueSql("gen_random_uuid()")
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
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("OTPLogs_UserID_fkey");
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.UserId }).HasName("Participants_pkey");

            entity.Property(e => e.ConversationId).HasColumnName("ConversationID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.HistoryDeletedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("JoinedAT");
            entity.Property(e => e.LastReadMessageId).HasColumnName("LastReadMessageID");
            entity.Property(e => e.Status).HasMaxLength(7);

            entity.HasOne(d => d.Conversation).WithMany(p => p.Participants)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("Participants_ConversationID_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Participants)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("Participants_UserID_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("Payments_pkey");

            entity.Property(e => e.PaymentId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("PaymentID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.GatewayId)
                .HasMaxLength(50)
                .HasColumnName("GatewayID");
            entity.Property(e => e.GatewayTransactionId)
                .HasMaxLength(100)
                .HasColumnName("GatewayTransactionID");
            entity.Property(e => e.Method).HasMaxLength(50);
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.ProcessedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.TransactionRef).HasMaxLength(100);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("Payments_OrderID_fkey");
        });

        modelBuilder.Entity<PaymentGatewayConfig>(entity =>
        {
            entity.HasKey(e => e.GatewayId).HasName("PaymentGatewayConfigs_pkey");

            entity.Property(e => e.GatewayId).HasColumnName("GatewayID");
            entity.Property(e => e.ClientId)
                .HasMaxLength(255)
                .HasColumnName("ClientID");
            entity.Property(e => e.GatewayName).HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.WebhookUrl).HasMaxLength(2048);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PaymentGatewayConfigs)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("PaymentGatewayConfigs_UpdatedBy_fkey");
        });

        modelBuilder.Entity<Payout>(entity =>
        {
            entity.HasKey(e => e.PayoutId).HasName("Payouts_pkey");

            entity.HasIndex(e => new { e.CoachId, e.PayoutMonth, e.PayoutYear }, "Payouts_CoachID_PayoutMonth_PayoutYear_key").IsUnique();

            entity.Property(e => e.PayoutId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("PayoutID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.NetPayoutAmount).HasPrecision(18, 2);
            entity.Property(e => e.ProcessedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.SystemCommissionAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalGrossAmount).HasPrecision(18, 2);

            entity.HasOne(d => d.Coach).WithMany(p => p.Payouts)
                .HasForeignKey(d => d.CoachId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Payouts_CoachID_fkey");

            entity.HasOne(d => d.ProcessedByNavigation).WithMany(p => p.Payouts)
                .HasForeignKey(d => d.ProcessedBy)
                .HasConstraintName("Payouts_ProcessedBy_fkey");
        });

        modelBuilder.Entity<PayoutItem>(entity =>
        {
            entity.HasKey(e => e.PayoutItemId).HasName("PayoutItems_pkey");

            entity.Property(e => e.PayoutItemId)
                .HasDefaultValueSql("gen_random_uuid()")
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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("PayoutItems_OrderID_fkey");

            entity.HasOne(d => d.Payout).WithMany(p => p.PayoutItems)
                .HasForeignKey(d => d.PayoutId)
                .HasConstraintName("PayoutItems_PayoutID_fkey");
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Posts_pkey");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.LocationId).HasColumnName("LocationID");
            entity.Property(e => e.PostType)
                .HasMaxLength(50)
                .HasColumnName("PostType");

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

        modelBuilder.Entity<PostInteraction>(entity =>
        {
            entity.HasKey(e => new { e.PostId, e.UserId }).HasName("PostInteractions_pkey");

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

        modelBuilder.Entity<PostMedium>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PostMedia_pkey");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.MediaType).HasMaxLength(50);
            entity.Property(e => e.MediaUrl)
                .HasMaxLength(150)
                .HasColumnName("MediaURL");
            entity.Property(e => e.PostId).HasColumnName("PostID");

            entity.HasOne(d => d.Post).WithMany(p => p.PostMedia)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("PostMedia_PostID_fkey");
        });

        modelBuilder.Entity<Price>(entity =>
        {
            entity.HasKey(e => e.PriceId).HasName("Price_pkey");

            entity.ToTable("Price");

            entity.Property(e => e.PriceId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("PriceID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.TokenId).HasName("RefreshTokens_pkey");

            entity.HasIndex(e => e.TokenHash, "RefreshTokens_TokenHash_key").IsUnique();

            entity.Property(e => e.TokenId)
                .HasDefaultValueSql("gen_random_uuid()")
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

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("Reports_pkey");

            entity.Property(e => e.ReportId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ReportID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ReportedPostId).HasColumnName("ReportedPostID");
            entity.Property(e => e.ReportedUserId).HasColumnName("ReportedUserID");
            entity.Property(e => e.ReporterId).HasColumnName("ReporterID");
            entity.Property(e => e.ResolvedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.Status).HasMaxLength(30);

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
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("Reviews_pkey");

            entity.Property(e => e.ReviewId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ReviewID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.TraineeId).HasColumnName("TraineeID");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Coach).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.CoachId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Reviews_CoachID_fkey");

            entity.HasOne(d => d.Trainee).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.TraineeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Reviews_TraineeID_fkey");
        });

        modelBuilder.Entity<Sport>(entity =>
        {
            entity.HasKey(e => e.SportId).HasName("Sports_pkey");

            entity.Property(e => e.SportId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("SportID");
            entity.Property(e => e.SportName).HasMaxLength(100);
        });

        modelBuilder.Entity<TrainingPackage>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("TrainingPackages_pkey");

            entity.Property(e => e.PackageId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("PackageID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OrderDetailsId).HasColumnName("OrderDetailsID");
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Coach).WithMany(p => p.TrainingPackages)
                .HasForeignKey(d => d.CoachId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TrainingPackages_CoachID_fkey");

            entity.HasOne(d => d.OrderDetails).WithMany(p => p.TrainingPackages)
                .HasForeignKey(d => d.OrderDetailsId)
                .HasConstraintName("TrainingPackages_OrderDetailsID_fkey");
        });

        modelBuilder.Entity<TrainingPlan>(entity =>
        {
            entity.HasKey(e => e.TrainingPlanId).HasName("TrainingPlan_pkey");

            entity.ToTable("TrainingPlan");

            entity.Property(e => e.TrainingPlanId)
                .HasDefaultValueSql("gen_random_uuid()")
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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("TrainingPlan_CoachID_fkey");

            entity.HasOne(d => d.OrderDetails).WithMany(p => p.TrainingPlans)
                .HasForeignKey(d => d.OrderDetailsId)
                .HasConstraintName("TrainingPlan_OrderDetailsID_fkey");
        });

        modelBuilder.Entity<TrainingPlanExercise>(entity =>
        {
            entity.HasKey(e => e.TrainingPlanExerciseId).HasName("TrainingPlanExercise_pkey");

            entity.ToTable("TrainingPlanExercise");

            entity.Property(e => e.TrainingPlanExerciseId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("TrainingPlanExerciseID");
            entity.Property(e => e.ExerciseName).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.TrainingPlanId).HasColumnName("TrainingPlanID");
            entity.Property(e => e.VideoTutorialId).HasColumnName("VideoTutorialID");

            entity.HasOne(d => d.TrainingPlan).WithMany(p => p.TrainingPlanExercises)
                .HasForeignKey(d => d.TrainingPlanId)
                .HasConstraintName("TrainingPlanExercise_TrainingPlanID_fkey");

            entity.HasOne(d => d.VideoTutorial).WithMany(p => p.TrainingPlanExercises)
                .HasForeignKey(d => d.VideoTutorialId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("TrainingPlanExercise_VideoTutorialID_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("Users_pkey");

            entity.HasIndex(e => e.Email, "Users_Email_key").IsUnique();

            entity.HasIndex(e => e.GoogleProviderId, "Users_GoogleProviderID_key").IsUnique();

            entity.HasIndex(e => e.PhoneNumber, "Users_PhoneNumber_key").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("gen_random_uuid()")
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
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.RoleCode).HasMaxLength(8);
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.LockedByNavigation).WithMany(p => p.InverseLockedByNavigation)
                .HasForeignKey(d => d.LockedBy)
                .HasConstraintName("Users_LockedBy_fkey");

            entity.HasMany(d => d.Sports).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserFavoriteSport",
                    r => r.HasOne<Sport>().WithMany()
                        .HasForeignKey("SportId")
                        .HasConstraintName("UserFavoriteSports_SportID_fkey"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .HasConstraintName("UserFavoriteSports_UserID_fkey"),
                    j =>
                    {
                        j.HasKey("UserId", "SportId").HasName("UserFavoriteSports_pkey");
                        j.ToTable("UserFavoriteSports");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("UserID");
                        j.IndexerProperty<Guid>("SportId").HasColumnName("SportID");
                    });
        });

        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("userBlocks_pkey");

            entity.ToTable("userBlocks");

            entity.HasIndex(e => new { e.BlockerId, e.BlockedId }, "userBlocks_BlockerID_BlockedID_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ID");
            entity.Property(e => e.BlockedId).HasColumnName("BlockedID");
            entity.Property(e => e.BlockerId).HasColumnName("BlockerID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Blocked).WithMany(p => p.UserBlockBlockeds)
                .HasForeignKey(d => d.BlockedId)
                .HasConstraintName("userBlocks_BlockedID_fkey");

            entity.HasOne(d => d.Blocker).WithMany(p => p.UserBlockBlockers)
                .HasForeignKey(d => d.BlockerId)
                .HasConstraintName("userBlocks_BlockerID_fkey");
        });

        modelBuilder.Entity<VideoCall>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("VideoCalls_pkey");

            entity.Property(e => e.MessageId)
                .ValueGeneratedNever()
                .HasColumnName("MessageID");
            entity.Property(e => e.CallStatus).HasMaxLength(20);

            entity.HasOne(d => d.Message).WithOne(p => p.VideoCall)
                .HasForeignKey<VideoCall>(d => d.MessageId)
                .HasConstraintName("VideoCalls_MessageID_fkey");
        });

        modelBuilder.Entity<VideoTutorial>(entity =>
        {
            entity.HasKey(e => e.VideoTutorialId).HasName("VideoTutorial_pkey");

            entity.ToTable("VideoTutorial");

            entity.Property(e => e.VideoTutorialId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("VideoTutorialID");
            entity.Property(e => e.CoachId).HasColumnName("CoachID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.VideoUrl).HasMaxLength(500);

            entity.HasOne(d => d.Coach).WithMany(p => p.VideoTutorials)
                .HasForeignKey(d => d.CoachId)
                .HasConstraintName("VideoTutorial_CoachID_fkey");
        });

        modelBuilder.Entity<WorkoutCompletionLog>(entity =>
        {
            entity.HasKey(e => e.WorkoutCompletionLogId).HasName("WorkoutCompletionLog_pkey");

            entity.ToTable("WorkoutCompletionLog");

            entity.Property(e => e.WorkoutCompletionLogId)
                .HasDefaultValueSql("gen_random_uuid()")
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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WorkoutCompletionLog_TraineeID_fkey");

            entity.HasOne(d => d.TrainingPlanExercise).WithMany(p => p.WorkoutCompletionLogs)
                .HasForeignKey(d => d.TrainingPlanExerciseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WorkoutCompletionLog_TrainingPlanExerciseID_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
