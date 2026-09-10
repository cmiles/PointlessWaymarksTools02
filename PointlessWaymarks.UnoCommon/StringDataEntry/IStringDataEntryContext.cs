namespace PointlessWaymarks.UnoCommon.StringDataEntry;

public interface IStringDataEntryContext
{
    int BindingDelay { get; set; }
    bool HasChanges { get; }
    bool HasValidationIssues { get; }
    string HelpText { get; set; }
    bool IsEnabled { get; set; }
    string ReferenceValue { get; set; }
    string ReferenceValueString { get; }
    string Title { get; set; }
    string UserValue { get; set; }
    string ValidationMessage { get; set; }
}
