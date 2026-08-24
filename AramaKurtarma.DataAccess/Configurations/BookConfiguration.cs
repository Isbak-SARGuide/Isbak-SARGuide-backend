using AramaKurtarma.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AramaKurtarma.DataAccess.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.Property(b => b.Title).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Slug).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(2000);
        builder.Property(b => b.LanguageCode).HasMaxLength(10).IsRequired();

        builder.HasIndex(b => b.Slug).IsUnique();

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
