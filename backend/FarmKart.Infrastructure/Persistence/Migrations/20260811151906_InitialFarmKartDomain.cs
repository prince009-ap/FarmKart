using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmKart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFarmKartDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressInfo_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_Pincode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    AddressInfo_Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    AddressInfo_Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FarmerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FarmSize = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FarmLocation = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressInfo_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_Pincode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    AddressInfo_Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    AddressInfo_Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FarmerProfiles", x => x.Id);
                    table.CheckConstraint("CK_FarmerProfile_FarmSize_NonNegative", "[FarmSize] IS NULL OR [FarmSize] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "MachineryCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NotificationType = table.Column<int>(type: "int", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RevieweeUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RelatedEntityType = table.Column<int>(type: "int", nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Review_Rating_Range", "[Rating] >= 1 AND [Rating] <= 5");
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    ExpectedDailyWage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    AvailableFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    AvailabilityNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressInfo_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_Pincode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    AddressInfo_Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    AddressInfo_Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerProfiles", x => x.Id);
                    table.CheckConstraint("CK_WorkerProfile_ExpectedDailyWage_NonNegative", "[ExpectedDailyWage] >= 0");
                    table.CheckConstraint("CK_WorkerProfile_ExperienceYears_NonNegative", "[ExperienceYears] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ConversationParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProfileType = table.Column<int>(type: "int", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationParticipants_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MessageText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrderDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderStatus = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Order_TotalAmount_NonNegative", "[TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Crops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Variety = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Area = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SowingDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedHarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActualHarvestDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<int>(type: "int", nullable: false),
                    QualityGrade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crops", x => x.Id);
                    table.CheckConstraint("CK_Crop_ActualHarvest_After_Sowing", "[ActualHarvestDate] IS NULL OR [SowingDate] IS NULL OR [ActualHarvestDate] >= [SowingDate]");
                    table.CheckConstraint("CK_Crop_Area_NonNegative", "[Area] >= 0");
                    table.CheckConstraint("CK_Crop_ExpectedHarvest_After_Sowing", "[ExpectedHarvestDate] IS NULL OR [SowingDate] IS NULL OR [ExpectedHarvestDate] >= [SowingDate]");
                    table.CheckConstraint("CK_Crop_Quantity_NonNegative", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_Crops_FarmerProfiles_FarmerProfileId",
                        column: x => x.FarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    WorkCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CropType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkersRequired = table.Column<int>(type: "int", nullable: false),
                    RequiredExperience = table.Column<int>(type: "int", nullable: false),
                    WagePerDay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WorkingHours = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FarmLocation = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    FarmSize = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FoodProvided = table.Column<bool>(type: "bit", nullable: false),
                    AccommodationProvided = table.Column<bool>(type: "bit", nullable: false),
                    IsUrgent = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.CheckConstraint("CK_Job_EndDate_After_StartDate", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_Job_FarmSize_NonNegative", "[FarmSize] IS NULL OR [FarmSize] >= 0");
                    table.CheckConstraint("CK_Job_RequiredExperience_NonNegative", "[RequiredExperience] >= 0");
                    table.CheckConstraint("CK_Job_WagePerDay_NonNegative", "[WagePerDay] >= 0");
                    table.CheckConstraint("CK_Job_WorkersRequired_Positive", "[WorkersRequired] > 0");
                    table.ForeignKey(
                        name: "FK_Jobs_FarmerProfiles_FarmerProfileId",
                        column: x => x.FarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Machinery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerFarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ManufacturingYear = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DailyRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WeeklyRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDriverIncluded = table.Column<bool>(type: "bit", nullable: false),
                    IsFuelIncluded = table.Column<bool>(type: "bit", nullable: false),
                    AvailabilityStatus = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machinery", x => x.Id);
                    table.CheckConstraint("CK_Machinery_ManufacturingYear_Range", "[ManufacturingYear] IS NULL OR ([ManufacturingYear] >= 1900 AND [ManufacturingYear] <= 2100)");
                    table.CheckConstraint("CK_Machinery_RentValues_NonNegative", "[DailyRent] >= 0 AND ([WeeklyRent] IS NULL OR [WeeklyRent] >= 0) AND ([MonthlyRent] IS NULL OR [MonthlyRent] >= 0) AND [SecurityDeposit] >= 0");
                    table.ForeignKey(
                        name: "FK_Machinery_FarmerProfiles_OwnerFarmerProfileId",
                        column: x => x.OwnerFarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Machinery_MachineryCategories_MachineryCategoryId",
                        column: x => x.MachineryCategoryId,
                        principalTable: "MachineryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProficiencyScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    ExperienceYears = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerSkills", x => x.Id);
                    table.CheckConstraint("CK_WorkerSkill_ExperienceYears_NonNegative", "[ExperienceYears] IS NULL OR [ExperienceYears] >= 0");
                    table.CheckConstraint("CK_WorkerSkill_ProficiencyScore_Range", "[ProficiencyScore] IS NULL OR ([ProficiencyScore] >= 0 AND [ProficiencyScore] <= 100)");
                    table.ForeignKey(
                        name: "FK_WorkerSkills_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerSkills_WorkerProfiles_WorkerProfileId",
                        column: x => x.WorkerProfileId,
                        principalTable: "WorkerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryType = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AddressInfo_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AddressInfo_Pincode = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    AddressInfo_Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    AddressInfo_Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    ScheduledDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliveries_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payment_Amount_NonNegative", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CropImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CropImages_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CropListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityForSale = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<int>(type: "int", nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ListingType = table.Column<int>(type: "int", nullable: false),
                    ListingStatus = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropListings", x => x.Id);
                    table.CheckConstraint("CK_CropListing_PricePerUnit_NonNegative", "[PricePerUnit] >= 0");
                    table.CheckConstraint("CK_CropListing_QuantityForSale_NonNegative", "[QuantityForSale] >= 0");
                    table.ForeignKey(
                        name: "FK_CropListings_Crops_CropId",
                        column: x => x.CropId,
                        principalTable: "Crops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CropListings_FarmerProfiles_FarmerProfileId",
                        column: x => x.FarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_WorkerProfiles_WorkerProfileId",
                        column: x => x.WorkerProfileId,
                        principalTable: "WorkerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineryImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineryImages_Machinery_MachineryId",
                        column: x => x.MachineryId,
                        principalTable: "Machinery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MachineryRentalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RenterFarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerFarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryRentalRequests", x => x.Id);
                    table.CheckConstraint("CK_MachineryRentalRequest_Amounts_NonNegative", "[RequestedAmount] >= 0 AND [SecurityDeposit] >= 0");
                    table.CheckConstraint("CK_MachineryRentalRequest_DateRange", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_MachineryRentalRequests_FarmerProfiles_OwnerFarmerProfileId",
                        column: x => x.OwnerFarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRentalRequests_FarmerProfiles_RenterFarmerProfileId",
                        column: x => x.RenterFarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRentalRequests_Machinery_MachineryId",
                        column: x => x.MachineryId,
                        principalTable: "Machinery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Auctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentHighestBid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumBidIncrement = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuctionStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auctions", x => x.Id);
                    table.CheckConstraint("CK_Auction_EndTime_After_StartTime", "[EndTimeUtc] > [StartTimeUtc]");
                    table.CheckConstraint("CK_Auction_Prices_Valid", "[StartingPrice] >= 0 AND [CurrentHighestBid] >= 0 AND [MinimumBidIncrement] > 0");
                    table.ForeignKey(
                        name: "FK_Auctions_CropListings_CropListingId",
                        column: x => x.CropListingId,
                        principalTable: "CropListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auctions_FarmerProfiles_FarmerProfileId",
                        column: x => x.FarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CropListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItem_Values_NonNegative", "[Quantity] >= 0 AND [UnitPrice] >= 0 AND [TotalPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_CropListings_CropListingId",
                        column: x => x.CropListingId,
                        principalTable: "CropListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerAssignments", x => x.Id);
                    table.CheckConstraint("CK_WorkerAssignment_EndDate_After_StartDate", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_WorkerAssignments_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerAssignments_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerAssignments_WorkerProfiles_WorkerProfileId",
                        column: x => x.WorkerProfileId,
                        principalTable: "WorkerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineryRentals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerFarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RenterFarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryRentalRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SecurityDeposit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RentalStatus = table.Column<int>(type: "int", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryRentals", x => x.Id);
                    table.CheckConstraint("CK_MachineryRental_Amounts_NonNegative", "[TotalAmount] >= 0 AND [SecurityDeposit] >= 0");
                    table.CheckConstraint("CK_MachineryRental_DateRange", "[EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_MachineryRentals_FarmerProfiles_OwnerFarmerProfileId",
                        column: x => x.OwnerFarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRentals_FarmerProfiles_RenterFarmerProfileId",
                        column: x => x.RenterFarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRentals_MachineryRentalRequests_MachineryRentalRequestId",
                        column: x => x.MachineryRentalRequestId,
                        principalTable: "MachineryRentalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineryRentals_Machinery_MachineryId",
                        column: x => x.MachineryId,
                        principalTable: "Machinery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BidTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BidStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.CheckConstraint("CK_Bid_Amount_Positive", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Bids_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bids_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    CheckOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    TotalHours = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.CheckConstraint("CK_Attendance_TotalHours_Range", "[TotalHours] >= 0 AND [TotalHours] <= 24");
                    table.ForeignKey(
                        name: "FK_Attendances_WorkerAssignments_WorkerAssignmentId",
                        column: x => x.WorkerAssignmentId,
                        principalTable: "WorkerAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FarmerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    TransactionReference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerPayments", x => x.Id);
                    table.CheckConstraint("CK_WorkerPayment_Amount_NonNegative", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_WorkerPayments_FarmerProfiles_FarmerProfileId",
                        column: x => x.FarmerProfileId,
                        principalTable: "FarmerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerPayments_WorkerAssignments_WorkerAssignmentId",
                        column: x => x.WorkerAssignmentId,
                        principalTable: "WorkerAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkerPayments_WorkerProfiles_WorkerProfileId",
                        column: x => x.WorkerProfileId,
                        principalTable: "WorkerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineryDamageReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryRentalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DamageAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryDamageReports", x => x.Id);
                    table.CheckConstraint("CK_MachineryDamageReport_DamageAmount_NonNegative", "[DamageAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_MachineryDamageReports_MachineryRentals_MachineryRentalId",
                        column: x => x.MachineryRentalId,
                        principalTable: "MachineryRentals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionWinners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WinningBidId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SelectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionWinners", x => x.Id);
                    table.CheckConstraint("CK_AuctionWinner_FinalAmount_NonNegative", "[FinalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_AuctionWinners_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionWinners_Bids_WinningBidId",
                        column: x => x.WinningBidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionWinners_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineryDamageReportImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineryDamageReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineryDamageReportImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineryDamageReportImages_MachineryDamageReports_MachineryDamageReportId",
                        column: x => x.MachineryDamageReportId,
                        principalTable: "MachineryDamageReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_WorkerAssignmentId_Date",
                table: "Attendances",
                columns: new[] { "WorkerAssignmentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_CropListingId",
                table: "Auctions",
                column: "CropListingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_FarmerProfileId",
                table: "Auctions",
                column: "FarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionWinners_AuctionId",
                table: "AuctionWinners",
                column: "AuctionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionWinners_CustomerProfileId",
                table: "AuctionWinners",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionWinners_WinningBidId",
                table: "AuctionWinners",
                column: "WinningBidId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bids_AuctionId",
                table: "Bids",
                column: "AuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_CustomerProfileId",
                table: "Bids",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ConversationId_UserId",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CropImages_CropId_DisplayOrder",
                table: "CropImages",
                columns: new[] { "CropId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CropListings_CropId",
                table: "CropListings",
                column: "CropId");

            migrationBuilder.CreateIndex(
                name: "IX_CropListings_FarmerProfileId",
                table: "CropListings",
                column: "FarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CropListings_ListingType",
                table: "CropListings",
                column: "ListingType");

            migrationBuilder.CreateIndex(
                name: "IX_Crops_FarmerProfileId",
                table: "Crops",
                column: "FarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_UserId",
                table: "CustomerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_OrderId",
                table: "Deliveries",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FarmerProfiles_UserId",
                table: "FarmerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobId_WorkerProfileId",
                table: "JobApplications",
                columns: new[] { "JobId", "WorkerProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_WorkerProfileId",
                table: "JobApplications",
                column: "WorkerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_FarmerProfileId",
                table: "Jobs",
                column: "FarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status",
                table: "Jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_MachineryCategoryId",
                table: "Machinery",
                column: "MachineryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Machinery_OwnerFarmerProfileId",
                table: "Machinery",
                column: "OwnerFarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryCategories_Name",
                table: "MachineryCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryDamageReportImages_MachineryDamageReportId_DisplayOrder",
                table: "MachineryDamageReportImages",
                columns: new[] { "MachineryDamageReportId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryDamageReports_MachineryRentalId",
                table: "MachineryDamageReports",
                column: "MachineryRentalId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryImages_MachineryId_DisplayOrder",
                table: "MachineryImages",
                columns: new[] { "MachineryId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentalRequests_MachineryId",
                table: "MachineryRentalRequests",
                column: "MachineryId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentalRequests_OwnerFarmerProfileId",
                table: "MachineryRentalRequests",
                column: "OwnerFarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentalRequests_RenterFarmerProfileId",
                table: "MachineryRentalRequests",
                column: "RenterFarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_MachineryId",
                table: "MachineryRentals",
                column: "MachineryId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_MachineryRentalRequestId",
                table: "MachineryRentals",
                column: "MachineryRentalRequestId",
                unique: true,
                filter: "[MachineryRentalRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_OwnerFarmerProfileId",
                table: "MachineryRentals",
                column: "OwnerFarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineryRentals_RenterFarmerProfileId",
                table: "MachineryRentals",
                column: "RenterFarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_IsRead",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_CropListingId",
                table: "OrderItems",
                column: "CropListingId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerProfileId",
                table: "Orders",
                column: "CustomerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RevieweeUserId",
                table: "Reviews",
                column: "RevieweeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerUserId",
                table: "Reviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_JobApplicationId",
                table: "WorkerAssignments",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_JobId",
                table: "WorkerAssignments",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerAssignments_WorkerProfileId",
                table: "WorkerAssignments",
                column: "WorkerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerPayments_FarmerProfileId",
                table: "WorkerPayments",
                column: "FarmerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerPayments_WorkerAssignmentId",
                table: "WorkerPayments",
                column: "WorkerAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerPayments_WorkerProfileId",
                table: "WorkerPayments",
                column: "WorkerProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerProfiles_UserId",
                table: "WorkerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerSkills_SkillId",
                table: "WorkerSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerSkills_WorkerProfileId_SkillId",
                table: "WorkerSkills",
                columns: new[] { "WorkerProfileId", "SkillId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "AuctionWinners");

            migrationBuilder.DropTable(
                name: "ConversationParticipants");

            migrationBuilder.DropTable(
                name: "CropImages");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "MachineryDamageReportImages");

            migrationBuilder.DropTable(
                name: "MachineryImages");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "WorkerPayments");

            migrationBuilder.DropTable(
                name: "WorkerSkills");

            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "MachineryDamageReports");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "WorkerAssignments");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Auctions");

            migrationBuilder.DropTable(
                name: "MachineryRentals");

            migrationBuilder.DropTable(
                name: "CustomerProfiles");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "CropListings");

            migrationBuilder.DropTable(
                name: "MachineryRentalRequests");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "WorkerProfiles");

            migrationBuilder.DropTable(
                name: "Crops");

            migrationBuilder.DropTable(
                name: "Machinery");

            migrationBuilder.DropTable(
                name: "FarmerProfiles");

            migrationBuilder.DropTable(
                name: "MachineryCategories");
        }
    }
}
