using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Persistence.Seeding;

public static class DatabaseSchemaMigrationSeeder
{
    public static async Task EnsureSchemaUpdatedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        await EnsureSchemaUpdatedAsync(dbContext);
    }

    public static async Task EnsureSchemaUpdatedAsync(FarmKartDbContext dbContext)
    {
        try
        {
            var sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuctionAllocations')
                BEGIN
                    CREATE TABLE [AuctionAllocations] (
                        [Id] uniqueidentifier NOT NULL,
                        [AuctionId] uniqueidentifier NOT NULL,
                        [BidId] uniqueidentifier NOT NULL,
                        [CustomerProfileId] uniqueidentifier NOT NULL,
                        [RequestedQuantityKg] decimal(18,2) NOT NULL,
                        [AllocatedQuantityKg] decimal(18,2) NOT NULL,
                        [WinningBidAmountPerMan] decimal(18,2) NOT NULL,
                        [Status] nvarchar(50) NOT NULL,
                        [FinalizedAtUtc] datetime2 NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [UpdatedAtUtc] datetime2 NULL,
                        CONSTRAINT [PK_AuctionAllocations] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_AuctionAllocations_Auctions_AuctionId] FOREIGN KEY ([AuctionId]) REFERENCES [Auctions] ([Id]),
                        CONSTRAINT [FK_AuctionAllocations_Bids_BidId] FOREIGN KEY ([BidId]) REFERENCES [Bids] ([Id]),
                        CONSTRAINT [FK_AuctionAllocations_CustomerProfiles_CustomerProfileId] FOREIGN KEY ([CustomerProfileId]) REFERENCES [CustomerProfiles] ([Id])
                    );
                    CREATE INDEX [IX_AuctionAllocations_AuctionId] ON [AuctionAllocations] ([AuctionId]);
                    CREATE INDEX [IX_AuctionAllocations_BidId] ON [AuctionAllocations] ([BidId]);
                    CREATE INDEX [IX_AuctionAllocations_CustomerProfileId] ON [AuctionAllocations] ([CustomerProfileId]);
                END;

                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AuctionAllocations')
                BEGIN
                    UPDATE [AuctionAllocations]
                    SET [Status] = CASE [Status]
                        WHEN '0' THEN 'Won'
                        WHEN '1' THEN 'PartiallyWon'
                        WHEN '2' THEN 'Lost'
                        ELSE [Status]
                    END
                    WHERE [Status] IN ('0', '1', '2');
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Bids') AND name = 'RequestedQuantityKg')
                BEGIN
                    ALTER TABLE [Bids] ADD [RequestedQuantityKg] decimal(18,2) NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuctionPayments') AND name = 'AllocatedQuantityKg')
                BEGIN
                    ALTER TABLE [AuctionPayments] ADD [AllocatedQuantityKg] decimal(18,2) NOT NULL DEFAULT 0;
                END;
            ";

            await dbContext.Database.ExecuteSqlRawAsync(sql);
        }
        catch
        {
            // Ignore in environment configurations where DDL is restricted or in-memory provider is used
        }
    }
}
