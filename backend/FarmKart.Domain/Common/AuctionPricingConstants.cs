namespace FarmKart.Domain.Common;

public static class AuctionPricingConstants
{
    public const decimal KgPerMan = 20m;

    public static decimal ConvertKgToMan(decimal quantityInKg)
    {
        return quantityInKg / KgPerMan;
    }
}
