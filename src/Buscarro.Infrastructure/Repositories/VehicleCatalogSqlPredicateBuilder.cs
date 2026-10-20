using Buscarro.Application.DTOs;
using Buscarro.Application.Services;
using Npgsql;
using NpgsqlTypes;

namespace Buscarro.Infrastructure.Repositories;

internal sealed class VehicleCatalogSqlPredicateBuilder
{
    public VehicleCatalogSqlPredicate Build(NormalizedVehicleCatalogSearchCriteria criteria, int[] generationIds, string[] providers)
    {
        var clauses = new List<string> { "v.generation_id = ANY(@generation_ids)" };
        var parameters = new List<NpgsqlParameter>
        {
            new("generation_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = generationIds }
        };
        AddAny("v.provider", "providers_raw", providers, clauses, parameters);
        AddAny("v.state_normalized", "states", criteria.States, clauses, parameters);
        AddAny("v.city_normalized", "cities", criteria.Cities, clauses, parameters);
        AddAny("v.brand_normalized", "manufacturers", criteria.Manufacturers, clauses, parameters);
        AddAny("v.model_normalized", "models", criteria.Models, clauses, parameters);
        AddAny("v.version_normalized", "versions", criteria.Versions, clauses, parameters);
        AddAny("v.color_normalized", "colors", criteria.Colors, clauses, parameters);
        AddAny("v.fuel_normalized", "fuels", criteria.Fuels, clauses, parameters);
        AddAny("v.transmission_normalized", "gears", criteria.Gears, clauses, parameters);
        AddRange("v.price", "price_min", criteria.PriceMin, ">=", clauses, parameters);
        AddRange("v.price", "price_max", criteria.PriceMax, "<=", clauses, parameters);
        AddRange("v.year", "year_min", criteria.YearMin, ">=", clauses, parameters);
        AddRange("v.year", "year_max", criteria.YearMax, "<=", clauses, parameters);
        AddRange("v.mileage", "mileage_min", criteria.MileageMin, ">=", clauses, parameters);
        AddRange("v.mileage", "mileage_max", criteria.MileageMax, "<=", clauses, parameters);
        if (criteria.Doors.Count > 0)
        {
            clauses.Add("v.doors = ANY(@doors)");
            parameters.Add(new("doors", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = criteria.Doors.ToArray() });
        }
        foreach (var code in criteria.Profiles)
        {
            var models = SemanticVehicleModelCatalog.GetRequired(code);
            var index = clauses.Count;
            var brandsParameter = $"profile_brands_{index}";
            var modelsParameter = $"profile_models_{index}";
            clauses.Add($"EXISTS (SELECT 1 FROM unnest(@{brandsParameter}::text[], @{modelsParameter}::text[]) AS p(brand, model) WHERE v.brand_normalized = p.brand AND v.model_normalized = p.model)");
            parameters.Add(new(brandsParameter, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = models.Select(model => model.Brand).ToArray() });
            parameters.Add(new(modelsParameter, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = models.Select(model => model.Model).ToArray() });
        }
        if (criteria.Query is not null)
        {
            clauses.Add("v.search_vector @@ websearch_to_tsquery('simple', @query_normalized)");
            parameters.Add(new("query_normalized", NpgsqlDbType.Text) { Value = criteria.Query });
        }
        return new(string.Join(" AND ", clauses), parameters);
    }

    public static string OrderBy(string? order) => order switch
    {
        "price" => "v.price ASC NULLS LAST, v.id ASC",
        "price-inverse" => "v.price DESC NULLS LAST, v.id ASC",
        "year" => "v.year ASC NULLS LAST, v.id ASC",
        "year-inverse" => "v.year DESC NULLS LAST, v.id ASC",
        "mileage" => "v.mileage ASC NULLS LAST, v.id ASC",
        "mileage-inverse" => "v.mileage DESC NULLS LAST, v.id ASC",
        _ => "row_number() OVER (PARTITION BY v.provider ORDER BY v.id), lower(v.provider), v.provider"
    };

    private static void AddAny(string column, string name, IReadOnlyCollection<string> values, List<string> clauses, List<NpgsqlParameter> parameters)
    {
        if (values.Count == 0) return;
        clauses.Add($"{column} = ANY(@{name})");
        parameters.Add(new(name, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = values.ToArray() });
    }
    private static void AddRange<T>(string column, string name, T? value, string operation, List<string> clauses, List<NpgsqlParameter> parameters) where T : struct
    {
        if (!value.HasValue) return;
        clauses.Add($"{column} {operation} @{name}");
        parameters.Add(new(name, value.Value));
    }
}

internal sealed record VehicleCatalogSqlPredicate(string WhereSql, IReadOnlyList<NpgsqlParameter> Parameters);
