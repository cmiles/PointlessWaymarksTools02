using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.UnoCommon.ChangesAndValidation;

namespace PointlessWaymarks.UnoCommon.StringDataEntry;

public partial class StringDataEntryContext : ObservableObject, IHasChanges, IHasValidationIssues, IStringDataEntryContext, ICheckForChangesAndValidation
{
    [ObservableProperty] private int _bindingDelay = 10;
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private bool _hasValidationIssues;
    [ObservableProperty] private string _helpText = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string _referenceValue = string.Empty;
    [ObservableProperty] private string _referenceValueString = "Original Value: Previously blank";
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _userValue = string.Empty;
    [ObservableProperty] private List<Func<string?, Task<IsValid>>> _validationFunctions = [];
    [ObservableProperty] private string _validationMessage = string.Empty;

    public StringDataEntryContext()
    {
        PropertyChanged += OnPropertyChanged;
        UpdateReferenceValueString();
    }

    private void UpdateReferenceValueString()
    {
        ReferenceValueString = string.IsNullOrWhiteSpace(ReferenceValue)
            ? "Original Value: Previously blank"
            : $"Original Value: {ReferenceValue}";
    }

    public async Task CheckForChangesAndValidationIssues()
    {
        HasChanges = UserValue.TrimNullToEmpty() != ReferenceValue.TrimNullToEmpty();
        UpdateReferenceValueString();

        if (ValidationFunctions.Count > 0)
            foreach (var loopValidations in ValidationFunctions)
            {
                var validationResult = await loopValidations(UserValue);
                if (!validationResult.Valid)
                {
                    HasValidationIssues = true;
                    ValidationMessage = validationResult.Explanation;
                    return;
                }
            }

        HasValidationIssues = false;
        ValidationMessage = string.Empty;
    }

    void ICheckForChangesAndValidation.CheckForChangesAndValidationIssues()
    {
        _ = CheckForChangesAndValidationIssues();
    }

    public static StringDataEntryContext CreateInstance()
    {
        return new StringDataEntryContext();
    }

    private async void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName.Equals(nameof(HasChanges)) || e.PropertyName.Equals(nameof(HasValidationIssues)) ||
            e.PropertyName.Equals(nameof(ValidationMessage)) || e.PropertyName.Equals(nameof(ReferenceValueString))) return;

        if (e.PropertyName.Equals(nameof(ReferenceValue)) || e.PropertyName.Equals(nameof(UserValue)) ||
            e.PropertyName.Equals(nameof(ValidationFunctions)) || e.PropertyName.Equals(nameof(IsEnabled)))
            await CheckForChangesAndValidationIssues();
    }
}
