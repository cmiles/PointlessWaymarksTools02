using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timer = System.Timers.Timer;

namespace PointlessWaymarks.UnoCommon.AppToast;

public partial class AppToastContext : ObservableObject
{
    private readonly Timer _toastDisposalTimer;

    public AppToastContext() : this([])
    {
    }

    public AppToastContext(ObservableCollection<AppToastMessage> initialItems)
    {
        _items = initialItems;
        _toastDisposalTimer = new Timer(1000);
        _toastDisposalTimer.Elapsed += async (_, _) => await HandleToastDisposalTimer();
        _disposeToastCommand = new AsyncRelayCommand<AppToastMessage>(DisposeToast);
    }

    [ObservableProperty] private AsyncRelayCommand<AppToastMessage> _disposeToastCommand;
    [ObservableProperty] private ObservableCollection<AppToastMessage> _items;

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
        if (toDispose is null) return;

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
        return new AppToastContext();
    }

    public async Task Show(string? toastText, ToastType toastType, bool userMustDismiss = false)
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        Items.Add(new AppToastMessage
        {
            Message = toastText ?? string.Empty,
            AddedOn = DateTime.Now,
            MessageType = toastType,
            UserMustDismiss = userMustDismiss
        });
        if (!_toastDisposalTimer.Enabled) _toastDisposalTimer.Start();
    }
}
