using UnityEngine;
using UnityEngine.UIElements;

public class BiomeChart : VisualElement {
  public Biome[] biomes {
    get { return m_biomes; }
    set {
      m_biomes = value;
      MarkDirtyRepaint();
    }
  }
  private Biome[] m_biomes = new Biome[0];

  public BiomeChart() {
    style.width = new Length(100f, LengthUnit.Percent);

    generateVisualContent += OnGenerateVisualContent;
  }

  private void OnGenerateVisualContent(MeshGenerationContext mgc) {
    Painter2D paint2D = mgc.painter2D;

    // Size to render the chart
    float totalWidth = resolvedStyle.width;
    float totalHeight = resolvedStyle.height;

    // We need these min and max values to normalize the temperature and precipitation
    float minTemperature = float.MaxValue;
    float maxTemperature = float.MinValue;
    float minPrecipitation = float.MaxValue;
    float maxPrecipitation = float.MinValue;
    for (int i = 0; i < m_biomes.Length; i++) {
      Biome biome = m_biomes[i];
      if (biome == null) {
        continue;
      }

      if (biome.temperatureRange.min < minTemperature) {
        minTemperature = biome.temperatureRange.min;
      } else if (biome.temperatureRange.max > maxTemperature) {
        maxTemperature = biome.temperatureRange.max;
      }

      if (biome.precipitationRange.min < minPrecipitation) {
        minPrecipitation = biome.precipitationRange.min;
      } else if (biome.precipitationRange.max > maxPrecipitation) {
        maxPrecipitation = biome.precipitationRange.max;
      }
    }

    // Iterate the biomes to draw a rectangle for each one
    foreach (var biome in biomes) {
      if (biome == null) {
        continue;
      }

      // Fill Color
      Color color = biome.debugColor;
      color.a = 0.5f;
      paint2D.fillColor = color;

      // Beging path
      float tempMin = MathUtils.LinearInterpolation(biome.temperatureRange.min, minTemperature, maxTemperature, 0f, totalWidth);
      float tempMax = MathUtils.LinearInterpolation(biome.temperatureRange.max, minTemperature, maxTemperature, 0f, totalWidth);
      float preMin = MathUtils.LinearInterpolation(biome.precipitationRange.min, minPrecipitation, maxPrecipitation, totalHeight, 0f);
      float preMax = MathUtils.LinearInterpolation(biome.precipitationRange.max, minPrecipitation, maxPrecipitation, totalHeight, 0f);
      paint2D.BeginPath();
      paint2D.MoveTo(new Vector2(tempMin, preMin));
      paint2D.LineTo(new Vector2(tempMax, preMin));
      paint2D.LineTo(new Vector2(tempMax, preMax));
      paint2D.LineTo(new Vector2(tempMin, preMax));
      paint2D.ClosePath();

      // Fill
      paint2D.Fill();

      // Stroke
      paint2D.fillColor = Color.black;
      paint2D.lineWidth = 2;
      paint2D.Stroke();
    }
  }
}