using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Guard.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261020120000_AddAuditJournal")]
public sealed class AddAuditJournal : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
    CREATE TABLE "AuditEvents" (
      "EventId" uuid NOT NULL PRIMARY KEY,
      "OccurredAtUtc" timestamp with time zone NOT NULL,
      "SchemaVersion" integer NOT NULL,
      "EventType" integer NOT NULL,
      "Actor" character varying(256),
      "ActorKind" character varying(16) NOT NULL,
      "Outcome" character varying(16) NOT NULL,
      "ClientIp" character varying(64),
      "CorrelationId" character varying(128),
      "ObjectType" character varying(64),
      "ObjectId" character varying(128),
      "Details" character varying(16384),
      "Attempts" integer NOT NULL,
      "NextAttemptAtUtc" timestamp with time zone NOT NULL,
      "LeaseToken" uuid,
      "LeaseUntilUtc" timestamp with time zone,
      "SentAtUtc" timestamp with time zone,
      "LastError" character varying(128)
    );
    CREATE INDEX "IX_AuditEvents_OccurredAtUtc_EventId" ON "AuditEvents" ("OccurredAtUtc", "EventId");
    CREATE INDEX "IX_AuditEvents_NextAttemptAtUtc_EventId" ON "AuditEvents" ("NextAttemptAtUtc", "EventId") WHERE "SentAtUtc" IS NULL;
    """);

  protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AuditEvents");
}
