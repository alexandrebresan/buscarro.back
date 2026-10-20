using Buscarro.Application.Exceptions;

namespace Buscarro.Application.Services;

public static class VehicleSearchAdditionalFilterValidator
{
    public static void ValidateMileage(int? minimum, int? maximum)
    {
        if (minimum < 0 || maximum < 0 || minimum > maximum)
            throw new ValidationException("mileage deve ser nao negativa e o minimo nao pode exceder o maximo.");
    }

    public static List<int> CanonicalizeDoors(IEnumerable<int> values)
    {
        var doors = values.ToArray();
        if (doors.Length > 20 || doors.Any(value => value is < 1 or > 6))
            throw new ValidationException("doors deve conter de 1 a 6 portas e no maximo 20 valores.");
        return doors.Distinct().Order().ToList();
    }

    public static string? CanonicalizeOrder(string? value)
    {
        var order = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        if (order is not (null or "price" or "price-inverse" or "year" or "year-inverse" or "mileage" or "mileage-inverse"))
            throw new ValidationException("order invalida.");
        return order;
    }
}
