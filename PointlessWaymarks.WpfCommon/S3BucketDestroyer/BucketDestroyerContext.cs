using System.ComponentModel;
using System.IO;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.CommonTools.S3;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;
using PointlessWaymarks.WpfCommon.MarkdownDisplay;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.StringDropdownDataEntry;

namespace PointlessWaymarks.WpfCommon.S3BucketDestroyer;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class BucketDestroyerContext
{
    public readonly string HelpText = """
                                      ## Cloud Bucket Destroyer Help

                                      ### USING THIS WILL DESTROY YOUR DATA - YOU SHOULD PROBABLY NOT USE THIS

                                      If you use Cloud Buckets for any length of time you may eventually want to delete an entire bucket - to eliminate old data, move to a new provider, etc...

                                      Some providers may provide a convenient way to delete an entire bucket in their web interface - if they do you should  take advantage of that as it is likely the most efficient way to delete a bucket. This tool can help if the provider does not provide an easy way to delete a bucket.

                                      To use this tool you will need to enter your Cloud Credentials - the Access Key and Secret Key. If you are using a provider other than Amazon S3 you will also need to enter the Service URL for your provider.

                                      Once this information is entered then use the "Load Buckets" button to get a list of buckets in your account. Then select/confirm the bucket you want to delete and hit "Destroy Bucket".

                                      **THIS PROCESS CAN NOT BE UNDONE.**

                                      **All data in the bucket will be permanently deleted and can not be recovered.**

                                      This program will attempt to delete all items in the bucket before deleting the bucket itself. Depending on the number of items in the bucket this may take a long, long, time. Because the process deletes files as it goes interrupting the process will not undo any deletions that have already happened.
                                      """;

    public string AccessKey { get; set; } = string.Empty;

