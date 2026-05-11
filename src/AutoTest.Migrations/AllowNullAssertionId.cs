using FluentMigrator;

namespace AutoTest.Migrations;

[Migration(2026051103)]
public class AllowNullAssertionId : Migration
{
    public override void Down()
    {
        // 恢复：将 NULL AssertionId 的行删掉，再恢复 FK
        Execute.Sql("DELETE FROM AssertionResult WHERE AssertionId IS NULL");
        Delete.ForeignKey("FK_AssertionResult_Assertion").OnTable("AssertionResult");
        Alter.Column("AssertionId").OnTable("AssertionResult").AsGuid().NotNullable();
        Create.ForeignKey("FK_AssertionResult_Assertion")
            .FromTable("AssertionResult").ForeignColumn("AssertionId")
            .ToTable("Assertion").PrimaryColumn("Id");
    }

    public override void Up()
    {
        Delete.ForeignKey("FK_AssertionResult_Assertion").OnTable("AssertionResult");
        Alter.Column("AssertionId").OnTable("AssertionResult").AsGuid().Nullable();
    }
}
