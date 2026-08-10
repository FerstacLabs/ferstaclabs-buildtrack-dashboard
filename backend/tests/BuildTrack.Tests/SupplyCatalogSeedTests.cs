using System.Reflection;
using BuildTrack.Api.Contracts;
using BuildTrack.Infrastructure.Data;

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
}
