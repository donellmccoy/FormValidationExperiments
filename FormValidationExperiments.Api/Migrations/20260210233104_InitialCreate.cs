using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormValidationExperiments.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INCAPDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    CivilianIncomeLoss = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Documentation = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INCAPDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MEDCONDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtensionDays = table.Column<int>(type: "int", nullable: false),
                    UsesInterimLOD = table.Column<bool>(type: "bit", nullable: false),
                    TreatmentPlan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutOfLocalAreaLeaveApproved = table.Column<bool>(type: "bit", nullable: false),
                    PhysicianMemo = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEDCONDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProcessType = table.Column<int>(type: "int", nullable: false),
                    Component = table.Column<int>(type: "int", nullable: false),
                    MemberName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemberRank = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncidentType = table.Column<int>(type: "int", nullable: false),
                    IncidentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IncidentDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncidentDutyStatus = table.Column<int>(type: "int", nullable: false),
                    IsMilitaryFacility = table.Column<bool>(type: "bit", nullable: true),
                    TreatmentFacilityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TreatmentDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClinicalDiagnosis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MedicalFindings = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WasUnderInfluence = table.Column<bool>(type: "bit", nullable: true),
                    SubstanceType = table.Column<int>(type: "int", nullable: true),
                    WasMentallyResponsible = table.Column<bool>(type: "bit", nullable: true),
                    PsychiatricEvalCompleted = table.Column<bool>(type: "bit", nullable: true),
                    PsychiatricEvalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PsychiatricEvalResults = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRelevantConditions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherTestsDone = table.Column<bool>(type: "bit", nullable: true),
                    OtherTestDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtherTestResults = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsServiceAggravated = table.Column<bool>(type: "bit", nullable: true),
                    IsPotentiallyUnfitting = table.Column<bool>(type: "bit", nullable: true),
                    IsAtDeployedLocation = table.Column<bool>(type: "bit", nullable: true),
                    RequiresArcBoard = table.Column<bool>(type: "bit", nullable: true),
                    MedicalRecommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MemberStatementReviewed = table.Column<bool>(type: "bit", nullable: false),
                    MedicalRecordsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    WitnessStatementsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    PoliceReportsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    CommanderReportReviewed = table.Column<bool>(type: "bit", nullable: false),
                    OtherSourcesReviewed = table.Column<bool>(type: "bit", nullable: false),
                    OtherSourcesDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MisconductExplanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InitiationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalTimelineDays = table.Column<int>(type: "int", nullable: false),
                    IsInterimLOD = table.Column<bool>(type: "bit", nullable: false),
                    InterimLODExpiration = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalFinding = table.Column<int>(type: "int", nullable: false),
                    ProximateCause = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPriorServiceCondition = table.Column<bool>(type: "bit", nullable: false),
                    PSCDocumentation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EightYearRuleApplies = table.Column<bool>(type: "bit", nullable: false),
                    YearsOfService = table.Column<int>(type: "int", nullable: false),
                    IsSexualAssaultCase = table.Column<bool>(type: "bit", nullable: false),
                    RestrictedReporting = table.Column<bool>(type: "bit", nullable: false),
                    SARCCoordination = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WitnessStatements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToxicologyReport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MEDCONId = table.Column<int>(type: "int", nullable: false),
                    INCAPId = table.Column<int>(type: "int", nullable: false),
                    MemberChoseMEDCON = table.Column<bool>(type: "bit", nullable: false),
                    IsAudited = table.Column<bool>(type: "bit", nullable: false),
                    AuditComments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointOfContact = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cases_INCAPDetails_INCAPId",
                        column: x => x.INCAPId,
                        principalTable: "INCAPDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Cases_MEDCONDetails_MEDCONId",
                        column: x => x.MEDCONId,
                        principalTable: "MEDCONDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Authorities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Recommendation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Authorities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Authorities_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    AppealDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Appellant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewEvidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalFinding = table.Column<int>(type: "int", nullable: false),
                    AppealOutcome = table.Column<int>(type: "int", nullable: false),
                    AppellateAuthorityId = table.Column<int>(type: "int", nullable: true),
                    ResolutionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appeals_Authorities_AppellateAuthorityId",
                        column: x => x.AppellateAuthorityId,
                        principalTable: "Authorities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Appeals_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimelineSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineOfDutyCaseId = table.Column<int>(type: "int", nullable: false),
                    StepDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimelineDays = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    ResponsibleAuthorityId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimelineSteps_Authorities_ResponsibleAuthorityId",
                        column: x => x.ResponsibleAuthorityId,
                        principalTable: "Authorities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TimelineSteps_Cases_LineOfDutyCaseId",
                        column: x => x.LineOfDutyCaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_AppellateAuthorityId",
                table: "Appeals",
                column: "AppellateAuthorityId");

            migrationBuilder.CreateIndex(
                name: "IX_Appeals_LineOfDutyCaseId",
                table: "Appeals",
                column: "LineOfDutyCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Authorities_LineOfDutyCaseId",
                table: "Authorities",
                column: "LineOfDutyCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CaseId",
                table: "Cases",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_INCAPId",
                table: "Cases",
                column: "INCAPId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_MEDCONId",
                table: "Cases",
                column: "MEDCONId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LineOfDutyCaseId",
                table: "Documents",
                column: "LineOfDutyCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSteps_LineOfDutyCaseId",
                table: "TimelineSteps",
                column: "LineOfDutyCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSteps_ResponsibleAuthorityId",
                table: "TimelineSteps",
                column: "ResponsibleAuthorityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appeals");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "TimelineSteps");

            migrationBuilder.DropTable(
                name: "Authorities");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "INCAPDetails");

            migrationBuilder.DropTable(
                name: "MEDCONDetails");
        }
    }
}
