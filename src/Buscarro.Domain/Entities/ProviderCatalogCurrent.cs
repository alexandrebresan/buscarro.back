namespace Buscarro.Domain.Entities;

public sealed class ProviderCatalogCurrent
{
    private ProviderCatalogCurrent()
    {
    }

    public ProviderCatalogCurrent(string provider, int currentGenerationId)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException("Provider is required.", nameof(provider));
        }
        if (currentGenerationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentGenerationId));
        }

        Provider = provider.Trim();
        CurrentGenerationId = currentGenerationId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public string Provider { get; private set; } = string.Empty;
    public int CurrentGenerationId { get; private set; }
    public CatalogGeneration? CurrentGeneration { get; private set; }
    public int? PreviousGenerationId { get; private set; }
    public CatalogGeneration? PreviousGeneration { get; private set; }
    public int? LastPublicationId { get; private set; }
    public CatalogPublication? LastPublication { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Publish(int currentGenerationId, int? previousGenerationId, DateTimeOffset updatedAtUtc)
    {
        if (updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC offset.", nameof(updatedAtUtc));
        }
        if (currentGenerationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentGenerationId));
        }
        if (previousGenerationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(previousGenerationId));
        }
        if (previousGenerationId == currentGenerationId)
        {
            throw new ArgumentException("Current and previous generation must be different.", nameof(previousGenerationId));
        }

        CurrentGenerationId = currentGenerationId;
        PreviousGenerationId = previousGenerationId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void AssignPublication(int publicationId) => LastPublicationId = publicationId;
}
