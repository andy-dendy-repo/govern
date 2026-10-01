using System.Collections.Immutable;
using System.Text.Json;
using Govern.Core.Events;
using Govern.Core.Serialization;
using Govern.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Govern.Infrastructure.Persistence.Configurations;

internal sealed class GameLogEntryConfiguration : IEntityTypeConfiguration<GameLogEntry>
{
    // Entries are written once and never changed, so reference equality is enough.
    private static readonly ValueComparer<ImmutableArray<GameEvent>> ReferenceComparer = new(
        static (left, right) => left == right,
        static events => events.IsDefault ? 0 : events.Length,
        static events => events);

    public void Configure(EntityTypeBuilder<GameLogEntry> builder)
    {
        builder.HasKey(entry => entry.Id);

        // A seat's log is read by sequence (the recent window). Not unique: the game's revision token already lets only one
        // command per revision commit, and a unique index would make racing commands fail (or deadlock) on the log rows first.
        builder.HasIndex(entry => new { entry.PlayerId, entry.Sequence });
        builder.HasIndex(entry => entry.GameId);
        builder.HasOne<Game>().WithMany(game => game.LogEntries).HasForeignKey(entry => entry.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GamePlayer>().WithMany().HasForeignKey(entry => entry.PlayerId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(entry => entry.Events)
            .HasColumnType("jsonb")
            .HasConversion(
                events => JsonSerializer.Serialize(events, GameSerializer.Options),
                json => JsonSerializer.Deserialize<ImmutableArray<GameEvent>>(json, GameSerializer.Options),
                ReferenceComparer)
            .IsRequired();
    }
}
