using Metalama.Framework.Aspects;

namespace PointlessWaymarks.LlamaAspects;

public class StopAndWarnIfContentIsNullAttribute : OverrideMethodAspect
{
    public override dynamic? OverrideMethod()
    {
        meta.InsertStatement("""
                             if (content is null)
                             {
                                 await StatusContext.ToastError("Nothing Selected?");
                                 return;
                             }
                             """);

        return meta.Proceed();
    }
}