namespace Buscarro.Infrastructure.Contracts;

public sealed class OpenAiVehicleSearchCriteriaContract
{
    public List<string>? Providers { get; set; }
    public List<string>? States { get; set; }
    public List<string>? Cities { get; set; }
    public List<string>? Manufacturers { get; set; }
    public List<string>? Models { get; set; }
    public List<string>? Versions { get; set; }
    public List<string>? Colors { get; set; }
    public List<string>? Gears { get; set; }
    public List<string>? Fuels { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public int? YearMin { get; set; }
    public int? YearMax { get; set; }
}
