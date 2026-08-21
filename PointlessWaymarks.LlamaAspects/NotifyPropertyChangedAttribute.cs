using System.ComponentModel;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Diagnostics;

namespace PointlessWaymarks.LlamaAspects;

public class NotifyPropertyChangedAttribute : TypeAspect
{
    // The "event is never used" warning (CS0067) is raised for the introduced
    // PropertyChanged event. Declare it here so the aspect can suppress it.
    private static readonly SuppressionDefinition _suppressEventNeverUsed = new("CS0067");

    public override void BuildAspect(IAspectBuilder<INamedType> builder)
    {
        builder.ImplementInterface(typeof(INotifyPropertyChanged), OverrideStrategy.Ignore);

        // Suppress CS0067 only for the class this aspect is applied to, so the
        // warning keeps working normally for every other event in the solution.
        builder.Diagnostics.Suppress(_suppressEventNeverUsed, builder.Target);

        foreach (var property in builder.Target.Properties.Where(p =>
                     p is { IsAbstract: false, Writeability: Writeability.All } &&
                     !p.Attributes.Any(typeof(DoNotGenerateInpc))))
            builder.With(property).OverrideAccessors(null, nameof(OverridePropertySetter));
    }

    [Introduce(WhenExists = OverrideStrategy.Ignore)]
    protected void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(meta.This, new PropertyChangedEventArgs(name));
    }

    [Template]
    private dynamic OverridePropertySetter(dynamic value)
    {
        if (value != meta.Target.Property.Value)
        {
            meta.Proceed();
            OnPropertyChanged(meta.Target.Property.Name);
        }

        return value;
    }

    [InterfaceMember] public event PropertyChangedEventHandler? PropertyChanged;
}