using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Appointments;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class AppointmentRequestConfiguration : IEntityTypeConfiguration<AppointmentRequest>
{
    public void Configure(EntityTypeBuilder<AppointmentRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClinicId).IsRequired();
        builder.HasIndex(x => x.ClinicId);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(120);
        builder.Property(x => x.PetName).HasMaxLength(60).IsRequired();
        builder.Property(x => x.PetType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(600);
        builder.Property(x => x.VisitReason).HasMaxLength(120);
        builder.Property(x => x.DurationMinutes).HasDefaultValue(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.ConfirmationTokenHash).HasMaxLength(64);
        builder.Property(x => x.ManagementTokenHash).HasMaxLength(64);
        builder.HasIndex(x => x.ManagementTokenHash).IsUnique();
        builder.HasIndex(x => new { x.ClinicId, x.StartUtcSeconds, x.EndUtcSeconds });
        builder.HasIndex(x => new { x.ClinicId, x.CreatedUtcSeconds });
        builder.HasIndex(x => x.ConfirmationTokenHash).IsUnique();
        builder.HasIndex(x => new { x.ClinicId, x.RequestedStartAt })
            .IsUnique()
            .HasFilter("RequestedStartAt IS NOT NULL AND Status <> 'Cancelled'");
    }
}
