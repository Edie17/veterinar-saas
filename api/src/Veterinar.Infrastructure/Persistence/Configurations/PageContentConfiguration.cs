using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Content;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class PageContentConfiguration : IEntityTypeConfiguration<PageContent>
{
    public void Configure(EntityTypeBuilder<PageContent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClinicId).IsRequired();
        builder.HasIndex(x => x.ClinicId).IsUnique();
        builder.Property(x => x.ClinicName).HasMaxLength(180).IsRequired();
        builder.Property(x => x.HeroTitle).HasMaxLength(220).IsRequired();
        builder.Property(x => x.HeroText).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300).IsRequired();
    }
}
