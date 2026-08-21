namespace Buscarro.Infrastructure.Services;

public sealed class CatalogReadinessOptions
{
    public const string SectionName="CatalogReadiness";
    public TimeSpan MaximumCatalogAge { get; init; }=TimeSpan.FromHours(12);
    public TimeSpan DatabaseTimeout { get; init; }=TimeSpan.FromSeconds(3);
    public TimeSpan AllowedClockSkew { get; init; }=TimeSpan.FromMinutes(5);
    public bool IsValid()=>MaximumCatalogAge>TimeSpan.Zero&&DatabaseTimeout>TimeSpan.Zero&&AllowedClockSkew>=TimeSpan.Zero&&AllowedClockSkew<MaximumCatalogAge;
}
