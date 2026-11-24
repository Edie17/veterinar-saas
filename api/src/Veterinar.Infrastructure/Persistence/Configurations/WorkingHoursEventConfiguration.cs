using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Scheduling;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class WorkingHoursEventConfiguration : IEntityTypeConfiguration<WorkingHoursEvent>
{
    public void Configure(EntityTypeBuilder<WorkingHoursEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.DisplayFrom).IsRequired();
        builder.Property(x => x.StartsOn).IsRequired();
        builder.Property(x => x.EndsOn).IsRequired();
        builder.Property(x => x.IsPublished).IsRequired();
        builder.HasIndex(x => new { x.ClinicId, x.DisplayFrom, x.StartsOn, x.EndsOn });

        builder.HasMany(x => x.Rules)
            .WithOne(x => x.Event)
            .HasForeignKey(x => x.WorkingHoursEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
