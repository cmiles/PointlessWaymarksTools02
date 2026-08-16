The cb_2025_us_county_5m file is from: [Cartographic Boundary Files](https://www.census.gov/geographies/mapping-files/time-series/geo/cartographic-boundary.html#)

Then transformed to GeoJson via:
```
ocker run --rm -v "${PWD}:/data" -w /data ghcr.io/osgeo/gdal:ubuntu-small-latest ogr2ogr -f GeoJSON cb_2025_us_county_5m.geojson cb_2025_us_county_5m.shp -select "NAMELSAD,STUSPS,STATE_NAME" -lco COORDINATE_PRECISION=5
```

I decided to tranform the data to GeoJson to have an easily to include with the library with the side benefit of easy readability.