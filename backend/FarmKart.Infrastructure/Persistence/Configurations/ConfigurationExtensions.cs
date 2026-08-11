using FarmKart.Domain.Common;
using FarmKart.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Linq.Expressions;

namespace FarmKart.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    public static void ConfigureBaseEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc)
            .IsRequired();
    }

    public static void ConfigureAddressInfo<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, AddressInfo?>> navigationExpression,
        Action<OwnedNavigationBuilder<TEntity, AddressInfo>>? configure = null)
        where TEntity : class
    {
        builder.OwnsOne(navigationExpression, address =>
        {
            address.Property(property => property.AddressLine)
                .HasColumnName("Address")
                .HasMaxLength(250)
                .IsRequired();

            address.Property(property => property.City)
                .HasMaxLength(100)
                .IsRequired();

            address.Property(property => property.State)
                .HasMaxLength(100)
                .IsRequired();

            address.Property(property => property.Pincode)
                .HasMaxLength(12)
                .IsRequired();

            address.Property(property => property.Latitude)
                .HasPrecision(9, 6);

            address.Property(property => property.Longitude)
                .HasPrecision(9, 6);

            configure?.Invoke(address);
        });
    }
}
