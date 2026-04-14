using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026041004)]
public sealed class CreateOutboxTable : Migration
{
    public override void Up()
    {
        Create.Table("OutboxMessage")
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("Type").AsString(200).NotNullable()
            .WithColumn("PayloadJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("OccurredAt").AsDateTime().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable()
            .WithColumn("Attempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("NextAttemptAt").AsDateTime().Nullable()
            .WithColumn("LockedUntil").AsDateTime().Nullable()
            .WithColumn("LockedBy").AsString(200).Nullable()
            .WithColumn("LastError").AsString(int.MaxValue).Nullable()
            .WithColumn("SentAt").AsDateTime().Nullable();

        Create.Index("IX_OutboxMessage_Status_NextAttemptAt_OccurredAt")
            .OnTable("OutboxMessage")
            .OnColumn("Status").Ascending()
            .OnColumn("NextAttemptAt").Ascending()
            .OnColumn("OccurredAt").Ascending();
    }

    public override void Down()
    {
        Delete.Table("OutboxMessage");
    }
}

