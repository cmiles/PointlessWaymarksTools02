namespace PointlessWaymarks.AvaloniaCommon.BoolNullableDataEntry;

public interface IBoolNullableDataEntryContext
{
    bool HasChanges { get; }
    bool HasValidationIssues { get; }
    string HelpText { get; }
    bool IsEnabled { get; }
    bool? ReferenceValue { get; }
    string Title { get; }
    bool? UserValue { get; set; }
    bool UserValueIsNullable { get; }
    string ValidationMessage { get; }
}