namespace PointlessWaymarks.AvaloniaCommon.StringDataEntry;

public interface IStringDataEntryContext
{
    int BindingDelay { get; set; }
    string HelpText { get; set; }
    string ReferenceValue { get; set; }
    string Title { get; set; }
    string UserValue { get; set; }
    string ValidationMessage { get; set; }
    bool HasChanges { get; set; }
    bool HasValidationIssues { get; set; }
}