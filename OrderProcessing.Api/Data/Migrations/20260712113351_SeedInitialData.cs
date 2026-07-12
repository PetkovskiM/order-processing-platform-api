using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrderProcessing.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "CreatedAtUtc", "Email", "FirstName", "LastName", "PhoneNumber" },
                values: new object[,]
                {
                    { 1001, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), "john.smith@example.com", "John", "Smith", "+38970111222" },
                    { 1002, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), "ana.petrovska@example.com", "Ana", "Petrovska", "+38970222333" },
                    { 1003, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), "mark.johnson@example.com", "Mark", "Johnson", null }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CreatedAtUtc", "Description", "Name", "Price", "Sku", "StockQuantity", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1001, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Reliable laptop for office and development work.", "Business Laptop", 899.99m, "LAPTOP-001", 15, null },
                    { 1002, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Ergonomic wireless mouse.", "Wireless Mouse", 24.99m, "MOUSE-001", 100, null },
                    { 1003, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), "Mechanical keyboard with backlight.", "Mechanical Keyboard", 79.99m, "KEYBOARD-001", 50, null },
                    { 1004, new DateTime(2026, 7, 12, 0, 0, 0, 0, DateTimeKind.Utc), "27 inch full HD monitor.", "27 Inch Monitor", 199.99m, "MONITOR-001", 25, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "Customers",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: 1004);
        }
    }
}
