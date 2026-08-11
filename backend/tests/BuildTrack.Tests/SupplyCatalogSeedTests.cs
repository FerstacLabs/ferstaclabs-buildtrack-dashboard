using System.Reflection;
using BuildTrack.Api.Contracts;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Data;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Tests;

public sealed class SupplyCatalogSeedTests
{
    [Fact]
    public void SupplyCatalogSeedIsBroadAndCodesAreUnique()
    {
        var items = DbInitializer.SupplyCatalogSeedItems;

        Assert.True(items.Count >= 120, $"Expected at least 120 seeded catalog items, got {items.Count}.");
        Assert.Empty(items.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1));
        Assert.Contains(items, x => x.Code == "PPE-HELMET");
        Assert.Contains(items, x => x.Code == "PPE-GLOVE");
        Assert.Contains(items, x => x.Code == "MAT-CEMENT-M400");
        Assert.Contains(items, x => x.Code == "MAT-CONCRETE-B25");
        Assert.Contains(items, x => x.Code == "ELEC-EXT-CABLE-30");
    }

    [Fact]
    public void SupplyCatalogSeedUpsertKeyIsIdempotent()
    {
        var byCode = new Dictionary<string, DbInitializer.SupplyCatalogSeedItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in DbInitializer.SupplyCatalogSeedItems.Concat(DbInitializer.SupplyCatalogSeedItems))
        {
            byCode[item.Code] = item;
        }

        Assert.Equal(DbInitializer.SupplyCatalogSeedItems.Count, byCode.Count);
    }

    [Fact]
    public void SupplyCatalogSeedDefinitionsDoNotContainDuplicateNamesOrCodes()
    {
        DbInitializer.ValidateSupplyCatalogSeedDefinitions();

        Assert.Empty(DbInitializer.SupplyCatalogSeedItems
            .GroupBy(x => DbInitializer.NormalizeSeedKey(x.NameAz))
            .Where(x => x.Count() > 1));
        Assert.Empty(DbInitializer.SupplyCatalogSeedItems
            .GroupBy(x => DbInitializer.NormalizeSeedKey(x.Code))
            .Where(x => x.Count() > 1));
    }

    [Fact]
    public async Task SupplyChainSeedCanRunRepeatedlyWithoutGrowingCatalog()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "Tenant A", Code = "tenant-a", Status = TenantStatus.Active });
        await db.SaveChangesAsync();

        await DbInitializer.SeedSupplyChainDataAsync(db, CancellationToken.None);
        var firstCount = await db.FieldWarehouseCatalogItems.CountAsync(x => x.TenantId == tenantId);
        await DbInitializer.SeedSupplyChainDataAsync(db, CancellationToken.None);
        await DbInitializer.SeedSupplyChainDataAsync(db, CancellationToken.None);

        Assert.Equal(firstCount, await db.FieldWarehouseCatalogItems.CountAsync(x => x.TenantId == tenantId));
        Assert.Equal(DbInitializer.SupplyCatalogSeedItems.Count, firstCount);
    }

    [Fact]
    public async Task DemoWarehouseStockSeedIsGuardedAndIdempotent()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "Tenant A", Code = "tenant-a", Status = TenantStatus.Active });
        await db.SaveChangesAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SEED_DEMO_WAREHOUSE_STOCK"] = "true" })
            .Build();

        await DbInitializer.SeedSupplyChainDataAsync(db, configuration, CancellationToken.None);
        await DbInitializer.SeedSupplyChainDataAsync(db, configuration, CancellationToken.None);

        var helmet = await db.FieldWarehouseCatalogItems.SingleAsync(x => x.TenantId == tenantId && x.Code == "PPE-HELMET");
        Assert.Equal(10, helmet.MinimumStockLevel);
        Assert.Equal(8, await db.WarehouseStockMovements.CountAsync(x => x.TenantId == tenantId && x.ReferenceType == "SeedOpeningBalance"));
        Assert.Equal(25, await db.WarehouseStockMovements.Where(x => x.TenantId == tenantId && x.CatalogItemId == helmet.Id).SumAsync(x => x.Quantity));
    }

    [Fact]
    public void CatalogUpsertReusesLegacySameNameWithMissingCode()
    {
        using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var existing = new FieldWarehouseCatalogItem
        {
            Id = existingId,
            TenantId = tenantId,
            Name = "Kaska",
            Category = "Legacy",
            Unit = "ədəd",
            IsActive = true,
        };
        db.FieldWarehouseCatalogItems.Add(existing);
        var index = new DbInitializer.CatalogSeedIndex();
        index.Track(existing);

        DbInitializer.UpsertCatalog(db, index, tenantId, DbInitializer.SupplyCatalogSeedItems.Single(x => x.Code == "PPE-HELMET"));

        Assert.Single(db.FieldWarehouseCatalogItems.Local.Where(x => x.TenantId == tenantId && x.Name == "Kaska"));
        Assert.Equal(existingId, existing.Id);
        Assert.Equal("PPE-HELMET", existing.Code);
    }

    [Fact]
    public void CatalogUpsertReusesSameNameWithDifferentExistingCode()
    {
        using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var existing = new FieldWarehouseCatalogItem
        {
            Id = existingId,
            TenantId = tenantId,
            Name = "Kaska",
            Category = "Legacy",
            Unit = "ədəd",
            Code = "OLD-CODE",
            IsActive = true,
        };
        db.FieldWarehouseCatalogItems.Add(existing);
        var index = new DbInitializer.CatalogSeedIndex();
        index.Track(existing);

        DbInitializer.UpsertCatalog(db, index, tenantId, DbInitializer.SupplyCatalogSeedItems.Single(x => x.Code == "PPE-HELMET"));

        Assert.Single(db.FieldWarehouseCatalogItems.Local.Where(x => x.TenantId == tenantId && x.Name == "Kaska"));
        Assert.Equal(existingId, existing.Id);
        Assert.Equal("OLD-CODE", existing.Code);
    }

    [Fact]
    public void CatalogUpsertTracksAddedEntitiesBeforeSaveChanges()
    {
        using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var index = new DbInitializer.CatalogSeedIndex();
        var seed = DbInitializer.SupplyCatalogSeedItems.Single(x => x.Code == "PPE-HELMET");

        DbInitializer.UpsertCatalog(db, index, tenantId, seed);
        DbInitializer.UpsertCatalog(db, index, tenantId, seed with { Code = "PPE-HELMET-ALT" });

        Assert.Single(db.FieldWarehouseCatalogItems.Local.Where(x => x.TenantId == tenantId && x.Name == "Kaska"));
    }

    [Fact]
    public void CatalogUpsertAllowsSameNameForDifferentTenants()
    {
        using var db = CreateDb();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var seed = DbInitializer.SupplyCatalogSeedItems.Single(x => x.Code == "PPE-HELMET");
        var indexA = new DbInitializer.CatalogSeedIndex();
        var indexB = new DbInitializer.CatalogSeedIndex();

        DbInitializer.UpsertCatalog(db, indexA, tenantA, seed);
        DbInitializer.UpsertCatalog(db, indexB, tenantB, seed);

        Assert.Equal(2, db.FieldWarehouseCatalogItems.Local.Count(x => x.Name == "Kaska"));
        Assert.Single(db.FieldWarehouseCatalogItems.Local.Where(x => x.TenantId == tenantA && x.Name == "Kaska"));
        Assert.Single(db.FieldWarehouseCatalogItems.Local.Where(x => x.TenantId == tenantB && x.Name == "Kaska"));
    }

    [Fact]
    public void CatalogUpsertDoesNotDestructivelyConvertCustomItem()
    {
        using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var existing = new FieldWarehouseCatalogItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Kaska",
            NameAz = "Tenant custom kaska",
            Category = "Custom PPE",
            Unit = "ədəd",
            IsCustom = true,
            IsActive = true,
        };
        db.FieldWarehouseCatalogItems.Add(existing);
        var index = new DbInitializer.CatalogSeedIndex();
        index.Track(existing);

        DbInitializer.UpsertCatalog(db, index, tenantId, DbInitializer.SupplyCatalogSeedItems.Single(x => x.Code == "PPE-HELMET"));

        Assert.True(existing.IsCustom);
        Assert.Equal("Tenant custom kaska", existing.NameAz);
        Assert.Equal("Custom PPE", existing.Category);
        Assert.Equal("PPE-HELMET", existing.Code);
    }

    [Fact]
    public async Task ExistingReferencedCatalogItemIdRemainsUnchangedAfterSeed()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, CompanyName = "Tenant A", Code = "tenant-a", Status = TenantStatus.Active });
        db.FieldWarehouseCatalogItems.Add(new FieldWarehouseCatalogItem
        {
            Id = itemId,
            TenantId = tenantId,
            Name = "Kaska",
            Category = "Legacy",
            Unit = "ədəd",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        await DbInitializer.SeedSupplyChainDataAsync(db, CancellationToken.None);

        var item = await db.FieldWarehouseCatalogItems.SingleAsync(x => x.TenantId == tenantId && x.Name == "Kaska");
        Assert.Equal(itemId, item.Id);
        Assert.Equal("PPE-HELMET", item.Code);
    }

    [Theory]
    [InlineData("armatur", "STEEL-REBAR-12")]
    [InlineData("арматура", "STEEL-REBAR-12")]
    [InlineData("rebar", "STEEL-REBAR-12")]
    [InlineData("beton", "MAT-CONCRETE-B25")]
    [InlineData("бетон", "MAT-CONCRETE-B25")]
    [InlineData("concrete", "MAT-CONCRETE-B25")]
    [InlineData("kaska", "PPE-HELMET")]
    [InlineData("каска", "PPE-HELMET")]
    [InlineData("helmet", "PPE-HELMET")]
    [InlineData("elcek", "PPE-GLOVE")]
    [InlineData("əlcək", "PPE-GLOVE")]
    [InlineData("перчатки", "PPE-GLOVE")]
    [InlineData("gloves", "PPE-GLOVE")]
    [InlineData("kabel", "ELEC-CABLE-2-5")]
    [InlineData("кабель", "ELEC-CABLE-2-5")]
    [InlineData("cable", "ELEC-CABLE-2-5")]
    public void RepresentativeCatalogSearchesWorkAcrossLanguages(string query, string expectedCode)
    {
        var normalized = query.ToLowerInvariant();
        var match = DbInitializer.SupplyCatalogSeedItems.FirstOrDefault(item =>
            item.Code.Equals(expectedCode, StringComparison.OrdinalIgnoreCase)
            && $"{item.NameAz} {item.NameRu} {item.NameEn} {item.Code} {item.SearchAliases}".ToLowerInvariant().Contains(normalized));

        Assert.NotNull(match);
    }

    [Fact]
    public void FieldWarehouseCatalogDtoDoesNotExposeStockOrPriceData()
    {
        var propertyNames = typeof(FieldWarehouseCatalogItemDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("stock", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("balance", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("cost", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("price", StringComparison.OrdinalIgnoreCase));
    }

    private static BuildTrackDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BuildTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BuildTrackDbContext(options, new TenantContext());
    }
}
