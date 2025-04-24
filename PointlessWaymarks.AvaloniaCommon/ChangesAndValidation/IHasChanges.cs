namespace PointlessWaymarks.AvaloniaCommon.ChangesAndValidation;

public interface IHasChanges
{
    public bool HasChanges { get; }
}

public interface IHasChangesExtended : IHasChanges
{
    List<(bool hasChanges, string description)> HasChangesChangedList { get; set; }
}