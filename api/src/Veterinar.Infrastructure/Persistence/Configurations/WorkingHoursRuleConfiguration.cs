using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Scheduling;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class WorkingHoursRuleConfiguration : IEntityTypeConfiguration<WorkingHoursRule>
{
    public void Configure(EntityTypeBuilder<WorkingHoursRule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OpensAt).IsRequired();
        builder.Property(x => x.ClosesAt).IsRequired();
        builder.Property(x => x.SlotMinutes).IsRequired();
        builder.HasIndex(x => new { x.ClinicId, x.DayOfWeek }).IsUnique();
    }
}
