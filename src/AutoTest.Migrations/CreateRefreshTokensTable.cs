using FluentMigrator;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Migrations
{
    [Migration(2026041002)]
    public class CreateRefreshTokensTable : Migration
    {
        public override void Up()
        {
            Create.Table("RefreshTokens")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("UserId").AsInt32().NotNullable()
                .ForeignKey("Users", "Id")
                .WithColumn("Token").AsString(512).NotNullable().Unique()
                .WithColumn("ExpireAt").AsDateTime().NotNullable()
                .WithColumn("Revoked").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithColumn("ReplacedByToken").AsString(512).Nullable();
        }

        public override void Down()
        {
            Delete.Table("RefreshTokens");
        }
    }
}
