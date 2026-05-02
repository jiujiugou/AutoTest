using FluentMigrator;

namespace AutoTest.Migrations;

/// <summary>
/// AI 分析结果表
/// </summary>
[Migration(2026041801)]
public sealed class CreateAIAnalysisTable : Migration
{
    public override void Up()
    {
        Create.Table("AIAnalysis")
            .WithColumn("Id").AsGuid().PrimaryKey()

            .WithColumn("OutboxMessageId").AsGuid().NotNullable()

            .WithColumn("Type").AsString(100).NotNullable()
            .WithColumn("Severity").AsString(20).NotNullable()

            .WithColumn("Category").AsString(100).Nullable()
            .WithColumn("RootCause").AsString(int.MaxValue).Nullable()
            .WithColumn("Suggestion").AsString(int.MaxValue).Nullable()
            .WithColumn("Summary").AsString(500).Nullable()

            .WithColumn("Confidence").AsDouble().Nullable()

            .WithColumn("InputJson").AsString(int.MaxValue).NotNullable()
            .WithColumn("OutputJson").AsString(int.MaxValue).Nullable()

            .WithColumn("Model").AsString(100).Nullable()
            .WithColumn("PromptVersion").AsString(50).Nullable()

            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("ProcessedAt").AsDateTime().Nullable();

        // 索引（非常重要，不然查询会慢）
        Create.Index("IX_AIAnalysis_OutboxMessageId")
            .OnTable("AIAnalysis")
            .OnColumn("OutboxMessageId").Ascending();

        Create.Index("IX_AIAnalysis_Severity")
            .OnTable("AIAnalysis")
            .OnColumn("Severity").Ascending();

        Create.Index("IX_AIAnalysis_Type")
            .OnTable("AIAnalysis")
            .OnColumn("Type").Ascending();
    }

    public override void Down()
    {
        Delete.Table("AIAnalysis");
    }
}