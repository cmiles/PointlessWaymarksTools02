using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PointlessWaymarks.AvaloniaLlamaAspects;
using Timer = System.Timers.Timer;

namespace PointlessWaymarks.AvaloniaCommon.AppToast;

[NotifyPropertyChanged]
public partial class AppToastContext
{
    private readonly Timer _toastDisposalTimer;

    public AppToastContext(ObservableCollection<AppToastMessage> initialItems)
    {
        Items = initialItems;
        _toastDisposalTimer = new Timer(1000);
        _toastDisposalTimer.Elapsed += async (_, _) => await HandleToastDisposalTimer();
        DisposeToastCommand = new AsyncRelayCommand<AppToastMessage>(DisposeToast);
    }

    private AsyncRelayCommand<AppToastMessage> DisposeToastCommand { get; set; }

    public ObservableCollection<AppToastMessage> Items { get; set; }

    private async Task HandleToastDisposalTimer()
    {
        if (!Items.Any())
        {
            _toastDisposalTimer.Stop();
            return;
        }

        var toDispose = Items.Where(x => !x.UserMustDismiss && x.AddedOn.AddSeconds(3) < DateTime.Now)
            .OrderBy(x => x.AddedOn).ToList();

        if (!toDispose.Any()) return;

        await ThreadSwitcher.ResumeForegroundAsync();
        foreach (var loopToDispose in toDispose) Items.Remove(loopToDispose);
    }

    public async Task DisposeToast(AppToastMessage? toDispose)
    {
        if (toDispose == null) return;

        if (Items.Contains(toDispose))
            try
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                Items.Remove(toDispose);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
    }

    public static async Task<AppToastContext> CreateInstance()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        return new AppToastContext([]);
    }

    public async Task Show(string? toastText, ToastType toastType)
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        Items.Add(new AppToastMessage
            { Message = toastText ?? string.Empty, AddedOn = DateTime.Now, MessageType = toastType });
        if (!_toastDisposalTimer.Enabled) _toastDisposalTimer.Start();
    }
}