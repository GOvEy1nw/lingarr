using FluentMigrator;

namespace Lingarr.Migrations.Migrations;

[Migration(12)]
public class M0012_AddEmbeddedSubtitlesSupport : Migration
{
    public override void Up()
    {
        if (!Schema.Table("movies").Column("video_file_path").Exists())
        {
            Alter.Table("movies")
                .AddColumn("video_file_path").AsString().Nullable();
        }

        if (!Schema.Table("movies").Column("embedded_subtitle_languages").Exists())
        {
            Alter.Table("movies")
                .AddColumn("embedded_subtitle_languages").AsString().Nullable();
        }

        if (!Schema.Table("episodes").Column("video_file_path").Exists())
        {
            Alter.Table("episodes")
                .AddColumn("video_file_path").AsString().Nullable();
        }

        if (!Schema.Table("episodes").Column("embedded_subtitle_languages").Exists())
        {
            Alter.Table("episodes")
                .AddColumn("embedded_subtitle_languages").AsString().Nullable();
        }
    }

    public override void Down()
    {
        Delete.Column("video_file_path").FromTable("movies");
        Delete.Column("embedded_subtitle_languages").FromTable("movies");
        Delete.Column("video_file_path").FromTable("episodes");
        Delete.Column("embedded_subtitle_languages").FromTable("episodes");
    }
}
