using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Lingarr.Core.Entities;

namespace Lingarr.Core.Configuration;

public class EpisodeConfiguration : IEntityTypeConfiguration<Episode>
{
    public void Configure(EntityTypeBuilder<Episode> builder)
    {
        builder
            .HasOne(e => e.Season)
            .WithMany(s => s.Episodes)
            .HasForeignKey(e => e.SeasonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Season).AutoInclude();

        // Store the embedded subtitle language list as a JSON string in a single column.
        // The provider type is string? so that EF Core generates null-safe read code for
        // existing rows that have NULL in this column (added by migration M0012).
        var languageListConverter = new ValueConverter<List<string>, string?>(
            list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
            json => string.IsNullOrEmpty(json)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>()
        );

        // Value comparer required to let EF Core detect changes to the list correctly
        var languageListComparer = new ValueComparer<List<string>>(
            (l1, l2) => (l1 == null && l2 == null) ||
                        (l1 != null && l2 != null && l1.SequenceEqual(l2)),
            l => l == null ? 0 : l.Aggregate(0, (hash, v) => HashCode.Combine(hash, v.GetHashCode())),
            l => l == null ? new List<string>() : l.ToList()
        );

        builder.Property(e => e.EmbeddedSubtitleLanguages)
            .HasColumnName("embedded_subtitle_languages")
            .IsRequired(false)
            .HasConversion(languageListConverter, languageListComparer);
    }
}