using Buscarro.Domain.Enums;
using System.Text.RegularExpressions;

namespace Buscarro.Domain.Entities;

public sealed class CatalogGeneration
{
    private CatalogGeneration()
    {
    }

    public CatalogGeneration(string provider, DateTimeOffset createdAtUtc)
    {
        Provider = RequireText(provider, nameof(provider));
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public int Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public CatalogGenerationStatus Status { get; private set; } = CatalogGenerationStatus.Loading;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ReadyAtUtc { get; private set; }
    public DateTimeOffset? ActivatedAtUtc { get; private set; }
    public DateTimeOffset? FailedAtUtc { get; private set; }
    public int PageCount { get; private set; }
    public int SourceItemCount { get; private set; }
    public int ValidItemCount { get; private set; }
    public int InvalidItemCount { get; private set; }
    public int DuplicateItemCount { get; private set; }
    public string? ChecksumSha256 { get; private set; }
    public string? ValidationSummaryJson { get; private set; }
    public string? SanitizedError { get; private set; }
    public ICollection<VehicleCatalogItem> Items { get; private set; } = new List<VehicleCatalogItem>();

    public void MarkReady(
        int pageCount,
        int sourceItemCount,
        int validItemCount,
        int invalidItemCount,
        int duplicateItemCount,
        string checksumSha256,
        string? validationSummaryJson,
        DateTimeOffset readyAtUtc)
    {
        RequireStatus(CatalogGenerationStatus.Loading);
        EnsureNonNegative(pageCount, nameof(pageCount));
        EnsureNonNegative(sourceItemCount, nameof(sourceItemCount));
        EnsureNonNegative(validItemCount, nameof(validItemCount));
        EnsureNonNegative(invalidItemCount, nameof(invalidItemCount));
        EnsureNonNegative(duplicateItemCount, nameof(duplicateItemCount));
        if (validItemCount + invalidItemCount + duplicateItemCount > sourceItemCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceItemCount), "Item counts cannot exceed the source count.");
        }

        if (checksumSha256.Length != 64 || !checksumSha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Checksum must be a 64-character hexadecimal SHA-256 value.", nameof(checksumSha256));
        }

        var timestamp = EnsureUtc(readyAtUtc, nameof(readyAtUtc));
        PageCount = pageCount;
        SourceItemCount = sourceItemCount;
        ValidItemCount = validItemCount;
        InvalidItemCount = invalidItemCount;
        DuplicateItemCount = duplicateItemCount;
        ChecksumSha256 = checksumSha256.ToLowerInvariant();
        ValidationSummaryJson = validationSummaryJson;
        ReadyAtUtc = timestamp;
        Status = CatalogGenerationStatus.Ready;
        UpdatedAtUtc = timestamp;
    }

    public void RecordValidationSummary(string validationSummaryJson, DateTimeOffset updatedAtUtc)
    {
        RequireStatus(CatalogGenerationStatus.Loading);
        ValidationSummaryJson = RequireText(validationSummaryJson, nameof(validationSummaryJson));
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void MarkCurrent(DateTimeOffset activatedAtUtc)
    {
        RequireStatus(CatalogGenerationStatus.Ready);
        var timestamp = EnsureUtc(activatedAtUtc, nameof(activatedAtUtc));
        Status = CatalogGenerationStatus.Current;
        ActivatedAtUtc = timestamp;
        UpdatedAtUtc = timestamp;
    }

    public void MarkPrevious(DateTimeOffset updatedAtUtc)
    {
        RequireStatus(CatalogGenerationStatus.Current);
        var timestamp = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        Status = CatalogGenerationStatus.Previous;
        UpdatedAtUtc = timestamp;
    }

    public void RestoreAsCurrent(DateTimeOffset activatedAtUtc)
    {
        RequireStatus(CatalogGenerationStatus.Previous);
        var timestamp=EnsureUtc(activatedAtUtc,nameof(activatedAtUtc));
        Status=CatalogGenerationStatus.Current;
        ActivatedAtUtc=timestamp;
        UpdatedAtUtc=timestamp;
    }

    public void MarkInactiveReady(DateTimeOffset updatedAtUtc)
    {
        RequireStatus(CatalogGenerationStatus.Previous);
        var timestamp = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        Status = CatalogGenerationStatus.Ready;
        UpdatedAtUtc = timestamp;
    }

    public void MarkFailed(string error, DateTimeOffset failedAtUtc)
    {
        RequireStatus(CatalogGenerationStatus.Loading);
        var timestamp = EnsureUtc(failedAtUtc, nameof(failedAtUtc));
        SanitizedError = Sanitize(error);
        FailedAtUtc = timestamp;
        Status = CatalogGenerationStatus.Failed;
        UpdatedAtUtc = timestamp;
    }

    private void RequireStatus(CatalogGenerationStatus required)
    {
        if (Status != required)
        {
            throw new InvalidOperationException($"Generation in status {Status} cannot transition from required status {required}.");
        }
    }

    private static string Sanitize(string value)
    {
        var sanitized = RequireText(value, nameof(value)).Replace('\r', ' ').Replace('\n', ' ');
        sanitized = Regex.Replace(sanitized, @"(?i)(bearer\s+)[^\s]+", "$1[REDACTED]");
        sanitized = Regex.Replace(sanitized, @"(?i)((?:password|api[_-]?key)\s*[=:]\s*)[^\s;,]+", "$1[REDACTED]");
        return sanitized.Length <= 2_000 ? sanitized : sanitized[..2_000];
    }

    private static string RequireText(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    private static void EnsureNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
        => value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException("Timestamp must use UTC offset.", parameterName);
}
