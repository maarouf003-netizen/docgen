using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocGenerator.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameAssetsAndAddKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // إعادة تسمية الجدولين مع الحفاظ على البيانات (تعويض إعادة الإنشاء الافتراضية من EF)
            migrationBuilder.RenameTable(
                name: "RealEstates",
                newName: "Assets");

            migrationBuilder.RenameTable(
                name: "RealEstateOwners",
                newName: "AssetOwners");

            // عمود المفتاح الأجنبي في جدول الملاك
            migrationBuilder.RenameColumn(
                name: "RealEstateId",
                table: "AssetOwners",
                newName: "AssetId");

            // قيد المفتاح الأجنبي والمؤشر الخاص بجدول الملاك
            migrationBuilder.DropForeignKey(
                name: "FK_RealEstateOwners_RealEstates_RealEstateId",
                table: "AssetOwners");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetOwners_Assets_AssetId",
                table: "AssetOwners",
                column: "AssetId",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropIndex(
                name: "IX_RealEstateOwners_RealEstateId",
                table: "AssetOwners");

            migrationBuilder.CreateIndex(
                name: "IX_AssetOwners_AssetId",
                table: "AssetOwners",
                column: "AssetId");

            // قيد المفتاح الأجنبي والمؤشر الخاص بجدول الأصول
            migrationBuilder.DropForeignKey(
                name: "FK_RealEstates_Documents_DocumentId",
                table: "Assets");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Documents_DocumentId",
                table: "Assets",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropIndex(
                name: "IX_RealEstates_DocumentId",
                table: "Assets");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DocumentId",
                table: "Assets",
                column: "DocumentId");

            // عمود نوع الأصل: جميع الصفوف القائمة عقارات (التحويل 1:1 حافظ على المعرّفات)
            migrationBuilder.AddColumn<string>(
                name: "AssetKind",
                table: "Assets",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "عقار");

            // أعمدة الأنواع الجديدة (اختيارية حسب نوع الأصل)
            migrationBuilder.AddColumn<string>(
                name: "VehicleType",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleClass",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlateNumber",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleGovernorate",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisterNumber",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDate",
                table: "Assets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopGovernorate",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopDescription",
                table: "Assets",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShopLocation",
                table: "Assets",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicEntity",
                table: "Assets",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "Assets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseDate",
                table: "Assets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseIssuer",
                table: "Assets",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Assets",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            // عمود معرّفات الأموال المباعة بالمزاد
            migrationBuilder.RenameColumn(
                name: "SoldEstateIds",
                table: "Documents",
                newName: "SoldAssetIds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SoldAssetIds",
                table: "Documents",
                newName: "SoldEstateIds");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LicenseIssuer",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LicenseDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PublicEntity",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ShopLocation",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ShopDescription",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ShopGovernorate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RegistrationDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RegisterNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VehicleGovernorate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PlateNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VehicleClass",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AssetKind",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_DocumentId",
                table: "Assets");

            migrationBuilder.CreateIndex(
                name: "IX_RealEstates_DocumentId",
                table: "Assets",
                column: "DocumentId");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Documents_DocumentId",
                table: "Assets");

            migrationBuilder.AddForeignKey(
                name: "FK_RealEstates_Documents_DocumentId",
                table: "Assets",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropIndex(
                name: "IX_AssetOwners_AssetId",
                table: "AssetOwners");

            migrationBuilder.CreateIndex(
                name: "IX_RealEstateOwners_RealEstateId",
                table: "AssetOwners",
                column: "AssetId");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetOwners_Assets_AssetId",
                table: "AssetOwners");

            migrationBuilder.AddForeignKey(
                name: "FK_RealEstateOwners_RealEstates_RealEstateId",
                table: "AssetOwners",
                column: "AssetId",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.RenameColumn(
                name: "AssetId",
                table: "AssetOwners",
                newName: "RealEstateId");

            migrationBuilder.RenameTable(
                name: "AssetOwners",
                newName: "RealEstateOwners");

            migrationBuilder.RenameTable(
                name: "Assets",
                newName: "RealEstates");
        }
    }
}
