using Buscarro.Domain.Entities;
using Buscarro.Domain.Enums;

namespace Buscarro.Tests;

public sealed class CatalogGenerationTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Generation_moves_through_publication_lifecycle()
    {
        var generation = new CatalogGeneration("carzin", StartedAt);

        generation.MarkReady(2, 120, 100, 15, 5, new string('a', 64), "{}", StartedAt.AddMinutes(1));
        generation.MarkCurrent(StartedAt.AddMinutes(2));
        generation.MarkPrevious(StartedAt.AddMinutes(3));
        generation.MarkInactiveReady(StartedAt.AddMinutes(4));

        Assert.Equal(CatalogGenerationStatus.Ready, generation.Status);
        Assert.Equal(2, generation.PageCount);
        Assert.Equal(120, generation.SourceItemCount);
        Assert.Equal(100, generation.ValidItemCount);
        Assert.Equal(15, generation.InvalidItemCount);
        Assert.Equal(5, generation.DuplicateItemCount);
        Assert.Equal(new string('a', 64), generation.ChecksumSha256);
        Assert.Equal(StartedAt.AddMinutes(1), generation.ReadyAtUtc);
        Assert.Equal(StartedAt.AddMinutes(2), generation.ActivatedAtUtc);
        Assert.Equal(StartedAt.AddMinutes(4), generation.UpdatedAtUtc);
    }

    [Fact]
    public void Generation_rejects_invalid_transition_and_invalid_counts()
    {
        var generation = new CatalogGeneration("carzin", StartedAt);

        Assert.Throws<InvalidOperationException>(() => generation.MarkCurrent(StartedAt));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            generation.MarkReady(1, 10, 8, 3, 0, new string('a', 64), null, StartedAt));
    }

    [Fact]
    public void Failed_generation_sanitizes_and_truncates_error()
    {
        var generation = new CatalogGeneration("carzin", StartedAt);
        var sensitive = "Bearer abc\r\n" + new string('x', 2_100);

        generation.MarkFailed(sensitive, StartedAt.AddMinutes(1));

        Assert.Equal(CatalogGenerationStatus.Failed, generation.Status);
        Assert.NotNull(generation.SanitizedError);
        Assert.DoesNotContain('\r', generation.SanitizedError);
        Assert.DoesNotContain('\n', generation.SanitizedError);
        Assert.DoesNotContain("abc", generation.SanitizedError, StringComparison.OrdinalIgnoreCase);
        Assert.True(generation.SanitizedError!.Length <= 2_000);
        Assert.Equal(StartedAt.AddMinutes(1), generation.FailedAtUtc);
    }

    [Fact]
    public void Transition_rejects_non_utc_timestamp_before_mutating_status()
    {
        var generation = new CatalogGeneration("carzin", StartedAt);
        generation.MarkReady(1, 1, 1, 0, 0, new string('a', 64), null, StartedAt.AddMinutes(1));
        generation.MarkCurrent(StartedAt.AddMinutes(2));
        var nonUtc = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));

        Assert.Throws<ArgumentException>(() => generation.MarkPrevious(nonUtc));

        Assert.Equal(CatalogGenerationStatus.Current, generation.Status);
        Assert.Equal(StartedAt.AddMinutes(2), generation.UpdatedAtUtc);
    }
}
