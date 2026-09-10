using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PointlessWaymarks.UnoCommon.AppToast;
using PointlessWaymarks.UnoCommon.Utility;
using Serilog;

namespace PointlessWaymarks.UnoCommon.Status;

public partial class StatusControlContext : ObservableObject
{
    private int _countOfRunningBlockingTasks;
    private int _countOfRunningNonBlockingTasks;

    public StatusControlContext()
        : this(new AppToastContext())
    {
    }

    public StatusControlContext(AppToastContext toastContext)
    {
        ContextDispatcher = ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();

        Toast = toastContext;
        StatusLog = [];
        CancellationList = [];

        UserMessageBoxResponseCommand = new RelayCommand<string>(UserMessageBoxResponse);
        UserStringEntryApprovedResponseCommand = new RelayCommand(UserStringEntryApprovedResponse);
        UserStringEntryCancelledResponseCommand = new RelayCommand(UserStringEntryCanceledResponse);
    }

    [ObservableProperty] private bool _blockUi;
    [ObservableProperty] private ObservableCollection<UserCancellations> _cancellationList;
    [ObservableProperty] private DispatcherQueue? _contextDispatcher;
    [ObservableProperty] private CancellationTokenSource? _currentFullScreenCancellationSource;
    [ObservableProperty] private List<StatusControlMessageButton>? _messageBoxButtonList;
    [ObservableProperty] private string? _messageBoxMessage;
    [ObservableProperty] private string _messageBoxTitle = string.Empty;
    [ObservableProperty] private bool _messageBoxVisible;
    [ObservableProperty] private bool _nonBlockingTaskAreRunning;
    [ObservableProperty] private bool _showCancellations;
    [ObservableProperty] private string? _showMessageResponse;
    public Guid StatusControlContextId { get; } = Guid.NewGuid();
    [ObservableProperty] private ObservableCollection<string> _statusLog;
    [ObservableProperty] private bool _stringEntryApproved;
    [ObservableProperty] private string _stringEntryMessage = string.Empty;
    [ObservableProperty] private string _stringEntryTitle = string.Empty;
    [ObservableProperty] private string _stringEntryUserText = string.Empty;
    [ObservableProperty] private bool _stringEntryVisible;
    [ObservableProperty] private AppToastContext _toast;
    [ObservableProperty] private RelayCommand<string> _userMessageBoxResponseCommand;
    [ObservableProperty] private RelayCommand _userStringEntryApprovedResponseCommand;
    [ObservableProperty] private RelayCommand _userStringEntryCancelledResponseCommand;

