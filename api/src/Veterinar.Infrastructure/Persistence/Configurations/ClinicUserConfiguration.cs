using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Users;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class ClinicUserConfiguration : IEntityTypeConfiguration<ClinicUser>
{
    public void Configure(EntityTypeBuilder<ClinicUser> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.ClinicId, x.Email }).IsUnique();
        builder.HasIndex(x => x.Email);
    }
}