    public bool CloudCredentialsHaveValidationIssues { get; set; }

    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }
    public HelpDisplayContext? HelpContext { get; set; }
    public string SecretKey { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public required StatusControlContext StatusContext { get; set; }
    public required StringDropdownDataEntryContext UserAwsRegionEntry { get; set; }
    public required StringDropdownDataEntryContext UserCloudBucketEntry { get; set; }
    public required StringDropdownDataEntryContext UserCloudProviderEntry { get; set; }


    public void CheckForChangesAndValidationIssues()
    {
        HasChanges = PropertyScanners.ChildPropertiesHaveChanges(this);
        HasValidationIssues =
            PropertyScanners.ChildPropertiesHaveValidationIssues(this);
        CloudCredentialsCheckForValidationIssues();
    }

    private void CloudCredentialsCheckForValidationIssues()
    {
        CloudCredentialsHaveValidationIssues = string.IsNullOrWhiteSpace(SecretKey) ||
                                               string.IsNullOrWhiteSpace(AccessKey);

        if (UserCloudProviderEntry.UserValue != nameof(S3Providers.Amazon))
            CloudCredentialsHaveValidationIssues = CloudCredentialsHaveValidationIssues ||
                                                   string.IsNullOrWhiteSpace(ServiceUrl);
    }

    public static async Task<BucketDestroyerContext> CreateInstance(StatusControlContext? statusContext)
    {
        var factoryStatusContext = await StatusControlContext.CreateInstance(statusContext);

        await ThreadSwitcher.ResumeBackgroundAsync();

        var cloudBucketEntry = StringDropdownDataEntryContext.CreateInstance();
        cloudBucketEntry.Title = "Cloud Bucket";
        cloudBucketEntry.HelpText = "The S3/Cloud Bucket to DESTROY";
        cloudBucketEntry.ReferenceValue = string.Empty;
        cloudBucketEntry.Choices = [];
        cloudBucketEntry.ValidationFunctions =
        [
            x =>
            {
                if (string.IsNullOrWhiteSpace(x))
                    return new IsValid(false, "A Cloud Region is required for the job");

                return new IsValid(true, string.Empty);
            }
        ];


        var cloudProviderDataEntry = StringDropdownDataEntryContext.CreateInstance();
        cloudProviderDataEntry.Title = "Cloud Provider";
        cloudProviderDataEntry.HelpText = "The cloud provider for the job.";
        cloudProviderDataEntry.ReferenceValue = string.Empty;
        cloudProviderDataEntry.Choices = new List<string> { string.Empty }
            .Concat(Enum.GetNames(typeof(S3Providers)))
            .Select(x => new DropDownDataChoice { DisplayString = x, DataString = x }).ToList();


        var regionsDataEntry = StringDropdownDataEntryContext.CreateInstance();
        regionsDataEntry.Title = "Cloud Region";
        regionsDataEntry.HelpText = "The region of the S3 Bucket.";
        regionsDataEntry.ReferenceValue = string.Empty;
        regionsDataEntry.Choices = new DropDownDataChoice { DataString = "", DisplayString = "" }.AsList().Concat(
            RegionEndpoint.EnumerableAllRegions.Select(x => new DropDownDataChoice
                { DisplayString = x.SystemName, DataString = x.SystemName })).ToList();
        regionsDataEntry.ValidationFunctions =
        [
            x =>
            {
                if (cloudProviderDataEntry.UserValue == nameof(S3Providers.Amazon))
                    if (string.IsNullOrWhiteSpace(x))
                        return new IsValid(false, "A Cloud Region is required for the job");

                return new IsValid(true, string.Empty);
            }
        ];

        var toReturn = new BucketDestroyerContext
        {
            StatusContext = factoryStatusContext,
            UserAwsRegionEntry = regionsDataEntry,
            UserCloudProviderEntry = cloudProviderDataEntry,
            UserCloudBucketEntry = cloudBucketEntry
        };

        await toReturn.Setup();

        return toReturn;
    }

    [BlockingCommand]
    public async Task DestroyBucket(CancellationToken cancellationToken)
    {
        if (HasValidationIssues)
        {
            await StatusContext.ToastError("Fix all Validation Issues...");
            return;
        }

        if (string.IsNullOrWhiteSpace(UserCloudBucketEntry.UserValue))
        {
            await StatusContext.ToastError("Please select a Bucket...");
            return;
        }

        // Ask for user confirmation before proceeding with deletion
        var confirmationMessage = $"""
                                   WARNING: You are about to PERMANENTLY DELETE the bucket '{UserCloudBucketEntry.UserValue}' and ALL of its contents.

                                   This action CANNOT be undone and all data will be PERMANENTLY LOST.

                                   Are you absolutely sure you want to proceed?
                                   """;

        var confirmation = await StatusContext.ShowMessage("CONFIRM BUCKET DESTRUCTION",
            confirmationMessage,
            ["Yes, I understand - DELETE EVERYTHING", "Cancel"]);

        if (confirmation != "Yes, I understand - DELETE EVERYTHING")
        {
            await StatusContext.ToastWarning("Bucket deletion cancelled.");
            return;
        }

        var s3Credentials = GetS3AccountInformation(UserCloudBucketEntry.UserValue);
        var s3Client = s3Credentials.S3Client();

        try
        {
            // First, list all objects in the bucket
            StatusContext.Progress("Listing objects in the bucket...");

            var allObjects = await S3Tools.ListS3Items(s3Credentials, $"",
                StatusContext.ProgressTracker(), cancellationToken);

            //Sorted items as a quick way to delete the deepest items first
            var groupedItems = allObjects.GroupBy(x => x.Key).Select(x => x.Key).GroupBy(x => x.Count(y => y == '/'))
                .OrderByDescending(x => x.Key).ToList();

            var totalCount = groupedItems.Count;
            var groupLoopCount = 0;

            foreach (var loopDeletionGroups in groupedItems)
            {
                if (cancellationToken.IsCancellationRequested) throw new TaskCanceledException();

                StatusContext.Progress(
                    $"S3 Deletion Group {groupLoopCount} of {totalCount} - Depth {loopDeletionGroups.Key}");

                var sortedDeleteGroups = loopDeletionGroups.OrderByDescending(x => x.Length).Chunk(500).ToList();

                var loopCount = 0;

                foreach (var loopDeletes in sortedDeleteGroups)
                {
                    if (++loopCount % 10 == 0)
                        StatusContext.Progress(
                            $"  S3 Deletion - Section {loopCount} of {loopDeletes.Length} - Group {groupLoopCount} of {totalCount} -  Depth {loopDeletionGroups.Key}");
                    try
                    {
                        await s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                        {
                            BucketName = s3Credentials.BucketName(), Objects =
                                [..loopDeletes.Select(x => new KeyVersion { Key = x })]
                        }, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        StatusContext.Progress($"S3 Deletion Error - {loopDeletionGroups.Key} - {e.Message}");
                        throw;
                    }
                }
            }

            // Finally, delete the empty bucket
            StatusContext.Progress("Deleting the bucket...");
            await s3Client.DeleteBucketAsync(UserCloudBucketEntry.UserValue, cancellationToken);

            await StatusContext.ToastSuccess($"Successfully destroyed bucket '{UserCloudBucketEntry.UserValue}'!");

            // Refresh the bucket list
            await GetBuckets();
        }
        catch (AmazonS3Exception ex)
        {
            await StatusContext.ToastError($"Failed to delete bucket: {ex.Message}");
            StatusContext.Progress($"Error details: {ex}");
        }
        catch (Exception ex)
        {
            await StatusContext.ToastError($"An error occurred: {ex.Message}");
            StatusContext.Progress($"Error details: {ex}");
        }
    }

    [BlockingCommand]
    public async Task EnterCloudCredentials()
    {
        var newKeyEntry = await StatusContext.ShowStringEntry("Cloud Access Key",
            "Enter the Cloud Access Key", string.Empty);

        if (!newKeyEntry.Item1)
        {
            await StatusContext.ToastWarning("Cloud Credential Entry Cancelled");
            return;
        }

        AccessKey = newKeyEntry.Item2.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(AccessKey)) return;

        var newSecretEntry = await StatusContext.ShowStringEntry("Cloud Secret Key",
            "Enter the Secret Key", string.Empty);

        if (!newSecretEntry.Item1) return;

        SecretKey = newSecretEntry.Item2.TrimNullToEmpty();

        if (string.IsNullOrWhiteSpace(SecretKey))
        {
            await StatusContext.ToastError("Cloud Credential Entry Canceled - secret can not be blank");
            return;
        }

        if (UserCloudProviderEntry.UserValue != nameof(S3Providers.Amazon))
        {
            var serviceUrl = await StatusContext.ShowStringEntry("Service URL",
                "Enter the S3 service URL. For Cloudflare this will be https://{accountId}.r2.cloudflarestorage.com - other providers, like Wasabi, will have a Service URL based on region (for example s3.ca-central-1.wasabisys.com for Wasabi-Toronto)",
                string.Empty);

            if (!serviceUrl.Item1) return;

            ServiceUrl = serviceUrl.Item2.TrimNullToEmpty();

            if (string.IsNullOrWhiteSpace(ServiceUrl))
            {
                await StatusContext.ToastError("Cloud Credential Entry Canceled - Service URL can not be blank");
                return;
            }
        }

        CloudCredentialsCheckForValidationIssues();
    }

    [BlockingCommand]
    public async Task GetBuckets()
    {
        var amazonCredentials = GetS3AccountInformation();

        var connection = amazonCredentials.S3Client();

        var buckets = await connection.ListBucketsAsync();

        UserCloudBucketEntry.Choices = buckets.Buckets
            .Select(x => new DropDownDataChoice { DataString = x.BucketName, DisplayString = x.BucketName })
            .OrderBy(x => x.DisplayString)
            .Prepend(new DropDownDataChoice { DataString = string.Empty, DisplayString = string.Empty })
            .ToList();
        UserCloudBucketEntry.TrySetUserValue(string.Empty);
    }

    private S3AccountInformation GetS3AccountInformation(string bucketName = "")
    {
        var frozenNow = DateTime.Now;
        if (!Enum.TryParse(UserCloudProviderEntry.UserValue, out S3Providers provider))
            provider = S3Providers.Amazon;
        var serviceUrl = provider == S3Providers.Amazon
            ? S3Tools.AmazonServiceUrlFromBucketRegion(UserAwsRegionEntry.UserValue)
            : ServiceUrl;

        var amazonCredentials = new S3AccountInformation
        {
            AccessKey = () => AccessKey,
            Secret = () => SecretKey,
            ServiceUrl = () => serviceUrl,
            BucketName = () => bucketName,
            S3Provider = () => provider,
            FullFileNameForJsonUploadInformation = () =>
                Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
                    $"{frozenNow:yyyy-MM-dd-HH-mm}-BucketDestroyer.json"),
            FullFileNameForToExcel = () => Path.Combine(FileLocationTools.DefaultStorageDirectory().FullName,
                $"{frozenNow:yyyy-MM-dd-HH-mm}-BucketDestroyer.json")
        };
        return amazonCredentials;
    }

    public Task Setup()
    {
        BuildCommands();

        HelpContext = new HelpDisplayContext([HelpText]);

        CloudCredentialsCheckForValidationIssues();

        PropertyScanners.SubscribeToChildHasChangesAndHasValidationIssues(this,
            CheckForChangesAndValidationIssues);

        CheckForChangesAndValidationIssues();

        UserCloudProviderEntry.PropertyChanged += UserCloudProviderEntry_PropertyChanged;

        return Task.CompletedTask;
    }

    private void UserCloudProviderEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserCloudProviderEntry.SelectedItem))
        {
            CloudCredentialsCheckForValidationIssues();

            if (UserCloudProviderEntry.UserValue == nameof(S3Providers.Amazon))
                UserAwsRegionEntry.ValidationFunctions =
                [
                    x =>
                    {
                        if (string.IsNullOrWhiteSpace(x))
                            return new IsValid(false, "A Cloud Region is required for the job");
                        return new IsValid(true, string.Empty);
                    }
                ];
            else
                UserAwsRegionEntry.ValidationFunctions = [];
        }
    }
}