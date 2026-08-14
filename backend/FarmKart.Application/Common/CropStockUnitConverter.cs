using FarmKart.Domain.Enums;

namespace FarmKart.Application.Common;

public static class CropStockUnitConverter
{
    public static MeasurementUnit Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "kilogram" or "kilograms" or "kg" => MeasurementUnit.Kilogram,
        "quintal" or "quintals" or "qtl" => MeasurementUnit.Quintal,
        "ton" or "tons" or "tonne" or "tonnes" => MeasurementUnit.Ton,
        null or "" => throw new ArgumentException("A stock unit is required."),
        _ => throw new ArgumentException("Invalid stock unit. Supported units: Kilogram (Kg), Quintal, Ton.")
    };

    public static decimal ToKilograms(decimal quantity, MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Kilogram => quantity,
        MeasurementUnit.Quintal => quantity * 100m,
        MeasurementUnit.Ton => quantity * 1000m,
        _ => throw new ArgumentException("Invalid stock unit.")
    };

    public static string Format(MeasurementUnit unit) => unit switch
    {
        MeasurementUnit.Kilogram => "Kg",
        MeasurementUnit.Quintal => "Quintal",
        MeasurementUnit.Ton => "Ton",
        _ => unit.ToString()
    };
}
