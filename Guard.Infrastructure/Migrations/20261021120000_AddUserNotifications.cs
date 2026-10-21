using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Guard.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261021120000_AddUserNotifications")]
public sealed class AddUserNotifications : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
    CREATE SEQUENCE "NotificationRevision" AS bigint START WITH 1 INCREMENT BY 1 NO CYCLE;
    ALTER TABLE "SynchronizationRuns" ADD COLUMN "NotificationSourceKey" text;
    CREATE TABLE "NotificationEvents" (
      "Id" uuid PRIMARY KEY, "EventKey" varchar(200) NOT NULL, "UserId" varchar(450),
      "Kind" integer NOT NULL, "Severity" integer NOT NULL, "Title" varchar(200) NOT NULL,
      "Message" varchar(2000) NOT NULL, "Target" integer NOT NULL, "TargetId" uuid,
      "SourceKey" varchar(200), "Recovery" boolean NOT NULL, "UpdatedSince" date,
      "OccurredAtUtc" timestamptz NOT NULL, "ProcessedAtUtc" timestamptz
    );
    CREATE UNIQUE INDEX "IX_NotificationEvents_EventKey" ON "NotificationEvents"("EventKey");
    CREATE INDEX "IX_NotificationEvents_OccurredAtUtc_Id" ON "NotificationEvents"("OccurredAtUtc","Id") WHERE "ProcessedAtUtc" IS NULL;
    CREATE TABLE "NotificationIncidents" (
      "Id" uuid PRIMARY KEY, "SourceKey" varchar(200) NOT NULL, "Kind" integer NOT NULL,
      "Active" boolean NOT NULL, "FirstSeenUtc" timestamptz NOT NULL, "LastSeenUtc" timestamptz NOT NULL,
      "ResolvedAtUtc" timestamptz, "Occurrences" integer NOT NULL, "RequiredSince" date,
      "FullSnapshotRequired" boolean NOT NULL
    );
    CREATE UNIQUE INDEX "IX_NotificationIncidents_SourceKey" ON "NotificationIncidents"("SourceKey") WHERE "Active";
    CREATE TABLE "UserNotifications" (
      "Id" uuid PRIMARY KEY, "UserId" varchar(450) NOT NULL, "EventKey" varchar(200) NOT NULL,
      "Kind" integer NOT NULL, "Severity" integer NOT NULL, "Title" varchar(200) NOT NULL,
      "Message" varchar(2000) NOT NULL, "Target" integer NOT NULL, "TargetId" uuid,
      "RootOnly" boolean NOT NULL, "IncidentId" uuid, "IncidentActive" boolean NOT NULL,
      "CreatedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NOT NULL,
      "Revision" bigint NOT NULL, "ReadRevision" bigint NOT NULL, "ReadAtUtc" timestamptz,
      CONSTRAINT "FK_UserNotifications_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
      CONSTRAINT "FK_UserNotifications_NotificationIncidents_IncidentId" FOREIGN KEY ("IncidentId") REFERENCES "NotificationIncidents"("Id") ON DELETE RESTRICT
    );
    CREATE UNIQUE INDEX "IX_UserNotifications_UserId_EventKey" ON "UserNotifications"("UserId","EventKey");
    CREATE INDEX "IX_UserNotifications_UserId_UpdatedAtUtc_Id" ON "UserNotifications"("UserId","UpdatedAtUtc","Id");
    CREATE INDEX "IX_UserNotifications_UserId_Revision" ON "UserNotifications"("UserId","Revision");
    CREATE INDEX "IX_UserNotifications_Unread" ON "UserNotifications"("UserId","Revision") WHERE "ReadRevision" < "Revision";
    CREATE INDEX "IX_UserNotifications_ReadAtUtc" ON "UserNotifications"("ReadAtUtc") WHERE NOT "IncidentActive" AND "ReadRevision" = "Revision";
    CREATE INDEX "IX_UserNotifications_IncidentId" ON "UserNotifications"("IncidentId");
    """);

  protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
    DROP TABLE "UserNotifications";
    DROP TABLE "NotificationIncidents";
    DROP TABLE "NotificationEvents";
    DROP SEQUENCE "NotificationRevision";
    ALTER TABLE "SynchronizationRuns" DROP COLUMN "NotificationSourceKey";
    """);
}
