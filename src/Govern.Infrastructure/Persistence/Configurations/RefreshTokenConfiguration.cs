using Govern.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Govern.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RefreshToken"/>: tokens are looked up by their unique hash.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.HasIndex(token => token.UserId);

        // Rotation and revocation update only rows that are still unrevoked (WHERE revoked_at IS NULL), so two
        // concurrent refreshes with the same token cannot both succeed; the loser gets DbUpdateConcurrencyException.
        builder.Property(token => token.RevokedAt).IsConcurrencyToken();
    }
}
