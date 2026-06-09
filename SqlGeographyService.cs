using Microsoft.SqlServer.Types;
using System.Text.Json;

namespace BrianCaos
{
  public class SqlGeographyService
  {
    public static SqlGeography ToSqlGeography(FeatureGeometry geometry)
    {
      if (!geometry.Coordinates.HasValue)
        throw new ArgumentException("Geometry coordinates cannot be null.");

      SqlGeography sqlGeography;
      switch (geometry.Type)
      {
        case "Polygon":
          var polygon = geometry.Coordinates.Value.Deserialize<List<List<double[]>>>();
          sqlGeography = BuildPolygon(polygon ?? new List<List<double[]>>());
          break;
        case "MultiPolygon":
          var multiPolygon = geometry.Coordinates.Value.Deserialize<List<List<List<double[]>>>>();
          sqlGeography = BuildMultiPolygon(multiPolygon ?? new List<List<List<double[]>>>());
          break;
        default:
          throw new NotSupportedException($"Geometry type '{geometry.Type}' is not supported.");
      }

      if (!sqlGeography.STIsValid())
      {
        sqlGeography = sqlGeography.MakeValid();
      }
      return sqlGeography;
    }

    public static SqlGeography BuildMultiPolygon(List<List<List<double[]>>> coordinates)
    {
      if (coordinates == null || coordinates.Count == 0)
        throw new ArgumentException("MultiPolygon must contain at least one polygon.");

      var builder = new SqlGeographyBuilder();
      builder.SetSrid(4326);
      builder.BeginGeography(OpenGisGeographyType.MultiPolygon);

      foreach (var polygon in coordinates) // Polygon[]
      {
        if (polygon == null || polygon.Count == 0)
          continue;

        builder.BeginGeography(OpenGisGeographyType.Polygon);

        foreach (var ring in polygon) // Ring[]
        {
          if (ring == null || ring.Count < 4)
            continue; // invalid ring, skip or throw

          bool first = true;

          foreach (var coord in ring) // Point[]
          {
            if (coord.Length != 2)
              throw new ArgumentException("Coordinate must be [lon, lat].");

            double lon = Math.Round(coord[0], 5);
            double lat = Math.Round(coord[1], 5);

            if (first)
            {
              builder.BeginFigure(lat, lon);
              first = false;
            }
            else
            {
              builder.AddLine(lat, lon);
            }
          }
          builder.EndFigure(); // ring (outer or hole)
        }
        builder.EndGeography(); // polygon
      }
      builder.EndGeography(); // multipolygon
      return builder.ConstructedGeography;
    }

    public static SqlGeography BuildPolygon(List<List<double[]>> coordinates)
    {
      if (coordinates == null || coordinates.Count == 0)
        throw new ArgumentException("Polygon must contain at least one polygon.");

      var builder = new SqlGeographyBuilder();
      builder.SetSrid(4326);

      builder.BeginGeography(OpenGisGeographyType.Polygon);

      foreach (var ring in coordinates) // Ring[]
      {
        if (ring == null || ring.Count < 4)
          continue; // invalid ring skip

        bool first = true;

        foreach (var coord in ring) // Point[]
        {
          if (coord.Length != 2)
            throw new ArgumentException("Coordinate must be [lon, lat].");

          double lon = Math.Round(coord[0], 5);
          double lat = Math.Round(coord[1], 5);

          if (first)
          {
            builder.BeginFigure(lat, lon);
            first = false;
          }
          else
          {
            builder.AddLine(lat, lon);
          }
        }
        builder.EndFigure(); // ring (outer or hole)
      }
      builder.EndGeography(); // polygon

      return builder.ConstructedGeography;
    }
  }
}
