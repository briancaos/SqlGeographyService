using System.Text.Json;
using System.Text.Json.Serialization;

// Root
public class FeatureCollection
{
  [JsonPropertyName("type")]
  public string? Type { get; set; }

  [JsonPropertyName("features")]
  public List<Feature>? Features { get; set; }
}

public class Feature
{
  [JsonPropertyName("type")]
  public string? Type { get; set; }

  [JsonPropertyName("geometry")]
  public FeatureGeometry? Geometry { get; set; }

  [JsonPropertyName("properties")]
  public JsonElement? Properties { get; set; }
}

public class FeatureGeometry
{
  [JsonPropertyName("type")]
  public string? Type { get; set; }

  // MultiPolygon: double[polygon][ring][point][coordinate]
  // Polygon: double[ring][point][coordinate]
  [JsonPropertyName("coordinates")]
  public JsonElement? Coordinates { get; set; }
}

