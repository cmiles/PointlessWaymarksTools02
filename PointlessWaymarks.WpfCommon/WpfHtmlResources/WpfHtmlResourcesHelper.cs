using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.FileProviders;
using PointlessWaymarks.WpfCommon.WebViewVirtualDomain;

namespace PointlessWaymarks.WpfCommon.WpfHtmlResources;

public static class WpfHtmlResourcesHelper
{
    public static List<FileBuilderCreate> AwesomeMapSvgMarkers()
    {
        var returnList = new List<FileBuilderCreate>();

        var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());

        var siteResource = embeddedProvider.GetDirectoryContents("")
            .Single(x => x.Name.Contains("leaflet.awesome-svg-markers.css"));
        var embeddedAsStream = siteResource.CreateReadStream();
        var reader = new StreamReader(embeddedAsStream);
        var resourceString = reader.ReadToEnd();

        embeddedAsStream.Dispose();

        returnList.Add(new FileBuilderCreate("leaflet.awesome-svg-markers.css", resourceString));


        siteResource = embeddedProvider.GetDirectoryContents("")
            .Single(x => x.Name.Contains("leaflet.awesome-svg-markers.js"));
        embeddedAsStream = siteResource.CreateReadStream();
        reader = new StreamReader(embeddedAsStream);
        resourceString = reader.ReadToEnd();

        embeddedAsStream.Dispose();

        returnList.Add(new FileBuilderCreate("leaflet.awesome-svg-markers.js", resourceString));


        var markerResources = embeddedProvider.GetDirectoryContents("")
            .Where(x => x.Name.Contains("markers-"));

        foreach (var markerResource in markerResources)
        {
            embeddedAsStream = markerResource.CreateReadStream();

            byte[] binaryResource;

            using (MemoryStream ms = new MemoryStream())
            {
                embeddedAsStream.CopyTo(ms);
                binaryResource = ms.ToArray();
            }

            embeddedAsStream.Dispose();

            returnList.Add(new FileBuilderCreate($@"images\{markerResource.Name.Replace("WpfHtmlResources.images.", string.Empty)}", binaryResource));
        }

        return returnList;
    }

    public static string LocalMapCommonJs()
    {
        return ReadEmbeddedText("localMapCommon.js");
    }

    public static string LeafletJs()
    {
        return ReadEmbeddedText("leaflet.js");
    }

    public static string LeafletCss()
    {
        return ReadEmbeddedText("leaflet.css");
    }

    public static string ChartJs()
    {
        return ReadEmbeddedText("chart.umd.min.js");
    }

    public static List<FileBuilderCreate> LeafletImages()
    {
        var returnList = new List<FileBuilderCreate>();
        var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());

        var imageResources = embeddedProvider.GetDirectoryContents("")
            .Where(x => x.Name.Contains("marker-icon") || x.Name.Contains(".marker-shadow") || x.Name.Contains(".layers"));

        foreach (var resource in imageResources)
        {
            using var stream = resource.CreateReadStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var binaryResource = ms.ToArray();

            returnList.Add(new FileBuilderCreate(
                $@"images\{resource.Name.Replace("WpfHtmlResources.images.", string.Empty)}",
                binaryResource));
        }

        return returnList;
    }

    private static string ReadEmbeddedText(string nameFragment)
    {
        var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());

        var siteResource = embeddedProvider.GetDirectoryContents("")
            .Single(x => x.Name.EndsWith(nameFragment));
        using var embeddedAsStream = siteResource.CreateReadStream();
        var reader = new StreamReader(embeddedAsStream);
        return reader.ReadToEnd();
    }
}