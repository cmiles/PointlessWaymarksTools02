using System.Linq;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace PointlessWaymarks.LlamaAspects;

public class StopAndWarnIfFirstParameterIsNullAttribute : OverrideMethodAspect
{
    public override dynamic? OverrideMethod()
    {
        var firstParameter = meta.Target.Parameters.FirstOrDefault();

        // If the containing type does not expose a StatusContext member, do nothing.
        var hasStatusContext = meta.Target.Type.Properties.Any(p => p.Name == "StatusContext") ||
                               meta.Target.Type.Fields.Any(f => f.Name == "StatusContext");

        if (!hasStatusContext) return meta.Proceed();

        if (firstParameter is null) return meta.Proceed();

        meta.InsertStatement($$"""
                             if ({{firstParameter.Name}} is null)
                             {
                                 await StatusContext.ToastError("Nothing Selected?");
                                 return;
                             }
                             """);

        return meta.Proceed();
    }
}
