using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Content;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class ArticleSectionConfiguration : IEntityTypeConfiguration<ArticleSection>
{
    public void Configure(EntityTypeBuilder<ArticleSection> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClinicId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(140).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(600).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsPublished).IsRequired();
        builder.HasIndex(x => new { x.ClinicId, x.Slug }).IsUnique();

        builder.HasMany(x => x.Articles)
            .WithOne(x => x.Section)
            .HasForeignKey(x => x.ArticleSectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
