using CommunityToolkit.Mvvm.Messaging;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.AppMessages;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.WpfCommon.FileBasedGeoTagger;

[NotifyPropertyChanged]
public partial class FileBasedGeoTaggerWindow
{
    public FileBasedGeoTaggerWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public bool CloseAfterWrite { get; set; }
    public required FileBasedGeoTaggerContext FileBasedGeoTaggerContent { get; set; }
    public required StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; } = "File Based GeoTagger";

    public static async Task<FileBasedGeoTaggerWindow> CreateInstance(List<string>? initialFilesToTag = null,
        bool closeAfterWrite = false)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var statusContext = await StatusControlContext.CreateInstance();
        var context = await FileBasedGeoTaggerContext.CreateInstance(statusContext, null);

        var window = new FileBasedGeoTaggerWindow
        {
            StatusContext = statusContext,
            FileBasedGeoTaggerContent = context,
            CloseAfterWrite = closeAfterWrite
        };

        context.FilesWritten += window.OnFilesWritten;

        if (initialFilesToTag is { Count: > 0 } && context.FilesToTagFileList != null)
            statusContext.RunFireAndForgetNonBlockingTask(async () =>
            {
                await context.FilesToTagFileList.AddFilesToTag(initialFilesToTag);
            });

        return window;
    }

    private void OnFilesWritten(object? sender, List<string> writtenFiles)
    {
        WeakReferenceMessenger.Default.Send(
            new FileMetadataLocationUpdateMessage((this, writtenFiles)));

        if (CloseAfterWrite)
            StatusContext.RunFireAndForgetNonBlockingTask(async () =>
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                Close();
            });
    }
}