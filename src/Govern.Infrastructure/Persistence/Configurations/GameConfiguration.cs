using System.Runtime.CompilerServices;
using Govern.Core.Serialization;
using Govern.Core.State;
using Govern.Domain.Entities;
using Govern.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Govern.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Game"/>: unique invite code, phase, map choice and turn mode as text, <see cref="Game.Revision"/> as the concurrency
/// token and the whole <see cref="GameState"/> as one <c>jsonb</c> snapshot written through <see cref="GameSerializer"/>.
/// </summary>
internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    private const int PhaseMaxLength = 16;
    private const int TurnModeMaxLength = 16;

    // The state is immutable and replaced on every change, so reference equality detects changes without comparing or
    // copying a ~20 KB object graph on every DetectChanges.
    private static readonly ValueComparer<GameState?> ReferenceComparer = new(
        static (left, right) => ReferenceEquals(left, right),
        static state => state == null ? 0 : RuntimeHelpers.GetHashCode(state),
        static state => state);

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(game => game.Id);

        ConfigureTextColumns(builder);

        // Join, Start and commands update only the revision they read (WHERE revision = @old), so concurrent changes to the
        // same game cannot both win; the loser gets DbUpdateConcurrencyException.
        builder.Property(game => game.Revision).IsConcurrencyToken();

        // Games created before seats were chosen were all for two players.
        builder.Property(game => game.Seats).HasDefaultValue(Game.MinSeats);

        // Never queried inside: anything a query needs (phase, players, current player) has its own column. The current
        // player column is a denormalised copy of the snapshot's, without a foreign key (the seat rows are inserted later).
        builder.Property(game => game.State)
            .HasColumnType("jsonb")
            .HasConversion(state => GameSerializer.Serialize(state), json => GameSerializer.DeserializeState(json), ReferenceComparer)
            .IsRequired(false);

        // The expired-lobby sweep filters on both (phase = 'Lobby' AND activity_at < cutoff).
        builder.HasIndex(game => new { game.Phase, game.ActivityAt });

        builder.HasIndex(game => game.CreatorId);
        builder.HasOne<User>().WithMany().HasForeignKey(game => game.CreatorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(game => game.Players)
            .WithOne()
            .HasForeignKey(player => player.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(game => game.Players).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // The invite code, the phase, the map choice and the turn mode, each stored as text.
    private static void ConfigureTextColumns(EntityTypeBuilder<Game> builder)
    {
        builder.Property(game => game.InviteCode)
            .HasConversion(code => code.Value, value => InviteCode.Create(value))
            .HasMaxLength(InviteCode.MaxLength)
            .IsRequired();
        builder.HasIndex(game => game.InviteCode).IsUnique();

        builder.Property(game => game.Phase).HasConversion<string>().HasMaxLength(PhaseMaxLength);

        // Games created before simultaneous turns were all sequential.
        builder.Property(game => game.TurnMode)
            .HasConversion<string>()
            .HasMaxLength(TurnModeMaxLength)
            .HasDefaultValue(TurnMode.Sequential);

        // Games created before the map pool were all played on generated maps.
        builder.Property(game => game.Map)
            .HasConversion(map => map.Value, value => MapChoice.Create(value))
            .HasMaxLength(MapChoice.MaxLength)
            .HasDefaultValue(MapChoice.Generated)
            .IsRequired();
    }
}
