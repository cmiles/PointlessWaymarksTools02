using PointlessWaymarks.WindowsTools;

namespace PointlessWaymarks.WpfCommon.GeoNamesControl;

public static class GeoNamesApiCredentials
{
    /// <summary>
    ///     Returns the Credential Manager Resource Key for the current settings file for GEONAMES Site credentials
    /// </summary>
    /// <returns></returns>
    public static string GeoNamesSiteCredentialResourceString(Guid? settingId)
    {
        return $"Pointless Waymarks CMS - GeoNames Username - {(settingId is null ? "Default" : settingId.ToString())}";
    }

    /// <summary>
    ///     Retrieves the GEONAMES Credentials associated with this settings file
    /// </summary>
    /// <returns></returns>
    public static string GetGeoNamesSiteCredentials(Guid? settingId)
    {
        if (settingId is not null)
        {
            var siteSettings = PasswordVaultTools.GetCredentials(GeoNamesSiteCredentialResourceString(settingId))
                .password;

            if (!string.IsNullOrWhiteSpace(siteSettings)) return siteSettings;
        }

        return PasswordVaultTools.GetCredentials(GeoNamesSiteCredentialResourceString(null)).password;
    }

    /// <summary>
    ///     Removes all GEONAMES Credentials associated with this settings file
    /// </summary>
    public static void RemoveGeoNamesSiteCredentials(Guid? settingId)
    {
        PasswordVaultTools.RemoveCredentials(GeoNamesSiteCredentialResourceString(settingId));
    }

    /// <summary>
    ///     Saves any existing GeoNames Credentials Associated with this settings file and Saves new Credentials
    /// </summary>
    /// <param name="username"></param>
    /// <param name="settingId"></param>
    public static void SaveGeoNamesSiteCredential(string username, Guid? settingId)
    {
        PasswordVaultTools.SaveCredentials(GeoNamesSiteCredentialResourceString(settingId), "GeoNamesApiUserName",
            username);
    }
}