namespace PointlessWaymarks.AvaloniaCommon.ConversionDataEntry;

public interface IConversionDataEntryContext
{
    string HelpText { get; set; }
    bool IsNumeric { get; set; }
    string ReferenceValueString { get; }
    string Title { get; set; }
    string UserText { get; set; }
    string? ValidationMessage { get; set; }
    bool HasChanges { get; set; }
    bool HasValidationIssues { get; set; }
}