namespace Buscarro.Domain.Entities;

public sealed class CatalogPublication
{
    private CatalogPublication()
    {
    }

    public CatalogPublication(DateTimeOffset publishedAtUtc, int totalCatalogCount, int providerCount)
    {
        if (publishedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC offset.", nameof(publishedAtUtc));
        }
        if (totalCatalogCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCatalogCount));
        }
        if (providerCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(providerCount));
        }

        PublishedAtUtc = publishedAtUtc;
        TotalCatalogCount = totalCatalogCount;
        ProviderCount = providerCount;
    }

    public int Id { get; private set; }
    public DateTimeOffset PublishedAtUtc { get; private set; }
    public int TotalCatalogCount { get; private set; }
    public int ProviderCount { get; private set; }
}
