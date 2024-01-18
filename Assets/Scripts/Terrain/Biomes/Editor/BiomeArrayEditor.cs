using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(BiomeArray))]
public class BiomeArrayEditor : PropertyDrawer {
  public override VisualElement CreatePropertyGUI(SerializedProperty property) {
    var container = new VisualElement();

    var arrayField = new PropertyField(property.FindPropertyRelative("array"), "Biomes");

    var popup = new UnityEngine.UIElements.PopupWindow();
    popup.text = "Biome Chart";

    var biomeArray = (BiomeArray)property.boxedValue;
    var chart = new BiomeChart();
    chart.style.height = 400;
    chart.biomes = biomeArray;

    // Repaint the chart when the biomes are updated
    arrayField.RegisterCallback<SerializedPropertyChangeEvent>((evt) => {
      var biomeArray = (BiomeArray)property.boxedValue;
      chart.biomes = biomeArray;
    });

    popup.Add(chart);
    container.Add(popup);
    container.Add(arrayField);

    return container;
  }
}