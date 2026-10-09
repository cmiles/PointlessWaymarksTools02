using System.Buffers.Binary;
using System.IO.Compression;
using NetTopologySuite.Geometries;

namespace PointlessWaymarks.SpatialTools.Tests;

[TestFixture]
public class StaticDemElevationTests
{
    private string _testDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "StaticDemElevationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        ElevationService.ClearStaticDemCache();
    }

    [TearDown]
    public void TearDown()
    {
        ElevationService.ClearStaticDemCache();
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // ignored
            }
        }
    }

    private static byte[] CreateSyntheticHgtBytes(int gridSize, Func<int, int, short> elevationGenerator)
    {
        var buffer = new byte[gridSize * gridSize * 2];
        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                var val = elevationGenerator(row, col);
                int index = (row * gridSize + col) * 2;
                BinaryPrimitives.WriteInt16BigEndian(buffer.AsSpan(index, 2), val);
            }
        }

        return buffer;
    }

    [Test]
    public void TileKey_Calculations_AreCorrect()
    {
        Assert.That(HgtTile.GetTileKey(34.5, -110.5), Is.EqualTo("N34W111"));
        Assert.That(HgtTile.GetTileKey(34.0, -111.0), Is.EqualTo("N34W111"));
        Assert.That(HgtTile.GetTileKey(34.999, -110.001), Is.EqualTo("N34W111"));
        Assert.That(HgtTile.GetTileKey(-34.5, 18.5), Is.EqualTo("S35E018"));
        Assert.That(HgtTile.GetTileKey(-34.0, 18.0), Is.EqualTo("S34E018"));
        Assert.That(HgtTile.GetTileKey(0.0, 0.0), Is.EqualTo("N00E000"));
        Assert.That(HgtTile.GetTileKey(0.5, 0.5), Is.EqualTo("N00E000"));
        Assert.That(HgtTile.GetTileKey(-0.1, -0.1), Is.EqualTo("S01W001"));
        Assert.That(HgtTile.GetTileKey(89.9, 179.9), Is.EqualTo("N89E179"));
        Assert.That(HgtTile.GetTileKey(-89.9, -179.9), Is.EqualTo("S90W180"));
    }

    [Test]
    public void TryParseTileKey_ParsesExpectedFormats()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HgtTile.TryParseTileKey("N34W111", out var lat1, out var lon1), Is.True);
            Assert.That(lat1, Is.EqualTo(34));
            Assert.That(lon1, Is.EqualTo(-111));

            Assert.That(HgtTile.TryParseTileKey("n34w111.hgt", out var lat2, out var lon2), Is.True);
            Assert.That(lat2, Is.EqualTo(34));
            Assert.That(lon2, Is.EqualTo(-111));

            Assert.That(HgtTile.TryParseTileKey("N34W111.hgt.zip", out var lat3, out var lon3), Is.True);
            Assert.That(lat3, Is.EqualTo(34));
            Assert.That(lon3, Is.EqualTo(-111));

            Assert.That(HgtTile.TryParseTileKey("S05E018.zip", out var lat4, out var lon4), Is.True);
            Assert.That(lat4, Is.EqualTo(-5));
            Assert.That(lon4, Is.EqualTo(18));

            Assert.That(HgtTile.TryParseTileKey("random_name.txt", out _, out _), Is.False);
        });
    }

    [Test]
    public void HgtTile_BilinearInterpolation_CalculatesExpectedElevations()
    {
        // Grid: 3x3 covering Lat [34, 35], Lon [-111, -110]
        // Row 0 = Lat 35.0 (North edge), Row 2 = Lat 34.0 (South edge)
        // Col 0 = Lon -111.0 (West edge), Col 2 = Lon -110.0 (East edge)
        //
        // NW (row 0, col 0) = 200, NE (row 0, col 2) = 400
        // SW (row 2, col 0) = 100, SE (row 2, col 2) = 300
        var rawBytes = CreateSyntheticHgtBytes(3, (row, col) =>
        {
            if (row == 0 && col == 0) return 200; // NW
            if (row == 0 && col == 1) return 300; // N mid
            if (row == 0 && col == 2) return 400; // NE
            if (row == 1 && col == 0) return 150; // W mid
            if (row == 1 && col == 1) return 250; // Center
            if (row == 1 && col == 2) return 350; // E mid
            if (row == 2 && col == 0) return 100; // SW
            if (row == 2 && col == 1) return 200; // S mid
            if (row == 2 && col == 2) return 300; // SE
            return 0;
        });

        var tile = new HgtTile(rawBytes, "N34W111");

        Assert.Multiple(() =>
        {
            // Exact corners
            Assert.That(tile.GetElevation(34.0, -111.0), Is.EqualTo(100.0).Within(0.001));
            Assert.That(tile.GetElevation(35.0, -111.0), Is.EqualTo(200.0).Within(0.001));
            Assert.That(tile.GetElevation(34.0, -110.0), Is.EqualTo(300.0).Within(0.001));
            Assert.That(tile.GetElevation(35.0, -110.0), Is.EqualTo(400.0).Within(0.001));

            // Center
            Assert.That(tile.GetElevation(34.5, -110.5), Is.EqualTo(250.0).Within(0.001));

            // Edge midpoints
            Assert.That(tile.GetElevation(34.5, -111.0), Is.EqualTo(150.0).Within(0.001));
            Assert.That(tile.GetElevation(34.0, -110.5), Is.EqualTo(200.0).Within(0.001));

            // Intermediate points (e.g. 1/4 of the way)
            // lat 34.25 (row = 1.5, half way between row 1 and row 2), lon -111.0 (col 0): half way between 100 and 150 = 125
            Assert.That(tile.GetElevation(34.25, -111.0), Is.EqualTo(125.0).Within(0.001));

            // Out of bounds
            Assert.That(tile.GetElevation(36.0, -111.0), Is.Null);
            Assert.That(tile.GetElevation(34.5, -112.0), Is.Null);
        });
    }

    [Test]
    public void HgtTile_VoidHandling_InterpolatesRemainingValidCorners()
    {
        // 2x2 grid where NW is void (-32768)
        var rawBytes = CreateSyntheticHgtBytes(2, (row, col) =>
        {
            if (row == 0 && col == 0) return -32768; // NW void
            if (row == 0 && col == 1) return 400; // NE
            if (row == 2 && col == 0) return 100; // SW
            if (row == 2 && col == 1) return 300; // SE
            return (short)(row == 1 && col == 0 ? 100 : 300);
        });

        var tile = new HgtTile(rawBytes, "N34W111");

        // SW corner is valid
        Assert.That(tile.GetElevation(34.0, -111.0), Is.EqualTo(100.0).Within(0.001));

        // Center: NW is void, remaining NE (400), SW (100), SE (300) have equal weights 0.25
        // (400 + 100 + 300) / 3 = 800 / 3 = 266.6667
        var centerElev = tile.GetElevation(34.5, -110.5);
        Assert.That(centerElev, Is.Not.Null);
        Assert.That(centerElev!.Value, Is.EqualTo(800.0 / 3.0).Within(0.01));
    }

    [Test]
    public async Task StaticDemElevation_LocalDirectory_HgtAndZip_AssignsElevationProperly()
    {
        // Create N34W111.hgt directly in test directory
        var hgtBytes1 = CreateSyntheticHgtBytes(2, (row, col) =>
        {
            if (row == 0 && col == 0) return 500; // NW (35, -111)
            if (row == 0 && col == 1) return 600; // NE (35, -110)
            if (row == 1 && col == 0) return 100; // SW (34, -111)
            if (row == 1 && col == 1) return 200; // SE (34, -110)
            return 0;
        });
        await File.WriteAllBytesAsync(Path.Combine(_testDir, "N34W111.hgt"), hgtBytes1);

        // Create N34W112.hgt inside N34W112.zip in test directory
        var hgtBytes2 = CreateSyntheticHgtBytes(2, (row, col) =>
        {
            if (row == 0 && col == 0) return 700; // NW (35, -112)
            if (row == 0 && col == 1) return 500; // NE (35, -111)
            if (row == 1 && col == 0) return 300; // SW (34, -112)
            if (row == 1 && col == 1) return 100; // SE (34, -111)
            return 0;
        });
        var zipPath = Path.Combine(_testDir, "N34W112.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("N34W112.hgt");
            using var entryStream = entry.Open();
            await entryStream.WriteAsync(hgtBytes2);
        }

        // Test single point queries
        var elev1 = await ElevationService.StaticDemElevation(_testDir, 34.0, -111.0);
        Assert.That(elev1, Is.EqualTo(100.0).Within(0.01));

        var elev2 = await ElevationService.StaticDemElevation(_testDir, 34.5, -110.5);
        Assert.That(elev2, Is.EqualTo(350.0).Within(0.01)); // (100+200+500+600)/4 = 350

        var elev3 = await ElevationService.StaticDemElevation(_testDir, 34.0, -112.0);
        Assert.That(elev3, Is.EqualTo(300.0).Within(0.01));

        // Test list of coordinates spanning both tiles
        var coords = new List<CoordinateZ>
        {
            new(-110.5, 34.5, 0), // in N34W111 -> 350
            new(-111.0, 34.0, 0), // in N34W111 -> 100
            new(-112.0, 34.0, 0), // in N34W112 -> 300
            new(-111.5, 34.5, 0), // in N34W112 -> (300+100+700+500)/4 = 400
            new(0, 0, 999) // missing tile -> stays 999
        };

        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));

        var result = await ElevationService.StaticDemElevation(_testDir, coords, progress);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Z, Is.EqualTo(350.0).Within(0.01));
            Assert.That(result[1].Z, Is.EqualTo(100.0).Within(0.01));
            Assert.That(result[2].Z, Is.EqualTo(300.0).Within(0.01));
            Assert.That(result[3].Z, Is.EqualTo(400.0).Within(0.01));
            Assert.That(result[4].Z, Is.EqualTo(999.0)); // Unchanged
            Assert.That(progressMessages, Is.Not.Empty);
        });

        // Test single CoordinateZ overload
        var singleCoord = new CoordinateZ(-110.5, 34.5, 0);
        await ElevationService.StaticDemElevation(_testDir, singleCoord);
        Assert.That(singleCoord.Z, Is.EqualTo(350.0).Within(0.01));
    }

    [Test]
    public async Task StaticDemElevation_ManualCacheTile_WorksWithoutDirectory()
    {
        var rawBytes = CreateSyntheticHgtBytes(2, (r, c) => 1234);
        var tile = new HgtTile(rawBytes, "N34W111");

        ElevationService.CacheStaticDemTile("memory://dems", tile);

        var elev = await ElevationService.StaticDemElevation("memory://dems", 34.5, -110.5);
        Assert.That(elev, Is.EqualTo(1234.0).Within(0.01));
    }

    [Test]
    public async Task StaticDemElevation_HttpUrl_DownloadsAndInterpolatesElevation()
    {
        var hgtBytes = CreateSyntheticHgtBytes(2, (row, col) => 777);

        // Setup local HTTP server
        var listener = new System.Net.HttpListener();
        var port = new Random().Next(25000, 35000);
        var prefix = $"http://127.0.0.1:{port}/dems/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                try
                {
                    var ctx = await listener.GetContextAsync();
                    if (ctx.Request.Url?.AbsolutePath.EndsWith("N34W111.hgt") == true)
                    {
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "application/octet-stream";
                        await ctx.Response.OutputStream.WriteAsync(hgtBytes);
                        ctx.Response.Close();
                    }
                    else
                    {
                        ctx.Response.StatusCode = 404;
                        ctx.Response.Close();
                    }
                }
                catch
                {
                    break;
                }
            }
        });

        try
        {
            var elev = await ElevationService.StaticDemElevation(prefix, 34.5, -110.5);
            Assert.That(elev, Is.EqualTo(777.0).Within(0.01));
        }
        finally
        {
            listener.Stop();
            listener.Close();
            await Task.WhenAny(serverTask, Task.Delay(100));
        }
    }

    [Test]
    public async Task Elevation_WhenAllTilesAvailableInStaticDem_SetsAllElevationsWithoutFallback()
    {
        // Write synthetic tile N34W111
        var hgtBytes = CreateSyntheticHgtBytes(2, (row, col) =>
        {
            if (row == 0 && col == 0) return 500; // NW
            if (row == 0 && col == 1) return 600; // NE
            if (row == 1 && col == 0) return 100; // SW
            if (row == 1 && col == 1) return 200; // SE
            return 0;
        });
        await File.WriteAllBytesAsync(Path.Combine(_testDir, "N34W111.hgt"), hgtBytes);

        var coords = new List<CoordinateZ>
        {
            new(-110.5, 34.5, 0),
            new(-111.0, 34.0, 0),
            new(-110.2, 34.2, 0)
        };

        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));

        var result = await ElevationService.Elevation(coords, _testDir, progress);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Z, Is.EqualTo(350.0).Within(0.01));
            Assert.That(result[1].Z, Is.EqualTo(100.0).Within(0.01));
            Assert.That(result[2].Z, Is.EqualTo(260.0).Within(0.01));
            Assert.That(progressMessages, Is.Not.Empty);
            Assert.That(progressMessages.Any(m => m.Contains("All 3 Coordinates resolved via Static DEM")), Is.True);
        });
    }

    [Test]
    public async Task Elevation_WhenSomeTilesMissing_UsesStaticDemAndFallsBackToOpenTopo()
    {
        // Write synthetic tile N34W111 with unique elevation
        var hgtBytes = CreateSyntheticHgtBytes(2, (row, col) => 4321);
        await File.WriteAllBytesAsync(Path.Combine(_testDir, "N34W111.hgt"), hgtBytes);

        var coords = new List<CoordinateZ>
        {
            new(-110.5, 34.5, 0), // in N34W111 -> should get 4321 from static DEM
            new(-112.5, 34.5, 0) // in N34W112 (missing, US) -> should get elevation from OpenTopo Ned10
        };

        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));

        var result = await ElevationService.Elevation(coords, _testDir, progress);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Z, Is.EqualTo(4321.0).Within(0.01));
            Assert.That(result[1].Z, Is.GreaterThan(0)); // OpenTopo elevation returned
            Assert.That(progressMessages.Any(m => m.Contains("querying OpenTopo Ned10")), Is.True);
        });
    }

    [Test]
    public async Task Elevation_WhenNonUsCoordinateNeedsFallback_FallsBackFromNed10ToMapZen()
    {
        // Non-US location: London, UK (51.5074, -0.1278) - Ned10 has no coverage here
        var coords = new List<CoordinateZ>
        {
            new(-0.1278, 51.5074, 0)
        };

        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));

        var result = await ElevationService.Elevation(coords, _testDir, progress);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Z, Is.GreaterThan(0)); // Mapzen returns London elevation (~10-40m)
            Assert.That(progressMessages.Any(m => m.Contains("falling back to OpenTopo Mapzen")), Is.True);
        });
    }

    [Test]
    public async Task Elevation_SinglePoint_WhenTileAvailable_ReturnsStaticElevation()
    {
        var rawBytes = CreateSyntheticHgtBytes(2, (r, c) => 888);
        var tile = new HgtTile(rawBytes, "N34W111");

        ElevationService.CacheStaticDemTile("memory://elevation-test", tile);

        var elev = await ElevationService.Elevation(34.5, -110.5, "memory://elevation-test");
        Assert.That(elev, Is.EqualTo(888.0).Within(0.01));
    }

    [Test]
    public async Task Elevation_SinglePoint_WhenTileMissing_FallsBackToOpenTopoNed()
    {
        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));

        // N34W112 is in Arizona (US), so it will resolve via OpenTopo Ned10
        var elev = await ElevationService.Elevation(34.5, -112.5, "memory://elevation-test", progress);
        Assert.Multiple(() =>
        {
            Assert.That(elev, Is.Not.Null);
            Assert.That(elev!.Value, Is.GreaterThan(0));
            Assert.That(progressMessages.Any(m => m.Contains("falling back to OpenTopo Ned10")), Is.True);
        });
    }

    [Test]
    public async Task Elevation_SinglePoint_NonUsLocation_FallsBackToOpenTopoMapZen()
    {
        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));

        // London, UK (51.5074, -0.1278) is outside US, Ned10 returns null, so it falls back to Mapzen
        var elev = await ElevationService.Elevation(51.5074, -0.1278, "memory://elevation-test", progress);
        Assert.Multiple(() =>
        {
            Assert.That(elev, Is.Not.Null);
            Assert.That(elev!.Value, Is.GreaterThan(0));
            Assert.That(progressMessages.Any(m => m.Contains("falling back to OpenTopo Mapzen")), Is.True);
        });
    }

    [Test]
    public async Task Elevation_EmptyList_ReturnsEmptyImmediately()
    {
        var emptyCoords = new List<CoordinateZ>();
        var result = await ElevationService.Elevation(emptyCoords, _testDir);
        Assert.That(result, Is.Empty);
    }
}