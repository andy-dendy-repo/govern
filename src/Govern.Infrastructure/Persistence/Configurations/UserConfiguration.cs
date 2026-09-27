using Govern.Domain.Entities;
using Govern.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Govern.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="User"/>: unique normalised email and the owned refresh tokens collection.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasConversion(email => email.Value, value => Email.Create(value))
            .HasMaxLength(Email.MaxLength)
            .IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();

        builder.HasMany(user => user.RefreshTokens)
            .WithOne()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(user => user.RefreshTokens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
