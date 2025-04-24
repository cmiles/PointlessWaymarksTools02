using PointlessWaymarks.AvaloniaLlamaAspects;

namespace PointlessWaymarks.AvaloniaCommon.MarkdownDisplay;

[NotifyPropertyChanged]
public partial class HelpDisplayContext
{
    public HelpDisplayContext(List<string> markdownHelp)
    {
        if (!markdownHelp.Any()) HelpMarkdownContent = string.Empty;
        else
            HelpMarkdownContent = string.Join(Environment.NewLine + Environment.NewLine,
                markdownHelp.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public string HelpMarkdownContent { get; set; }
} 