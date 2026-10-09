using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Guard.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261009090000_AddLoginFailurePolicy")]
public partial class AddLoginFailurePolicy : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<int>(name: "MaxFailedLoginAttempts", table: "AccountPolicies", type: "integer", nullable: false, defaultValue: 5);
    migrationBuilder.AddColumn<int>(name: "LoginLockoutMinutes", table: "AccountPolicies", type: "integer", nullable: false, defaultValue: 15);
    migrationBuilder.AddCheckConstraint(name: "CK_AccountPolicies_LoginLockout", table: "AccountPolicies",
      sql: "\"MaxFailedLoginAttempts\" BETWEEN 1 AND 100 AND \"LoginLockoutMinutes\" BETWEEN 1 AND 10080");
  }
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropCheckConstraint(name: "CK_AccountPolicies_LoginLockout", table: "AccountPolicies");
    migrationBuilder.DropColumn(name: "MaxFailedLoginAttempts", table: "AccountPolicies");
    migrationBuilder.DropColumn(name: "LoginLockoutMinutes", table: "AccountPolicies");
  }
}
