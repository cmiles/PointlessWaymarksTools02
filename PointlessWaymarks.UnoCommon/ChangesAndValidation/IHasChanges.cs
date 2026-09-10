namespace PointlessWaymarks.UnoCommon.ChangesAndValidation;

public interface IHasChanges
{
    bool HasChanges { get; }
}

public interface IHasChangesExtended : IHasChanges
{
    List<(bool hasChanges, string description)> HasChangesChangedList { get; set; }
}
