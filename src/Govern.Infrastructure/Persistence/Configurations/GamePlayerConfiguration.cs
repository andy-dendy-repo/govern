using Govern.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Govern.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="GamePlayer"/>: a user sits at most once per game and each seat holds one user, both enforced by unique
/// indexes, so concurrent joins cannot oversubscribe a game.
/// </summary>
internal sealed class GamePlayerConfiguration : IEntityTypeConfiguration<GamePlayer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<GamePlayer> builder)
    {
        builder.HasKey(player => player.Id);

        builder.HasIndex(player => new { player.GameId, player.UserId }).IsUnique();
        builder.HasIndex(player => new { player.GameId, player.Seat }).IsUnique();

        // Leads with the user id, so it serves the foreign key and the "games I play in" filters, and the rolling seat
        // quota (seats joined since a time) too.
        builder.HasIndex(player => new { player.UserId, player.JoinedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(player => player.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(player => player.PlayerId);
    }
}
