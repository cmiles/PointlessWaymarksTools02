using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.UnoCommon.ChangesAndValidation;

namespace PointlessWaymarks.UnoCommon.BoolDataEntry;

public partial class BoolDataEntryNoChangeIndicatorContext : ObservableObject, IHasChanges, IHasValidationIssues, IBoolDataEntryContext
{
    [ObservableProperty] private bool _hasValidationIssues;
    [ObservableProperty] private string _helpText = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private bool _referenceValue;
    [ObservableProperty] private string _referenceValueString = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _userValue;
    [ObservableProperty] private List<Func<bool, IsValid>> _validationFunctions = [];
    [ObservableProperty] private string _validationMessage = string.Empty;

    public bool HasChanges => false;
    public bool UserValueIsNullable => false;

    public BoolDataEntryNoChangeIndicatorContext()
    {
        PropertyChanged += OnPropertyChanged;
    }

    public void CheckForChangesAndValidate()
    {
        if (ValidationFunctions.Count > 0)
            foreach (var loopValidations in ValidationFunctions)
            {
                var validationResult = loopValidations(UserValue);
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

    public static Task<BoolDataEntryNoChangeIndicatorContext> CreateInstance()
    {
        return Task.FromResult(new BoolDataEntryNoChangeIndicatorContext());
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName.Equals(nameof(HasChanges)) || e.PropertyName.Equals(nameof(HasValidationIssues)) ||
            e.PropertyName.Equals(nameof(ValidationMessage))) return;

        if (e.PropertyName.Equals(nameof(ReferenceValue)) || e.PropertyName.Equals(nameof(UserValue)) ||
            e.PropertyName.Equals(nameof(ValidationFunctions)) || e.PropertyName.Equals(nameof(IsEnabled)))
            CheckForChangesAndValidate();
    }
}
