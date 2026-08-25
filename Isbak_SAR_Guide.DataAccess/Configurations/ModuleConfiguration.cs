using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(2000);

        builder.HasOne(m => m.Book)
            .WithMany(b => b.Modules)
            .HasForeignKey(m => m.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.BookId, m.DisplayOrder });

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
