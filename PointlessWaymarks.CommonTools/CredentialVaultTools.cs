using GitCredentialManager;
using Polly;
using Serilog;

namespace PointlessWaymarks.CommonTools;

public static class CredentialVaultTools
{
    public static (string username, string password) GetCredentials(string credentialNamespace, string service,
        string account)
    {
        var vault = CredentialManager.Create(credentialNamespace);

        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();

        ICredential? possibleCredentials;

        try
        {
            possibleCredentials = pipeline.Execute(() => vault.Get(service, account));
        }
        catch (Exception e)
        {
            //Log if apparently not just a not found error - but either way just return null since
            //the credential can't currently be retrieved.
            if (!e.Message.Contains("element not found", StringComparison.OrdinalIgnoreCase))
                Log.ForContext(nameof(credentialNamespace), credentialNamespace)
                    .ForContext(nameof(service), service)
                    .ForContext(nameof(account), account)
                    .Error(e, "Error in PasswordVaultTools - GetCredentials");
            possibleCredentials = null;
        }

        if (possibleCredentials == null) return (string.Empty, string.Empty);

        return (possibleCredentials.Account, possibleCredentials.Password);
    }

    /// <summary>
    ///     Removes all Credentials associated with the namespace and service
    /// </summary>
    public static void RemoveCredentials(string credentialNamespace, string service)
    {
        var vault = CredentialManager.Create(credentialNamespace);

        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();

        List<string> accountsList;

        try
        {
            accountsList = pipeline.Execute(() => vault.GetAccounts(service).ToList());
        }
        catch (Exception e)
        {
            //Nothing to remove
            if (e.Message.Contains("element not found", StringComparison.OrdinalIgnoreCase)) return;

            //Error
            Log.ForContext(nameof(credentialNamespace), credentialNamespace)
                .ForContext(nameof(service), service)
                .Error(e, "Error in PasswordVaultTools - RemoveCredentials");
            throw;
        }

        if (!accountsList.Any()) return;

        accountsList.ToList().ForEach(x => vault.Remove(service, x));
    }

    /// <summary>
    ///     Removes the Credential associated with the credentialNamespace, service and account
    /// </summary>
    public static void RemoveCredentials(string credentialNamespace, string service, string account)
    {
        var vault = CredentialManager.Create(credentialNamespace);

        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();

        try
        {
            pipeline.Execute(() => vault.Remove(service, account));
        }
        catch (Exception e)
        {
            //Nothing to remove
            if (e.Message.Contains("element not found", StringComparison.OrdinalIgnoreCase)) return;

            //Error
            Log.ForContext(nameof(credentialNamespace), credentialNamespace)
                .ForContext(nameof(service), service)
                .ForContext(nameof(account), account)
                .Error(e, "Error in PasswordVaultTools - RemoveCredentials");
            throw;
        }
    }

    /// <summary>
    ///     Removes any existing Credentials Associated with the resourceIdentifier and then saves the new credentials
    /// </summary>
    /// <param name="account"></param>
    /// <param name="password"></param>
    /// <param name="credentialNamespace"></param>
    /// <param name="service"></param>
    public static void SaveCredentials(string credentialNamespace, string service, string account, string password)
    {
        var vault = CredentialManager.Create(credentialNamespace);

        var pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(5))
            .Build();

        //An error will throw on timeout
        pipeline.Execute(() => vault.AddOrUpdate(service, account, password));
    }
}