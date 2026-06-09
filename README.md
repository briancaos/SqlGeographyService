# SqlGeographyService

Converts a GeoJSON `FeatureGeometry` object into a `SqlGeography` value for use with Microsoft SQL Server (`Microsoft.SqlServer.Types`).

## What it does

`SqlGeographyService.ToSqlGeography(FeatureGeometry geometry)` accepts a GeoJSON geometry and returns a `SqlGeography` instance using SRID 4326 (WGS84). Supported geometry types are `Polygon` and `MultiPolygon`. Coordinates are rounded to 5 decimal places. If the resulting geometry is invalid, `MakeValid()` is called automatically before returning.

Two lower-level helpers are also exposed if you need to build shapes directly:

- `BuildPolygon(List<List<double[]>>)` — builds a single polygon from rings
- `BuildMultiPolygon(List<List<List<double[]>>>)` — builds a multi-polygon from an array of polygons

## FeatureGeometry format

`FeatureGeometry` mirrors the geometry object from the [GeoJSON spec (RFC 7946)](https://tools.ietf.org/html/rfc7946). It has a `Type` string and a `Coordinates` property that holds a raw `JsonElement`.

A **Polygon** has one outer ring and zero or more inner rings (holes). Each ring is a closed array of `[longitude, latitude]` pairs (the last point must equal the first):

```json
{
  "type": "Polygon",
  "coordinates": [
    [
      [10.0, 55.0],
      [12.0, 55.0],
      [12.0, 57.0],
      [10.0, 57.0],
      [10.0, 55.0]
    ]
  ]
}
```

A **MultiPolygon** is an array of Polygon coordinate arrays:

```json
{
  "type": "MultiPolygon",
  "coordinates": [
    [
      [
        [10.0, 55.0],
        [12.0, 55.0],
        [12.0, 57.0],
        [10.0, 57.0],
        [10.0, 55.0]
      ]
    ],
    [
      [
        [15.0, 55.0],
        [17.0, 55.0],
        [17.0, 57.0],
        [15.0, 57.0],
        [15.0, 55.0]
      ]
    ]
  ]
}
```

## FeatureCollection

`FeatureCollection` is a full C# representation of the [GeoJSON FeatureCollection](https://tools.ietf.org/html/rfc7946#section-3.3) object (RFC 7946). It maps directly to the JSON structure used by tools such as [geojson.io](https://geojson.io/) and [MapTiler](https://www.maptiler.com/), making it straightforward to load, inspect, and process GeoJSON files.

A GeoJSON file typically looks like this:

```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Polygon",
        "coordinates": [
          [
            [10.0, 55.0],
            [12.0, 55.0],
            [12.0, 57.0],
            [10.0, 57.0],
            [10.0, 55.0]
          ]
        ]
      },
      "properties": {
        "name": "My Region",
        "population": 42000
      }
    }
  ]
}
```

The C# classes map to this structure as follows:

- `FeatureCollection` — the root object with `type: "FeatureCollection"` and a `features` array
- `Feature` — each entry in `features`, with `type: "Feature"`, a `geometry`, and a `properties` bag
- `FeatureGeometry` — the geometry object with a `type` string (`"Polygon"`, `"MultiPolygon"`, etc.) and a raw `coordinates` element
- `Feature.Properties` is a `JsonElement?`, so any JSON object — including nested structures — is preserved without requiring a fixed schema

### Deserializing a GeoJSON file

```csharp
using System.Text.Json;

var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

await using var stream = File.OpenRead("regions.geojson");
var collection = await JsonSerializer.DeserializeAsync<FeatureCollection>(stream, options);

foreach (var feature in collection?.Features ?? [])
{
    var geometry = feature.Geometry;
    if (geometry == null) continue;

    SqlGeography shape = SqlGeographyService.ToSqlGeography(geometry);

    // use shape as needed, e.g. insert into SQL Server
}
```

## Inserting into SQL Server

Given a table with a `geography` column:

```sql
CREATE TABLE Regions (
    Id       INT PRIMARY KEY,
    Name     NVARCHAR(200),
    Shape    geography
);
```

Use Dapper (or plain ADO.NET) to insert the converted value:

```csharp
using Microsoft.SqlServer.Types;
using System.Text.Json;

// Deserialize your GeoJSON geometry
var geometry = JsonSerializer.Deserialize<FeatureGeometry>(geoJsonString);

// Convert to SqlGeography
SqlGeography shape = SqlGeographyService.ToSqlGeography(geometry);

// Insert with Dapper
await connection.ExecuteAsync(
    "INSERT INTO Regions (Id, Name, Shape) VALUES (@Id, @Name, @Shape)",
    new { Id = 1, Name = "My Region", Shape = shape }
);
```

With plain `SqlCommand`:

```csharp
using var cmd = new SqlCommand(
    "INSERT INTO Regions (Id, Name, Shape) VALUES (@Id, @Name, @Shape)", connection);

cmd.Parameters.AddWithValue("@Id", 1);
cmd.Parameters.AddWithValue("@Name", "My Region");

var param = cmd.Parameters.Add("@Shape", System.Data.SqlDbType.Udt);
param.UdtTypeName = "geography";
param.Value = shape;

await cmd.ExecuteNonQueryAsync();
```

## Dependencies

- [`Microsoft.SqlServer.Types`](https://www.nuget.org/packages/Microsoft.SqlServer.Types) — provides `SqlGeography` and `SqlGeographyBuilder`
- `System.Text.Json` — used to deserialize the `Coordinates` `JsonElement`
