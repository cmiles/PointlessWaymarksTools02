using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.UnoCommon.ChangesAndValidation;

namespace PointlessWaymarks.UnoCommon.BoolDataEntry;

public partial class BoolNullableDataEntryContext : ObservableObject, IHasChanges, IHasValidationIssues
{
    [ObservableProperty] private bool _hasChanges;
    [ObservableProperty] private bool _hasValidationIssues;
    [ObservableProperty] private string _helpText = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private bool? _referenceValue;
    [ObservableProperty] private string _referenceValueString = "Original Value: Previously blank";
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool? _userValue;
    [ObservableProperty] private List<Func<bool?, IsValid>> _validationFunctions = [];
    [ObservableProperty] private string _validationMessage = string.Empty;

    public bool UserValueIsNullable => true;

    public BoolNullableDataEntryContext()
    {
        PropertyChanged += OnPropertyChanged;
        UpdateReferenceValueString();
    }

    private void UpdateReferenceValueString()
    {
        ReferenceValueString = ReferenceValue.HasValue
            ? $"Original Value: {ReferenceValue.Value}"
            : "Original Value: Previously blank";
    }

    public void CheckForChangesAndValidate()
    {
        HasChanges = UserValue != ReferenceValue;
        UpdateReferenceValueString();

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

    public static Task<BoolNullableDataEntryContext> CreateInstance()
    {
        return Task.FromResult(new BoolNullableDataEntryContext());
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName.Equals(nameof(HasChanges)) || e.PropertyName.Equals(nameof(HasValidationIssues)) ||
            e.PropertyName.Equals(nameof(ValidationMessage)) || e.PropertyName.Equals(nameof(ReferenceValueString))) return;

        if (e.PropertyName.Equals(nameof(ReferenceValue)) || e.PropertyName.Equals(nameof(UserValue)) ||
            e.PropertyName.Equals(nameof(ValidationFunctions)) || e.PropertyName.Equals(nameof(IsEnabled)))
            CheckForChangesAndValidate();
    }
}
