namespace Buscarro.Application.DTOs;

public sealed class VehicleSearchCriteria
{
    public List<string> Providers { get; set; } = [];

    public List<string> Countries { get; set; } = [];

    public List<string> States { get; set; } = [];

    public List<string> Cities { get; set; } = [];

    public List<string> Manufacturers { get; set; } = [];

    public List<string> Models { get; set; } = [];

    public List<string> Versions { get; set; } = [];

    public List<string> Colors { get; set; } = [];

    public List<string> Gears { get; set; } = [];

    public List<string> Fuels { get; set; } = [];

    public string? Query { get; set; }

    public string? Order { get; set; }

    public int Page { get; set; } = 1;

    public int Results { get; set; } = 20;

    public decimal? PriceMin { get; set; }

    public decimal? PriceMax { get; set; }

    public int? YearMin { get; set; }

    public int? YearMax { get; set; }

    public int? MileageMin { get; set; }
    public int? MileageMax { get; set; }
    public List<int> Doors { get; set; } = [];
    public List<string> Profiles { get; set; } = [];
}