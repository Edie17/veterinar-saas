using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Clinics;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class ClinicConfiguration : IEntityTypeConfiguration<Clinic>
{
    public void Configure(EntityTypeBuilder<Clinic> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(180);
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}