    public static async Task<StatusControlContext> CreateInstance(StatusControlContext? statusContext = null)
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        return statusContext ?? new StatusControlContext();
    }

    private async Task BlockTaskCompleted(Task obj)
    {
        DecrementBlockingTasks();

        if (obj.IsCanceled)
        {
            await ToastWarning("Canceled Task");
            return;
        }

        if (obj.IsFaulted)
        {
            await ToastError($"Error: {FirstNonSeeInnerMessage(obj.Exception ?? new Exception("Unknown Error"))}");
            Log.Error(obj.Exception, "BlockTaskCompleted Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    private async Task BlockTaskCompleted(Task obj, CancellationTokenSource cancellationSource)
    {
        DecrementBlockingTasks();

        var toRemove = CancellationList.Where(x => x.CancelSource == cancellationSource).ToList();

        if (toRemove.Any())
        {
            var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
            if (dispatcher != null && !dispatcher.HasThreadAccess)
            {
                dispatcher.TryEnqueue(() =>
                {
                    toRemove.ForEach(x => CancellationList.Remove(x));
                    ShowCancellations = CancellationList.Any();
                });
            }
            else
            {
                toRemove.ForEach(x => CancellationList.Remove(x));
                ShowCancellations = CancellationList.Any();
            }
        }

        cancellationSource.Dispose();

        if (obj.IsCanceled)
        {
            await ToastWarning("Canceled Task");
            return;
        }

        if (obj.IsFaulted)
        {
            await ToastError($"Error: {FirstNonSeeInnerMessage(obj.Exception)}");
            Log.Error(obj.Exception, "BlockTaskCompleted Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    private void DecrementBlockingTasks()
    {
        Interlocked.Decrement(ref _countOfRunningBlockingTasks);
        var isBlocked = _countOfRunningBlockingTasks > 0;
        var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => BlockUi = isBlocked);
        }
        else
        {
            BlockUi = isBlocked;
        }
    }

    private void DecrementNonBlockingTasks()
    {
        Interlocked.Decrement(ref _countOfRunningNonBlockingTasks);
        var isRunning = _countOfRunningNonBlockingTasks > 0;
        var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => NonBlockingTaskAreRunning = isRunning);
        }
        else
        {
            NonBlockingTaskAreRunning = isRunning;
        }
    }

    private async void FireAndForgetBlockingTaskCompleted(Task obj)
    {
        DecrementBlockingTasks();

        if (obj.IsCanceled) return;

        if (obj.IsFaulted)
        {
            await ShowMessageWithOkButton("Error", FirstNonSeeInnerMessage(obj.Exception));

            Log.Error(obj.Exception, "FireAndForgetBlockingTaskCompleted Exception - Status Context Id: {ContextId}", StatusControlContextId);
        }
    }

    private async Task FireAndForgetNonBlockingTaskCompleted(Task obj)
    {
        DecrementNonBlockingTasks();

        if (obj.IsCanceled) return;

        if (obj.IsFaulted)
        {
            await ToastError($"Error: {FirstNonSeeInnerMessage(obj.Exception)}");

            Log.Error(obj.Exception,
                "FireAndForgetNonBlockingTaskCompleted Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    private async Task FireAndForgetWithToastOnErrorCompleted(Task obj)
    {
        if (obj.IsCanceled) return;

        if (obj.IsFaulted)
        {
            await ToastError($"Error: {FirstNonSeeInnerMessage(obj.Exception)}");

            Log.Error(obj.Exception,
                "FireAndForgetWithToastOnErrorCompleted Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    private static string FirstNonSeeInnerMessage(Exception? exception)
    {
        if (exception == null) return string.Empty;

        if (!exception.Message.Contains("inner exception", StringComparison.OrdinalIgnoreCase))
            return exception.Message;

        if (exception.InnerException == null) return exception.Message;

        var loopCounter = 0;
        var loopException = exception.InnerException;

        while (loopCounter < 20)
        {
            if (!loopException.Message.Contains("inner exception", StringComparison.OrdinalIgnoreCase))
                return loopException.Message;
            if (exception.InnerException == null) return exception.Message;

            loopException = exception.InnerException;
            loopCounter++;
        }

        return exception.Message;
    }

    private void IncrementBlockingTasks()
    {
        Interlocked.Increment(ref _countOfRunningBlockingTasks);
        var isBlocked = _countOfRunningBlockingTasks > 0;
        var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => BlockUi = isBlocked);
        }
        else
        {
            BlockUi = isBlocked;
        }
    }

    private void IncrementNonBlockingTasks()
    {
        Interlocked.Increment(ref _countOfRunningNonBlockingTasks);
        var isRunning = _countOfRunningNonBlockingTasks > 0;
        var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => NonBlockingTaskAreRunning = isRunning);
        }
        else
        {
            NonBlockingTaskAreRunning = isRunning;
        }
    }

    private async Task NonBlockTaskCompleted(Task obj)
    {
        DecrementNonBlockingTasks();

        if (obj.IsCanceled)
        {
            await ToastWarning("Canceled Task");
            return;
        }

        if (obj.IsFaulted)
        {
            await ToastError($"Error: {FirstNonSeeInnerMessage(obj.Exception)}");

            Log.Error(obj.Exception, "NonBlockTaskCompleted Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    public void Progress(string e)
    {
        var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() =>
            {
                StatusLog.Add(e);
                if (StatusLog.Count > 100) StatusLog.Remove(StatusLog.First());
                Task.Run(() => Log.Information("Progress: {0} - Status Context Id: {1}", e, StatusControlContextId));
            });
        }
        else
        {
            StatusLog.Add(e);
            if (StatusLog.Count > 100) StatusLog.Remove(StatusLog.First());
            Task.Run(() => Log.Information("Progress: {0} - Status Context Id: {1}", e, StatusControlContextId));
        }
    }

    public IProgress<string> ProgressTracker()
    {
        var toReturn = new Progress<string>();
        toReturn.ProgressChanged += ProgressTrackerChange;
        return toReturn;
    }

    private void ProgressTrackerChange(object? sender, string e)
    {
        Progress(e);
    }

    public void RunBlockingAction(Action toRun)
    {
        RunBlockingTask(() => Task.Run(toRun));
    }

    public void RunBlockingAction<T>(Action<T> toRun, T parameter)
    {
        RunBlockingTask(() => Task.Run(() => toRun(parameter)));
    }

    public RelayCommand RunBlockingActionCommand(Action toRun)
    {
        return new RelayCommand(() => RunBlockingAction(toRun));
    }

    public void RunBlockingTask(Func<Task> toRun)
    {
        IncrementBlockingTasks();
        Task.Run(toRun).ContinueWith(BlockTaskCompleted);
    }

    public void RunBlockingTask<T>(Func<T, Task> toRun, T parameter)
    {
        IncrementBlockingTasks();
        Task.Run(async () => await toRun(parameter)).ContinueWith(BlockTaskCompleted);
    }

    public RelayCommand<T> RunBlockingTaskCommand<T>(Func<T?, Task> toRun)
    {
        return new RelayCommand<T>(x => RunBlockingTask(async () => await toRun(x)));
    }

    public RelayCommand RunBlockingTaskCommand(Func<Task> toRun)
    {
        return new RelayCommand(() => RunBlockingTask(toRun));
    }

    public void RunBlockingTaskWithCancellation(Func<CancellationToken, Task> toRun, string cancelDescription)
    {
        IncrementBlockingTasks();
        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;

        var userCancellation = new UserCancellations { CancelSource = tokenSource, Description = cancelDescription };
        var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() =>
            {
                CancellationList.Add(userCancellation);
                ShowCancellations = CancellationList.Any();
            });
        }
        else
        {
            CancellationList.Add(userCancellation);
            ShowCancellations = CancellationList.Any();
        }

        // ReSharper disable once MethodSupportsCancellation No token for final cancellation
        Task.Run(async () => await toRun(token), token).ContinueWith(x => BlockTaskCompleted(x, tokenSource));
    }

    public void RunBlockingTaskWithCancellation<T>(Func<T?, CancellationToken, Task> toRun, T parameter,
        string cancelDescription)
    {
        IncrementBlockingTasks();
        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;

        var userCancellation = new UserCancellations { CancelSource = tokenSource, Description = cancelDescription };
        var dispatcher = ContextDispatcher ?? ThreadSwitcher.PinnedDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() =>
            {
                CancellationList.Add(userCancellation);
                ShowCancellations = CancellationList.Any();
            });
        }
        else
        {
            CancellationList.Add(userCancellation);
            ShowCancellations = CancellationList.Any();
        }

        // ReSharper disable once MethodSupportsCancellation No token for final cancellation
        Task.Run(async () => await toRun(parameter, token), token)
            .ContinueWith(x => BlockTaskCompleted(x, tokenSource));
    }

    public RelayCommand<T> RunBlockingTaskWithCancellationCommand<T>(Func<T?, CancellationToken, Task> toRun,
        string cancelDescription)
    {
        return new RelayCommand<T>(x => RunBlockingTaskWithCancellation(toRun, x, cancelDescription));
    }

    public RelayCommand RunBlockingTaskWithCancellationCommand(Func<CancellationToken, Task> toRun,
        string cancelDescription)
    {
        return new RelayCommand(() => RunBlockingTaskWithCancellation(toRun, cancelDescription));
    }

    public void RunFireAndForgetBlockingTask(Func<Task> toRun)
    {
        try
        {
            IncrementBlockingTasks();
            Task.Run(async () => await toRun()).ContinueWith(FireAndForgetBlockingTaskCompleted);
        }
        catch (Exception e)
        {
            ShowMessageWithOkButton("Error", e.ToString()).Wait();
            DecrementBlockingTasks();

            Task.Run(() => Log.Error(e, "RunFireAndForgetBlockingTask Exception - Status Context Id: {ContextId}",
                StatusControlContextId));
        }
    }

    public void RunFireAndForgetNonBlockingTask(Func<Task> toRun)
    {
        try
        {
            IncrementNonBlockingTasks();
            Task.Run(async () => await toRun()).ContinueWith(FireAndForgetNonBlockingTaskCompleted);
        }
        catch (Exception e)
        {
            DecrementNonBlockingTasks();
            _ = ToastError($"Error: {FirstNonSeeInnerMessage(e)}");

            Log.Error(e, "RunFireAndForgetNonBlockingTask Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    public void RunFireAndForgetNonBlockingAction(Action toRun)
    {
        try
        {
            IncrementNonBlockingTasks();
            Task.Run(toRun);
        }
        catch (Exception e)
        {
            DecrementNonBlockingTasks();
            _ = ToastError($"Error: {FirstNonSeeInnerMessage(e)}");

            Log.Error(e, "RunFireAndForgetNonBlockingTask Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    public void RunFireAndForgetWithToastOnError(Func<Task> toRun)
    {
        try
        {
            Task.Run(async () => await toRun()).ContinueWith(FireAndForgetWithToastOnErrorCompleted);
        }
        catch (Exception e)
        {
            DecrementNonBlockingTasks();
            _ = ToastError($"Error: {FirstNonSeeInnerMessage(e)}");

            Log.Error(e, "RunFireAndForgetWithToastOnError Exception - Status Context Id: {ContextId}",
                StatusControlContextId);
        }
    }

    public void RunNonBlockingAction(Action toRun)
    {
        RunNonBlockingTask(() => Task.Run(toRun));
    }

    public RelayCommand RunNonBlockingActionCommand(Action toRun)
    {
        return new RelayCommand(() => RunNonBlockingAction(toRun));
    }

    public void RunNonBlockingTask(Func<Task> toRun)
    {
        IncrementNonBlockingTasks();
        Task.Run(toRun).ContinueWith(NonBlockTaskCompleted);
    }

    public RelayCommand<T> RunNonBlockingTaskCommand<T>(Func<T?, Task> toRun)
    {
        return new RelayCommand<T>(x => RunBlockingTask(async () => await toRun(x)));
    }

    public RelayCommand RunNonBlockingTaskCommand(Func<Task> toRun)
    {
        return new RelayCommand(() => RunNonBlockingTask(toRun));
    }

    public async Task<string> ShowMessage(string title, string? body, List<string> buttons)
    {
        if (buttons.Any() != true) buttons = ["Ok"];

        return await ShowMessage(title, body,
            buttons.Select(x => new StatusControlMessageButton { MessageText = x }).ToList());
    }

    public async Task<string> ShowMessage(string title, string? body, List<StatusControlMessageButton>? buttons)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (buttons == null || !buttons.Any())
            buttons = [new StatusControlMessageButton { IsDefault = true, MessageText = "Ok" }];

        if (buttons.All(x => !x.IsDefault) || buttons.Count(x => x.IsDefault) > 1)
        {
            buttons.ForEach(x => x.IsDefault = false);
            buttons.First().IsDefault = true;
        }

        MessageBoxTitle = title;
        MessageBoxMessage = body;
        MessageBoxButtonList = buttons;
        MessageBoxVisible = true;

        Log.ForContext("MessageBoxTitle", title).ForContext("MessageBoxMessage", body)
            .ForContext("MessageBoxButtonList", buttons).ForContext("StatusControlContextId", StatusControlContextId)
            .Information("StatusControlContext Showing Message Box");

        await ThreadSwitcher.ResumeBackgroundAsync();

        CurrentFullScreenCancellationSource = new CancellationTokenSource();

        try
        {
            await CurrentFullScreenCancellationSource.Token.WhenCancelled();
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException) Progress($"ShowMessage Exception {e.Message}");
        }
        finally
        {
            CurrentFullScreenCancellationSource.Dispose();
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var toReturn = ShowMessageResponse ?? string.Empty;

        MessageBoxTitle = string.Empty;
        MessageBoxMessage = string.Empty;
        MessageBoxButtonList = [];
        ShowMessageResponse = string.Empty;
        MessageBoxVisible = false;

        Log.ForContext("MessageBoxReturn", toReturn).ForContext("StatusControlContextId", StatusControlContextId)
            .Information("StatusControlContext Returning From Message Box");

        return toReturn;
    }

    public async Task<string> ShowMessageWithOkButton(string title, string? body)
    {
        return await ShowMessage(title, body,
            [new StatusControlMessageButton { IsDefault = true, MessageText = "Ok" }]);
    }

    public async Task<string> ShowMessageWithYesNoButton(string title, string? body)
    {
        return await ShowMessage(title, body,
            [
                new StatusControlMessageButton { IsDefault = true, MessageText = "Yes" },
                new StatusControlMessageButton { MessageText = "No" }
            ]);
    }

    public async Task<(bool, string)> ShowStringEntry(string title, string body, string initialUserString)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        StringEntryTitle = title;
        StringEntryMessage = body;
        StringEntryUserText = initialUserString;
        StringEntryVisible = true;
        StringEntryApproved = false;

        CurrentFullScreenCancellationSource = new CancellationTokenSource();

        try
        {
            await CurrentFullScreenCancellationSource.Token.WhenCancelled();
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException) Progress($"ShowStringEntry Exception {e.Message}");
#pragma warning disable 4014
            // Intended as Fire and Forget
            Task.Run(() => Log.Error(e, "ShowStringEntry Exception - Status Context Id: {ContextId}",
#pragma warning restore 4014
                StatusControlContextId));
        }
        finally
        {
            CurrentFullScreenCancellationSource.Dispose();
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        var toReturn = StringEntryUserText;
        var approved = StringEntryApproved;

        StringEntryTitle = string.Empty;
        StringEntryMessage = string.Empty;

        StringEntryUserText = string.Empty;

        StringEntryVisible = false;
        StringEntryApproved = false;

        return (approved, toReturn);
    }

    public void StateForceDismissFullScreenMessage()
    {
        CurrentFullScreenCancellationSource?.Cancel();
    }

    public async Task ToastError(string? toastText, bool userMustDismiss = false)
    {
        await Toast.Show(toastText, ToastType.Error, userMustDismiss);
    }

    public async Task ToastSuccess(string? toastText, bool userMustDismiss = false)
    {
        await Toast.Show(toastText, ToastType.Success, userMustDismiss);
    }

    public async Task ToastWarning(string? toastText, bool userMustDismiss = false)
    {
        await Toast.Show(toastText, ToastType.Warning, userMustDismiss);
    }

    private void UserMessageBoxResponse(string? responseString)
    {
        ShowMessageResponse = responseString;
        Progress($"Show Message Response {responseString}");
        CurrentFullScreenCancellationSource?.Cancel();
    }

    private void UserStringEntryApprovedResponse()
    {
        StringEntryApproved = true;
        CurrentFullScreenCancellationSource?.Cancel();
    }

    private void UserStringEntryCanceledResponse()
    {
        StringEntryApproved = false;
        CurrentFullScreenCancellationSource?.Cancel();
    }
}
