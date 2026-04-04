using System.Collections.ObjectModel;
using System.ComponentModel;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;

namespace PointlessWaymarks.WpfCommon.StringMultiSelectDataEntry;

[NotifyPropertyChanged]
public partial class StringMultiSelectDataEntryContext : IHasChanges, IHasValidationIssues
{
    private StringMultiSelectDataEntryContext()
    {
        PropertyChanged += OnPropertyChanged;
    }

    public ObservableCollection<MultiSelectDataChoice> Choices { get; set; } = [];
    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }
    public string HelpText { get; set; } = string.Empty;
    public List<string> ReferenceValues { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public List<string> UserValues => Choices.Where(x => x.IsSelected).Select(x => x.DataString).ToList();
    public List<Func<List<string>, IsValid>> ValidationFunctions { get; set; } = [];
    public string ValidationMessage { get; set; } = string.Empty;

    private void CheckForChangesAndValidate()
    {
        var currentValues = UserValues.OrderBy(x => x).ToList();
        var referenceValues = ReferenceValues.OrderBy(x => x).ToList();
        
        HasChanges = !currentValues.SequenceEqual(referenceValues);

        if (ValidationFunctions.Any())
            foreach (var loopValidations in ValidationFunctions)
            {
                var (passed, validationMessage) = loopValidations(UserValues);
                if (!passed)
                {
                    HasValidationIssues = true;
                    ValidationMessage = validationMessage;
                    return;
                }
            }

        HasValidationIssues = false;
        ValidationMessage = string.Empty;
    }

    public static StringMultiSelectDataEntryContext CreateInstance()
    {
        return new StringMultiSelectDataEntryContext();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (e.PropertyName.Equals(nameof(HasChanges)) || e.PropertyName.Equals(nameof(HasValidationIssues)) ||
            e.PropertyName.Equals(nameof(ValidationMessage))) return;

        CheckForChangesAndValidate();
    }

    public void OnChoiceSelectionChanged()
    {
        CheckForChangesAndValidate();
    }

    public void SetChoices(List<MultiSelectDataChoice> choices)
    {
        foreach (var choice in Choices)
            choice.PropertyChanged -= Choice_PropertyChanged;

        Choices.Clear();

        foreach (var choice in choices)
        {
            choice.PropertyChanged += Choice_PropertyChanged;
            Choices.Add(choice);
        }

        CheckForChangesAndValidate();
    }

    private void Choice_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MultiSelectDataChoice.IsSelected))
            CheckForChangesAndValidate();
    }

    public void TrySetUserValues(List<string> userValues)
    {
        foreach (var choice in Choices)
            choice.IsSelected = userValues.Contains(choice.DataString);
    }

    public void SelectAll()
    {
        foreach (var choice in Choices)
            choice.IsSelected = true;
    }

    public void SelectNone()
    {
        foreach (var choice in Choices)
            choice.IsSelected = false;
    }
}
