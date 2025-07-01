using System.ComponentModel;
using PointlessWaymarks.AvaloniaCommon.ChangesAndValidation;
using PointlessWaymarks.AvaloniaLlamaAspects;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.AvaloniaCommon.BoolNullableDataEntry;

[NotifyPropertyChanged]
public partial class BoolNullableDataEntryContext : IHasChanges, IHasValidationIssues, IBoolNullableDataEntryContext
{
    private BoolNullableDataEntryContext()
    {
        PropertyChanged += OnPropertyChanged;
    }

    public bool? ReferenceValue { get; set; }
    public bool? UserValue { get; set; }
    public List<Func<bool?, IsValid>> ValidationFunctions { get; set; } = [];

    public string HelpText { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Title { get; set; } = string.Empty;

    public bool UserValueIsNullable => true;
    public string ValidationMessage { get; set; } = string.Empty;
    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }
    

    private void CheckForChangesAndValidate()
    {
        HasChanges = UserValue != ReferenceValue;

        if (ValidationFunctions.Any())
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

    public static BoolNullableDataEntryContext CreateInstance()
    {
        return new BoolNullableDataEntryContext();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName.Equals(nameof(HasChanges)) || e.PropertyName.Equals(nameof(HasValidationIssues)) ||
            e.PropertyName.Equals(nameof(ValidationMessage))) return;

        CheckForChangesAndValidate();
    }
}