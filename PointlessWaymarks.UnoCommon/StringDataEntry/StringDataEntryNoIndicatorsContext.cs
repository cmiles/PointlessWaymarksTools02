using CommunityToolkit.Mvvm.ComponentModel;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.UnoCommon.ChangesAndValidation;

namespace PointlessWaymarks.UnoCommon.StringDataEntry;

public partial class StringDataEntryNoIndicatorsContext : ObservableObject, IHasChanges, IHasValidationIssues, IStringDataEntryContext, ICheckForChangesAndValidation
{
    [ObservableProperty] private int _bindingDelay = 10;
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private bool _hasValidationIssues;
    [ObservableProperty] private string _helpText = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _referenceValue = string.Empty;
    [ObservableProperty] private string _referenceValueString = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _userValue = string.Empty;
    [ObservableProperty] private List<Func<string?, Task<IsValid>>> _validationFunctions = [];
    [ObservableProperty] private string _validationMessage = string.Empty;

    public StringDataEntryNoIndicatorsContext()
    {
    }

    public Task CheckForChangesAndValidationIssues()
    {
        return Task.CompletedTask;
    }

    void ICheckForChangesAndValidation.CheckForChangesAndValidationIssues()
    {
    }

    public static StringDataEntryNoIndicatorsContext CreateInstance()
    {
        return new StringDataEntryNoIndicatorsContext();
    }
}
