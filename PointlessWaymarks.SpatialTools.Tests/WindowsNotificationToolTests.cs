using Windows.Data.Xml.Dom;
using PointlessWaymarks.WindowsTools;

namespace PointlessWaymarks.SpatialTools.Tests;

[TestFixture]
public class WindowsNotificationToolTests
{
    [Test]
    public void CreateToastXml_BasicMessage_GeneratesValidXml()
    {
        var xml = WindowsNotificationTool.CreateToastXml(
            "Test message summary",
            "Pointless Waymarks Project",
            null,
            "file:///C:/Assets/logo.png",
            null);

        Assert.That(xml, Does.Contain("<text>Test message summary</text>"));
        Assert.That(xml, Does.Contain("<text placement=\"attribution\">Pointless Waymarks Project</text>"));
        Assert.That(xml, Does.Contain("<image placement=\"appLogoOverride\" src=\"file:///C:/Assets/logo.png\"/>"));
        Assert.That(xml, Does.Not.Contain("launch="));
        Assert.That(xml, Does.Not.Contain("placement=\"hero\""));

        var xmlDoc = new XmlDocument();
        Assert.DoesNotThrow(() => xmlDoc.LoadXml(xml));
    }

    [Test]
    public void CreateToastXml_WithHeroImage_GeneratesHeroImageElement()
    {
        var xml = WindowsNotificationTool.CreateToastXml(
            "Photo uploaded",
            "Pointless Waymarks Project",
            null,
            "file:///C:/Assets/logo.png",
            "file:///C:/Photos/image.jpg");

        Assert.That(xml, Does.Contain("<image placement=\"hero\" src=\"file:///C:/Photos/image.jpg\"/>"));

        var xmlDoc = new XmlDocument();
        Assert.DoesNotThrow(() => xmlDoc.LoadXml(xml));
    }

    [Test]
    public void CreateToastXml_WithLaunchProtocol_GeneratesLaunchAttribute()
    {
        var xml = WindowsNotificationTool.CreateToastXml(
            "Error occurred. Click for report.",
            "Cloud Backup Runner",
            "C:\\Reports\\report.html",
            "file:///C:/Assets/error.png",
            null);

        Assert.That(xml, Does.Contain("launch=\"C:\\Reports\\report.html\""));
        Assert.That(xml, Does.Contain("activationType=\"protocol\""));

        var xmlDoc = new XmlDocument();
        Assert.DoesNotThrow(() => xmlDoc.LoadXml(xml));
    }

    [Test]
    public void CreateToastXml_SpecialCharactersEscaped_ProducesValidXml()
    {
        var xml = WindowsNotificationTool.CreateToastXml(
            "Error: <File not found & 'access' \"denied\">",
            "Tom & Jerry's \"App\" <v1>",
            "C:\\Path with & in name\\report.html",
            "file:///C:/Assets/logo & icon.png",
            null);

        Assert.That(xml, Does.Contain("&lt;File not found &amp; &apos;access&apos; &quot;denied&quot;&gt;"));
        Assert.That(xml, Does.Contain("Tom &amp; Jerry&apos;s &quot;App&quot; &lt;v1&gt;"));

        var xmlDoc = new XmlDocument();
        Assert.DoesNotThrow(() => xmlDoc.LoadXml(xml));
    }

    [Test]
    public async Task NewNotifier_SetsAttributionAndLogoUrls()
    {
        var notifier = await WindowsNotificationBuilders.NewNotifier("Custom Notifier App");

        Assert.That(notifier.Attribution, Is.EqualTo("Custom Notifier App"));
        Assert.That(notifier.NotificationIconSuccessUrl, Does.Contain("PointlessWaymarksCmsAutomationCircularLogo.png"));
        Assert.That(notifier.NotificationIconErrorUrl, Does.Contain("PointlessWaymarksCmsAutomationErrorCircularLogo.png"));
    }
}
