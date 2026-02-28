using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.SpatialTools;

public static class SpatialValueValidations
{
    public static Task<IsValid> BearingValidation(double? bearing)
    {
        if (bearing == null) return Task.FromResult(new IsValid(true, "Null Bearing is Valid"));

        if (bearing is < 0 or >= 360)
            return Task.FromResult(new IsValid(false,
                $"Bearings are limited 0-359 - {bearing} was input..."));

        return Task.FromResult(new IsValid(true, "Bearing is Valid"));
    }

    public static Task<IsValid> ElevationValidation(double? elevation)
    {
        if (elevation == null) return Task.FromResult(new IsValid(true, "Null Elevation is Valid"));

        if (elevation > 29500)
            return Task.FromResult(new IsValid(false,
                $"Elevations are limited to the elevation of Mount Everest - 29,092' above sea level - {elevation} was input..."));

        if (elevation < -50000)
            return Task.FromResult(new IsValid(false,
                $"This is very unlikely to be a valid elevation, this exceeds the depth of the Mariana Trench and known Extended-Reach Drilling (as of 2020) - elevations under -50,000' are not considered valid - {elevation} was input..."));

        return Task.FromResult(new IsValid(true, "Elevation is Valid"));
    }

    public static bool IsApproximatelyEqualTo(this double initialValue, double value,
        double maximumDifferenceAllowed)
    {
        // Handle comparisons of floating point values that may not be exactly the same
        return Math.Abs(initialValue - value) < maximumDifferenceAllowed;
    }

    public static Task<IsValid> LatitudeValidation(double latitude)
    {
        if (latitude is > 90 or < -90)
            return Task.FromResult(new IsValid(false,
                $"Latitude on Earth must be between -90 and 90 - {latitude} is not valid."));

        return Task.FromResult(new IsValid(true, "Latitude is Valid"));
    }

    public static async Task<IsValid> LatitudeValidationWithNullOk(double? latitude)
    {
        if (latitude == null) return new IsValid(true, "No Latitude is Ok...");

        return await LatitudeValidation(latitude.Value);
    }

    public static Task<IsValid> LongitudeValidation(double longitude)
    {
        if (longitude is > 180 or < -180)
            return Task.FromResult(new IsValid(false,
                $"Longitude on Earth must be between -180 and 180 - {longitude} is not valid."));

        return Task.FromResult(new IsValid(true, "Longitude is Valid"));
    }

    public static async Task<IsValid> LongitudeValidationWithNullOk(double? longitude)
    {
        if (longitude == null) return new IsValid(true, "No Longitude is Ok...");

        return await LongitudeValidation(longitude.Value);
    }

    public static bool IsApproximatelyEqualTo(this double? initialValue, double? value,
        double maximumDifferenceAllowed)
    {
        if (initialValue == null && value == null) return true;
        if (initialValue != null && value == null) return false;
        if (initialValue == null /*&& value != null*/) return false;
        // ReSharper disable PossibleInvalidOperationException Checked above
        return initialValue.Value.IsApproximatelyEqualTo(value!.Value, maximumDifferenceAllowed);
        // ReSharper restore PossibleInvalidOperationException
    }
}