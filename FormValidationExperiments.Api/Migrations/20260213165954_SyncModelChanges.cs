using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FormValidationExperiments.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AbsentWithoutLeaveDate1",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AbsentWithoutLeaveDate2",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AbsentWithoutLeaveTime1",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AbsentWithoutLeaveTime2",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AppointingAuthorityDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AppointingAuthorityNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AppointingAuthoritySignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovingAuthorityDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovingAuthorityNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovingAuthoritySignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ApprovingFinding",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ApprovingReferForFormal",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BoardFinding",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BoardReferForFormal",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CommanderDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommanderFromLine",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommanderNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommanderSignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommanderToLine",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FromLine",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsAFROTC",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeployedLocation",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGMU",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMTF",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRMU",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUSAFA",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LegalReviewDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalReviewText",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalReviewerNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalReviewerSignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LodBoardChairDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LodBoardChairNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LodBoardChairSignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MedicalReviewDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MedicalReviewText",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MedicalReviewerNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MedicalReviewerSignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "MemberOrdersEndDate",
                table: "Cases",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemberOrdersEndTime",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MemberOrdersStartTime",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "MilitaryPoliceReportsReviewed",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OsiReportsReviewed",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProviderDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderSignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SjaConcurs",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SjaDate",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SjaNameRank",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "WasAbsentWithLeave",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WasAbsentWithoutLeave",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WasOnDuty",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WasOnIDT",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "WasPresentForDuty",
                table: "Cases",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WingCcSignature",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WitnessNameAddress1",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WitnessNameAddress2",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WitnessNameAddress3",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WitnessNameAddress4",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WitnessNameAddress5",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsentWithoutLeaveDate1",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AbsentWithoutLeaveDate2",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AbsentWithoutLeaveTime1",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AbsentWithoutLeaveTime2",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AppointingAuthorityDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AppointingAuthorityNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "AppointingAuthoritySignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ApprovingAuthorityDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ApprovingAuthorityNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ApprovingAuthoritySignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ApprovingFinding",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ApprovingReferForFormal",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "BoardFinding",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "BoardReferForFormal",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CommanderDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CommanderFromLine",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CommanderNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CommanderSignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CommanderToLine",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "FromLine",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "IsAFROTC",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "IsDeployedLocation",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "IsGMU",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "IsMTF",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "IsRMU",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "IsUSAFA",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LegalReviewDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LegalReviewText",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LegalReviewerNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LegalReviewerSignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LodBoardChairDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LodBoardChairNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "LodBoardChairSignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MedicalReviewDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MedicalReviewText",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MedicalReviewerNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MedicalReviewerSignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MemberOrdersEndDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MemberOrdersEndTime",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MemberOrdersStartTime",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "MilitaryPoliceReportsReviewed",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "OsiReportsReviewed",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ProviderDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ProviderNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "ProviderSignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SjaConcurs",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SjaDate",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SjaNameRank",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WasAbsentWithLeave",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WasAbsentWithoutLeave",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WasOnDuty",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WasOnIDT",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WasPresentForDuty",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WingCcSignature",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WitnessNameAddress1",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WitnessNameAddress2",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WitnessNameAddress3",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WitnessNameAddress4",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "WitnessNameAddress5",
                table: "Cases");
        }
    }
}
