using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FitSocial.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    LocationID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    LocationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Locations_pkey", x => x.LocationID);
                });

            migrationBuilder.CreateTable(
                name: "Price",
                columns: table => new
                {
                    PriceID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Price_pkey", x => x.PriceID);
                });

            migrationBuilder.CreateTable(
                name: "Sports",
                columns: table => new
                {
                    SportID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SportName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Sports_pkey", x => x.SportID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    GoogleProviderID = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoleCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    AvatarUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    LockedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Users_pkey", x => x.UserID);
                    table.ForeignKey(
                        name: "Users_LockedBy_fkey",
                        column: x => x.LockedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditLogID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorAccountID = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "jsonb", nullable: true),
                    NewValue = table.Column<string>(type: "jsonb", nullable: true),
                    IPAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("AuditLogs_pkey", x => x.AuditLogID);
                    table.ForeignKey(
                        name: "AuditLogs_ActorAccountID_fkey",
                        column: x => x.ActorAccountID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BodyMetricLog",
                columns: table => new
                {
                    BodyMetricLogID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TraineeID = table.Column<Guid>(type: "uuid", nullable: false),
                    LogDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    BodyWeightKg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CalorieIntake = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("BodyMetricLog_pkey", x => x.BodyMetricLogID);
                    table.ForeignKey(
                        name: "BodyMetricLog_TraineeID_fkey",
                        column: x => x.TraineeID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachProfiles",
                columns: table => new
                {
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceYears = table.Column<int>(type: "integer", nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    IdentityCardUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CertificateUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ApprovalStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("CoachProfiles_pkey", x => x.CoachID);
                    table.ForeignKey(
                        name: "CoachProfiles_ApprovedBy_fkey",
                        column: x => x.ApprovedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "CoachProfiles_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CreatorID = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Conversations_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "Conversations_CreatorID_fkey",
                        column: x => x.CreatorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    RequesterID = table.Column<Guid>(type: "uuid", nullable: false),
                    AddresseeID = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Friendships_pkey", x => new { x.RequesterID, x.AddresseeID });
                    table.ForeignKey(
                        name: "Friendships_AddresseeID_fkey",
                        column: x => x.AddresseeID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Friendships_RequesterID_fkey",
                        column: x => x.RequesterID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceID = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Notifications_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "Notifications_UserID_fkey",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OTPLogs",
                columns: table => new
                {
                    OtpID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserID = table.Column<Guid>(type: "uuid", nullable: true),
                    OtpHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: true, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("OTPLogs_pkey", x => x.OtpID);
                    table.ForeignKey(
                        name: "OTPLogs_UserID_fkey",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentGatewayConfigs",
                columns: table => new
                {
                    GatewayID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GatewayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientID = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EncryptedApiKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    EncryptedChecksumKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    WebhookUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PaymentGatewayConfigs_pkey", x => x.GatewayID);
                    table.ForeignKey(
                        name: "PaymentGatewayConfigs_UpdatedBy_fkey",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AuthorID = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    SportID = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationID = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Posts_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "Posts_AuthorID_fkey",
                        column: x => x.AuthorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Posts_LocationID_fkey",
                        column: x => x.LocationID,
                        principalTable: "Locations",
                        principalColumn: "LocationID");
                    table.ForeignKey(
                        name: "Posts_SportID_fkey",
                        column: x => x.SportID,
                        principalTable: "Sports",
                        principalColumn: "SportID");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    TokenID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeviceInfo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("RefreshTokens_pkey", x => x.TokenID);
                    table.ForeignKey(
                        name: "RefreshTokens_UserID_fkey",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "userBlocks",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BlockerID = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedID = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("userBlocks_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "userBlocks_BlockedID_fkey",
                        column: x => x.BlockedID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "userBlocks_BlockerID_fkey",
                        column: x => x.BlockerID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFavoriteSports",
                columns: table => new
                {
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    SportID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserFavoriteSports_pkey", x => new { x.UserID, x.SportID });
                    table.ForeignKey(
                        name: "UserFavoriteSports_SportID_fkey",
                        column: x => x.SportID,
                        principalTable: "Sports",
                        principalColumn: "SportID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "UserFavoriteSports_UserID_fkey",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BodyMeasurementDetail",
                columns: table => new
                {
                    BodyMeasurementDetailID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    BodyMetricLogID = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasurementType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValueCm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("BodyMeasurementDetail_pkey", x => x.BodyMeasurementDetailID);
                    table.ForeignKey(
                        name: "BodyMeasurementDetail_BodyMetricLogID_fkey",
                        column: x => x.BodyMetricLogID,
                        principalTable: "BodyMetricLog",
                        principalColumn: "BodyMetricLogID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachBankAccounts",
                columns: table => new
                {
                    BankID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BankCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("CoachBankAccounts_pkey", x => x.BankID);
                    table.ForeignKey(
                        name: "CoachBankAccounts_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachLocations",
                columns: table => new
                {
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("CoachLocations_pkey", x => new { x.CoachID, x.LocationID });
                    table.ForeignKey(
                        name: "CoachLocations_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "CoachLocations_LocationID_fkey",
                        column: x => x.LocationID,
                        principalTable: "Locations",
                        principalColumn: "LocationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachSports",
                columns: table => new
                {
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    SportID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("CoachSports_pkey", x => new { x.CoachID, x.SportID });
                    table.ForeignKey(
                        name: "CoachSports_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "CoachSports_SportID_fkey",
                        column: x => x.SportID,
                        principalTable: "Sports",
                        principalColumn: "SportID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    OrderID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TraineeID = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    OrderStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OrderType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Orders_pkey", x => x.OrderID);
                    table.ForeignKey(
                        name: "Orders_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID");
                    table.ForeignKey(
                        name: "Orders_TraineeID_fkey",
                        column: x => x.TraineeID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Payouts",
                columns: table => new
                {
                    PayoutID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    PayoutMonth = table.Column<int>(type: "integer", nullable: false),
                    PayoutYear = table.Column<int>(type: "integer", nullable: false),
                    TotalGrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SystemCommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NetPayoutAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProcessedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Payouts_pkey", x => x.PayoutID);
                    table.ForeignKey(
                        name: "Payouts_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID");
                    table.ForeignKey(
                        name: "Payouts_ProcessedBy_fkey",
                        column: x => x.ProcessedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TraineeID = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Reply = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Reviews_pkey", x => x.ReviewID);
                    table.ForeignKey(
                        name: "Reviews_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID");
                    table.ForeignKey(
                        name: "Reviews_TraineeID_fkey",
                        column: x => x.TraineeID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "VideoTutorial",
                columns: table => new
                {
                    VideoTutorialID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("VideoTutorial_pkey", x => x.VideoTutorialID);
                    table.ForeignKey(
                        name: "VideoTutorial_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ConversationID = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderID = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    MessageType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Messages_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "Messages_ConversationID_fkey",
                        column: x => x.ConversationID,
                        principalTable: "Conversations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Messages_SenderID_fkey",
                        column: x => x.SenderID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    ConversationID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAT = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastReadMessageID = table.Column<Guid>(type: "uuid", nullable: true),
                    HistoryDeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Participants_pkey", x => new { x.ConversationID, x.UserID });
                    table.ForeignKey(
                        name: "Participants_ConversationID_fkey",
                        column: x => x.ConversationID,
                        principalTable: "Conversations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Participants_UserID_fkey",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PostID = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorID = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCommentID = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Comments_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "Comments_AuthorID_fkey",
                        column: x => x.AuthorID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Comments_ParentCommentID_fkey",
                        column: x => x.ParentCommentID,
                        principalTable: "Comments",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Comments_PostID_fkey",
                        column: x => x.PostID,
                        principalTable: "Posts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostInteractions",
                columns: table => new
                {
                    PostID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PostInteractions_pkey", x => new { x.PostID, x.UserID });
                    table.ForeignKey(
                        name: "PostInteractions_PostID_fkey",
                        column: x => x.PostID,
                        principalTable: "Posts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "PostInteractions_UserID_fkey",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostMedia",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PostID = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaURL = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    MediaType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PostMedia_pkey", x => x.ID);
                    table.ForeignKey(
                        name: "PostMedia_PostID_fkey",
                        column: x => x.PostID,
                        principalTable: "Posts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    ReportID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ReporterID = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedUserID = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedPostID = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Reports_pkey", x => x.ReportID);
                    table.ForeignKey(
                        name: "Reports_ReportedPostID_fkey",
                        column: x => x.ReportedPostID,
                        principalTable: "Posts",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "Reports_ReportedUserID_fkey",
                        column: x => x.ReportedUserID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "Reports_ReporterID_fkey",
                        column: x => x.ReporterID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "Reports_ResolvedBy_fkey",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "UserID");
                });

            migrationBuilder.CreateTable(
                name: "CoachUpgrades",
                columns: table => new
                {
                    UpgradeID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PriceID = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderID = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("CoachUpgrades_pkey", x => x.UpgradeID);
                    table.ForeignKey(
                        name: "CoachUpgrades_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID");
                    table.ForeignKey(
                        name: "CoachUpgrades_OrderID_fkey",
                        column: x => x.OrderID,
                        principalTable: "Orders",
                        principalColumn: "OrderID");
                    table.ForeignKey(
                        name: "CoachUpgrades_PriceID_fkey",
                        column: x => x.PriceID,
                        principalTable: "Price",
                        principalColumn: "PriceID");
                });

            migrationBuilder.CreateTable(
                name: "OrderDetails",
                columns: table => new
                {
                    OrderDetailsID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderID = table.Column<Guid>(type: "uuid", nullable: false),
                    PackagePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("OrderDetails_pkey", x => x.OrderDetailsID);
                    table.ForeignKey(
                        name: "OrderDetails_OrderID_fkey",
                        column: x => x.OrderID,
                        principalTable: "Orders",
                        principalColumn: "OrderID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    PaymentID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OrderID = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GatewayID = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TransactionRef = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GatewayTransactionID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Payments_pkey", x => x.PaymentID);
                    table.ForeignKey(
                        name: "Payments_OrderID_fkey",
                        column: x => x.OrderID,
                        principalTable: "Orders",
                        principalColumn: "OrderID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayoutItems",
                columns: table => new
                {
                    PayoutItemID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PayoutID = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderID = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RefundAdjustment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PayoutItems_pkey", x => x.PayoutItemID);
                    table.ForeignKey(
                        name: "PayoutItems_OrderID_fkey",
                        column: x => x.OrderID,
                        principalTable: "Orders",
                        principalColumn: "OrderID");
                    table.ForeignKey(
                        name: "PayoutItems_PayoutID_fkey",
                        column: x => x.PayoutID,
                        principalTable: "Payouts",
                        principalColumn: "PayoutID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoCalls",
                columns: table => new
                {
                    MessageID = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    CallStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("VideoCalls_pkey", x => x.MessageID);
                    table.ForeignKey(
                        name: "VideoCalls_MessageID_fkey",
                        column: x => x.MessageID,
                        principalTable: "Messages",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealPlan",
                columns: table => new
                {
                    MealPlanID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderDetailsID = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("MealPlan_pkey", x => x.MealPlanID);
                    table.ForeignKey(
                        name: "MealPlan_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID");
                    table.ForeignKey(
                        name: "MealPlan_OrderDetailsID_fkey",
                        column: x => x.OrderDetailsID,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailsID");
                });

            migrationBuilder.CreateTable(
                name: "TrainingPackages",
                columns: table => new
                {
                    PackageID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    OrderDetailsID = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("TrainingPackages_pkey", x => x.PackageID);
                    table.ForeignKey(
                        name: "TrainingPackages_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID");
                    table.ForeignKey(
                        name: "TrainingPackages_OrderDetailsID_fkey",
                        column: x => x.OrderDetailsID,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailsID");
                });

            migrationBuilder.CreateTable(
                name: "TrainingPlan",
                columns: table => new
                {
                    TrainingPlanID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CoachID = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderDetailsID = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("TrainingPlan_pkey", x => x.TrainingPlanID);
                    table.ForeignKey(
                        name: "TrainingPlan_CoachID_fkey",
                        column: x => x.CoachID,
                        principalTable: "CoachProfiles",
                        principalColumn: "CoachID");
                    table.ForeignKey(
                        name: "TrainingPlan_OrderDetailsID_fkey",
                        column: x => x.OrderDetailsID,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailsID");
                });

            migrationBuilder.CreateTable(
                name: "MealPlanItem",
                columns: table => new
                {
                    MealPlanItemID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    MealPlanID = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekNumber = table.Column<short>(type: "smallint", nullable: true),
                    DayNumber = table.Column<short>(type: "smallint", nullable: true),
                    MealType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FoodDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Calories = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("MealPlanItem_pkey", x => x.MealPlanItemID);
                    table.ForeignKey(
                        name: "MealPlanItem_MealPlanID_fkey",
                        column: x => x.MealPlanID,
                        principalTable: "MealPlan",
                        principalColumn: "MealPlanID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    CartID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageID = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Carts_pkey", x => x.CartID);
                    table.ForeignKey(
                        name: "Carts_PackageID_fkey",
                        column: x => x.PackageID,
                        principalTable: "TrainingPackages",
                        principalColumn: "PackageID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "Carts_UserID_fkey",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingPlanExercise",
                columns: table => new
                {
                    TrainingPlanExerciseID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TrainingPlanID = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoTutorialID = table.Column<Guid>(type: "uuid", nullable: true),
                    WeekNumber = table.Column<short>(type: "smallint", nullable: true),
                    DayNumber = table.Column<short>(type: "smallint", nullable: true),
                    ExerciseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sets = table.Column<short>(type: "smallint", nullable: true),
                    Reps = table.Column<short>(type: "smallint", nullable: true),
                    DurationMinutes = table.Column<short>(type: "smallint", nullable: true),
                    RestSeconds = table.Column<short>(type: "smallint", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("TrainingPlanExercise_pkey", x => x.TrainingPlanExerciseID);
                    table.ForeignKey(
                        name: "TrainingPlanExercise_TrainingPlanID_fkey",
                        column: x => x.TrainingPlanID,
                        principalTable: "TrainingPlan",
                        principalColumn: "TrainingPlanID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "TrainingPlanExercise_VideoTutorialID_fkey",
                        column: x => x.VideoTutorialID,
                        principalTable: "VideoTutorial",
                        principalColumn: "VideoTutorialID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutCompletionLog",
                columns: table => new
                {
                    WorkoutCompletionLogID = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TraineeID = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingPlanExerciseID = table.Column<Guid>(type: "uuid", nullable: false),
                    LogDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("WorkoutCompletionLog_pkey", x => x.WorkoutCompletionLogID);
                    table.ForeignKey(
                        name: "WorkoutCompletionLog_TraineeID_fkey",
                        column: x => x.TraineeID,
                        principalTable: "Users",
                        principalColumn: "UserID");
                    table.ForeignKey(
                        name: "WorkoutCompletionLog_TrainingPlanExerciseID_fkey",
                        column: x => x.TrainingPlanExerciseID,
                        principalTable: "TrainingPlanExercise",
                        principalColumn: "TrainingPlanExerciseID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorAccountID",
                table: "AuditLogs",
                column: "ActorAccountID");

            migrationBuilder.CreateIndex(
                name: "IX_BodyMeasurementDetail_BodyMetricLogID",
                table: "BodyMeasurementDetail",
                column: "BodyMetricLogID");

            migrationBuilder.CreateIndex(
                name: "IX_BodyMetricLog_TraineeID",
                table: "BodyMetricLog",
                column: "TraineeID");

            migrationBuilder.CreateIndex(
                name: "Carts_UserID_PackageID_key",
                table: "Carts",
                columns: new[] { "UserID", "PackageID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_PackageID",
                table: "Carts",
                column: "PackageID");

            migrationBuilder.CreateIndex(
                name: "IX_CoachBankAccounts_CoachID",
                table: "CoachBankAccounts",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_CoachLocations_LocationID",
                table: "CoachLocations",
                column: "LocationID");

            migrationBuilder.CreateIndex(
                name: "IX_CoachProfiles_ApprovedBy",
                table: "CoachProfiles",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CoachSports_SportID",
                table: "CoachSports",
                column: "SportID");

            migrationBuilder.CreateIndex(
                name: "CoachUpgrades_OrderID_key",
                table: "CoachUpgrades",
                column: "OrderID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachUpgrades_CoachID",
                table: "CoachUpgrades",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_CoachUpgrades_PriceID",
                table: "CoachUpgrades",
                column: "PriceID");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorID",
                table: "Comments",
                column: "AuthorID");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentID",
                table: "Comments",
                column: "ParentCommentID");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostID",
                table: "Comments",
                column: "PostID");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CreatorID",
                table: "Conversations",
                column: "CreatorID");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_AddresseeID",
                table: "Friendships",
                column: "AddresseeID");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlan_CoachID",
                table: "MealPlan",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlan_OrderDetailsID",
                table: "MealPlan",
                column: "OrderDetailsID");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanItem_MealPlanID",
                table: "MealPlanItem",
                column: "MealPlanID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationID",
                table: "Messages",
                column: "ConversationID");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderID",
                table: "Messages",
                column: "SenderID");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserID",
                table: "Notifications",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_OrderID",
                table: "OrderDetails",
                column: "OrderID");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CoachID",
                table: "Orders",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TraineeID",
                table: "Orders",
                column: "TraineeID");

            migrationBuilder.CreateIndex(
                name: "IX_OTPLogs_UserID",
                table: "OTPLogs",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_UserID",
                table: "Participants",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentGatewayConfigs_UpdatedBy",
                table: "PaymentGatewayConfigs",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderID",
                table: "Payments",
                column: "OrderID");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutItems_OrderID",
                table: "PayoutItems",
                column: "OrderID");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutItems_PayoutID",
                table: "PayoutItems",
                column: "PayoutID");

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_ProcessedBy",
                table: "Payouts",
                column: "ProcessedBy");

            migrationBuilder.CreateIndex(
                name: "Payouts_CoachID_PayoutMonth_PayoutYear_key",
                table: "Payouts",
                columns: new[] { "CoachID", "PayoutMonth", "PayoutYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PostInteractions_UserID",
                table: "PostInteractions",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PostMedia_PostID",
                table: "PostMedia",
                column: "PostID");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorID",
                table: "Posts",
                column: "AuthorID");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_LocationID",
                table: "Posts",
                column: "LocationID");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_SportID",
                table: "Posts",
                column: "SportID");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserID",
                table: "RefreshTokens",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "RefreshTokens_TokenHash_key",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedPostID",
                table: "Reports",
                column: "ReportedPostID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedUserID",
                table: "Reports",
                column: "ReportedUserID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterID",
                table: "Reports",
                column: "ReporterID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ResolvedBy",
                table: "Reports",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CoachID",
                table: "Reviews",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TraineeID",
                table: "Reviews",
                column: "TraineeID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPackages_CoachID",
                table: "TrainingPackages",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPackages_OrderDetailsID",
                table: "TrainingPackages",
                column: "OrderDetailsID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlan_CoachID",
                table: "TrainingPlan",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlan_OrderDetailsID",
                table: "TrainingPlan",
                column: "OrderDetailsID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlanExercise_TrainingPlanID",
                table: "TrainingPlanExercise",
                column: "TrainingPlanID");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingPlanExercise_VideoTutorialID",
                table: "TrainingPlanExercise",
                column: "VideoTutorialID");

            migrationBuilder.CreateIndex(
                name: "IX_userBlocks_BlockedID",
                table: "userBlocks",
                column: "BlockedID");

            migrationBuilder.CreateIndex(
                name: "userBlocks_BlockerID_BlockedID_key",
                table: "userBlocks",
                columns: new[] { "BlockerID", "BlockedID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteSports_SportID",
                table: "UserFavoriteSports",
                column: "SportID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LockedBy",
                table: "Users",
                column: "LockedBy");

            migrationBuilder.CreateIndex(
                name: "Users_Email_key",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Users_GoogleProviderID_key",
                table: "Users",
                column: "GoogleProviderID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Users_PhoneNumber_key",
                table: "Users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoTutorial_CoachID",
                table: "VideoTutorial",
                column: "CoachID");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutCompletionLog_TraineeID",
                table: "WorkoutCompletionLog",
                column: "TraineeID");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutCompletionLog_TrainingPlanExerciseID",
                table: "WorkoutCompletionLog",
                column: "TrainingPlanExerciseID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BodyMeasurementDetail");

            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropTable(
                name: "CoachBankAccounts");

            migrationBuilder.DropTable(
                name: "CoachLocations");

            migrationBuilder.DropTable(
                name: "CoachSports");

            migrationBuilder.DropTable(
                name: "CoachUpgrades");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "MealPlanItem");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OTPLogs");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "PaymentGatewayConfigs");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PayoutItems");

            migrationBuilder.DropTable(
                name: "PostInteractions");

            migrationBuilder.DropTable(
                name: "PostMedia");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "userBlocks");

            migrationBuilder.DropTable(
                name: "UserFavoriteSports");

            migrationBuilder.DropTable(
                name: "VideoCalls");

            migrationBuilder.DropTable(
                name: "WorkoutCompletionLog");

            migrationBuilder.DropTable(
                name: "BodyMetricLog");

            migrationBuilder.DropTable(
                name: "TrainingPackages");

            migrationBuilder.DropTable(
                name: "Price");

            migrationBuilder.DropTable(
                name: "MealPlan");

            migrationBuilder.DropTable(
                name: "Payouts");

            migrationBuilder.DropTable(
                name: "Posts");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "TrainingPlanExercise");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Sports");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "TrainingPlan");

            migrationBuilder.DropTable(
                name: "VideoTutorial");

            migrationBuilder.DropTable(
                name: "OrderDetails");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "CoachProfiles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
