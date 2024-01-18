using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(BiomeArrayAttribute))]
public class BiomeArrayItemDrawer : PropertyDrawer {
  public override VisualElement CreatePropertyGUI(SerializedProperty property) {
    var container = new VisualElement();

    // Default array item inspector
    var itemField = new PropertyField(property, "Biomes");
    container.Add(itemField);

    // Get a SerializedObject from the item
    var serializedObj = new SerializedObject((Biome)property.objectReferenceValue);

    // Temperature
    var debugColorField = new PropertyField(serializedObj.FindProperty("debugColor"));
    debugColorField.Bind(serializedObj);
    container.Add(debugColorField);

    // Temperature
    var temperatureField = new PropertyField(serializedObj.FindProperty("temperatureRange"));
    temperatureField.Bind(serializedObj);
    container.Add(temperatureField);

    // Precipitation
    var precipitationField = new PropertyField(serializedObj.FindProperty("precipitationRange"));
    precipitationField.Bind(serializedObj);
    container.Add(precipitationField);

    return container;
  }
}