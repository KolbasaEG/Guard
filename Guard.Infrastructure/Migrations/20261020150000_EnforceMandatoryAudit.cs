using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Guard.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261020150000_EnforceMandatoryAudit")]
public sealed class EnforceMandatoryAudit : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
    ALTER TABLE "ExportDeliveryAttempts" ALTER COLUMN "Succeeded" DROP NOT NULL;
    UPDATE "ExportDeliveryAttempts" SET "Succeeded"=NULL WHERE "CompletedAtUtc" IS NULL;
    ALTER TABLE "AuditEvents"
      ADD COLUMN "ReasonCode" varchar(128), ADD COLUMN "AttemptedAccount" varchar(256),
      ADD COLUMN "DeliveryRequired" boolean NOT NULL DEFAULT true,
      ADD COLUMN "QueuedAtUtc" timestamptz,
      ADD COLUMN "DestinationVersion" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
      ADD COLUMN "PayloadBytes" integer NOT NULL DEFAULT 0;
    UPDATE "AuditEvents" a SET "QueuedAtUtc"="OccurredAtUtc", "PayloadBytes"=octet_length(row_to_json(a)::text);
    ALTER TABLE "AuditEvents" ALTER COLUMN "DeliveryRequired" DROP DEFAULT,
      ALTER COLUMN "DestinationVersion" DROP DEFAULT, ALTER COLUMN "PayloadBytes" DROP DEFAULT;
    CREATE INDEX "IX_AuditEvents_QueuedAtUtc_EventId" ON "AuditEvents"("QueuedAtUtc","EventId")
      WHERE "DeliveryRequired" AND "SentAtUtc" IS NULL;
    CREATE TABLE "AuditSettings" (
      "Id" integer PRIMARY KEY, "Mode" integer NOT NULL, "Configured" boolean NOT NULL,
      "RetentionYears" integer NOT NULL, "MaxQueueAgeMinutes" integer NOT NULL,
      "MaxQueueBytes" bigint NOT NULL, "MaxQueueCount" integer NOT NULL,
      "Host" varchar(253) NOT NULL, "Port" integer NOT NULL, "CaCertificatePem" varchar(16384),
      "Version" uuid NOT NULL, "ModifiedBy" varchar(256), "ModifiedAtUtc" timestamptz,
      "WorkerCompletedAtUtc" timestamptz, CONSTRAINT "CK_AuditSettings_Singleton" CHECK ("Id"=1)
    );
    INSERT INTO "AuditSettings"("Id","Mode","Configured","RetentionYears","MaxQueueAgeMinutes","MaxQueueBytes",
      "MaxQueueCount","Host","Port","Version") VALUES (1,0,false,1,0,0,0,'',6514,'00000000-0000-0000-0000-000000000000');
    CREATE TABLE "AuditDeliveryAttempts" (
      "Id" uuid PRIMARY KEY, "EventId" uuid NOT NULL REFERENCES "AuditEvents"("EventId") ON DELETE CASCADE,
      "StartedAtUtc" timestamptz NOT NULL, "CompletedAtUtc" timestamptz, "Destination" varchar(280) NOT NULL,
      "Succeeded" boolean, "Error" varchar(256)
    );
    CREATE INDEX "IX_AuditDeliveryAttempts_EventId_StartedAtUtc" ON "AuditDeliveryAttempts"("EventId","StartedAtUtc");
    """);

  protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
    UPDATE "ExportDeliveryAttempts" SET "Succeeded"=false WHERE "Succeeded" IS NULL;
    ALTER TABLE "ExportDeliveryAttempts" ALTER COLUMN "Succeeded" SET NOT NULL;
    DROP TABLE "AuditDeliveryAttempts";
    DROP TABLE "AuditSettings";
    DROP INDEX "IX_AuditEvents_QueuedAtUtc_EventId";
    ALTER TABLE "AuditEvents" DROP COLUMN "ReasonCode", DROP COLUMN "AttemptedAccount",
      DROP COLUMN "DeliveryRequired", DROP COLUMN "QueuedAtUtc", DROP COLUMN "DestinationVersion", DROP COLUMN "PayloadBytes";
    """);
}
