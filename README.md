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
