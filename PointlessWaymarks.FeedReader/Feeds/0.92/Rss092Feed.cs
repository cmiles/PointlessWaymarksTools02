using System.Xml.Linq;
using PointlessWaymarks.FeedReader.Feeds._0._91;
using PointlessWaymarks.FeedReader.Feeds.Base;

namespace PointlessWaymarks.FeedReader.Feeds._0._92;

/// <summary>
///     Rss 0.92 feed according to specification: http://backend.userland.com/rss092
/// </summary>
public class Rss092Feed : Rss091Feed
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Rss092Feed" /> class.
    ///     default constructor (for serialization)
    /// </summary>
    public Rss092Feed()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rss092Feed" /> class.
    ///     Reads a rss 0.92 feed based on the xml given in channel
    /// </summary>
    /// <param name="feedXml">the entire feed xml as string</param>
    /// <param name="channel">the "channel" element in the xml as XElement</param>
    public Rss092Feed(string feedXml, XElement? channel)
        : base(feedXml, channel)
    {
        Cloud = new FeedCloud(channel.GetElement("cloud"));
    }

    /// <summary>
    ///     The "cloud" field
    /// </summary>
    public FeedCloud? Cloud { get; set; }

    public override List<BaseFeedItem> CreateItems(IEnumerable<XElement?>? items)
    {
        if (items == null) return [];

        var returnList = new List<BaseFeedItem>();

        foreach (var item in items) returnList.Add(new Rss092FeedItem(item));

        return returnList;
    }

    /// <summary>
    ///     Creates the base <see cref="Feed" /> element out of this feed.
    /// </summary>
    /// <returns>feed</returns>
    public override Feed ToFeed()
    {
        var feed = base.ToFeed();
        feed.SpecificFeed = this;
        feed.Type = FeedType.Rss_0_92;
        return feed;
    }
}