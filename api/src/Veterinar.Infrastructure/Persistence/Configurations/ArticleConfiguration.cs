using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Veterinar.Domain.Content;

namespace Veterinar.Infrastructure.Persistence.Configurations;

public sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClinicId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(220).IsRequired();
        builder.Property(x => x.Summary).HasMaxLength(800).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsPublished).IsRequired();
        builder.HasIndex(x => new { x.ClinicId, x.Slug }).IsUnique();
        builder.HasIndex(x => new { x.ClinicId, x.ArticleSectionId, x.SortOrder });
    }
}
