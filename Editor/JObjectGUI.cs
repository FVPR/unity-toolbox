using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public class JObjectGUI
{
    public static void DrawJObject(JObject obj)
    {
        if (obj == null)
        {
            EditorGUILayout.LabelField("null");
            return;
        }

        foreach (var property in obj.Properties())
            DrawProperty(obj, property);
    }

    public static void DrawProperty(JObject obj, JProperty property)
    {
        switch (property.Value.Type)
        {
            case JTokenType.String:
                DrawStringProperty(obj, property);
                break;
            case JTokenType.Boolean:
                DrawBooleanProperty(obj, property);
                break;
            case JTokenType.Integer:
                DrawIntegerProperty(obj, property);
                break;
            case JTokenType.Float:
                DrawFloatProperty(obj, property);
                break;
            case JTokenType.Object:
                DrawJObjectProperty(obj, property);
                break;
            case JTokenType.Array:
                DrawJArrayProperty(obj, property);
                break;
            default:
                EditorGUILayout.HelpBox($"Unsupported type: {property.Value.Type}", MessageType.Error);
                break;
        }
    }

    public static void DrawStringProperty(JObject obj, JProperty property)
    {
        string value = property.Value.Value<string>();
        string newValue = EditorGUILayout.TextField(property.Name, value);

        if (newValue != value)
        {
            obj[property.Name] = newValue;
        }
    }

    public static void DrawBooleanProperty(JObject obj, JProperty property)
    {
        bool value = property.Value.Value<bool>();
        bool newValue = EditorGUILayout.Toggle(property.Name, value);

        if (newValue != value)
        {
            obj[property.Name] = newValue;
        }
    }

    public static void DrawIntegerProperty(JObject obj, JProperty property)
    {
        int value = property.Value.Value<int>();
        int newValue = EditorGUILayout.IntField(property.Name, value);

        if (newValue != value)
        {
            obj[property.Name] = newValue;
        }
    }

    public static void DrawFloatProperty(JObject obj, JProperty property)
    {
        float value = property.Value.Value<float>();
        float newValue = EditorGUILayout.FloatField(property.Name, value);

        if (newValue != value)
        {
            obj[property.Name] = newValue;
        }
    }

    public static void DrawJObjectProperty(JObject obj, JProperty property)
    {
        EditorGUILayout.LabelField(property.Name);
        EditorGUI.indentLevel++;

        var newObj = obj[property.Name] as JObject ?? new JObject();
        DrawJObject(newObj);
        obj[property.Name] = newObj;

        EditorGUI.indentLevel--;
    }

    public static void DrawJArrayProperty(JObject obj, JProperty property)
    {
        EditorGUILayout.LabelField(property.Name);
        EditorGUI.indentLevel++;        

        var newArray = obj[property.Name] as JArray ?? new JArray();
        DrawJArray(newArray);
        obj[property.Name] = newArray;

        EditorGUI.indentLevel--;
    }

    public static void DrawJArray(JArray array)
    {
        for (int i = 0; i < array.Count; i++)
        {
            var element = array[i];

            switch (element.Type)
            {
                case JTokenType.String:
                    DrawStringElement(array, i);
                    break;
                case JTokenType.Boolean:
                    DrawBooleanElement(array, i);
                    break;
                case JTokenType.Integer:
                    DrawIntegerElement(array, i);
                    break;
                case JTokenType.Float:
                    DrawFloatElement(array, i);
                    break;
                case JTokenType.Object:
                    DrawJObjectElement(array, i);
                    break;
                case JTokenType.Array:
                    DrawJArrayElement(array, i);
                    break;
                default:
                    EditorGUILayout.HelpBox($"Unsupported type: {element.Type}", MessageType.Error);
                    break;
            }
        }
    }
    
    public static void DrawStringElement(JArray array, int index)
    {
        string value = array[index].Value<string>();
        string newValue = EditorGUILayout.TextField(value);

        if (newValue != value)
        {
            array[index] = newValue;
        }
    }
    
    public static void DrawBooleanElement(JArray array, int index)
    {
        bool value = array[index].Value<bool>();
        bool newValue = EditorGUILayout.Toggle(value);

        if (newValue != value)
        {
            array[index] = newValue;
        }
    }
    
    public static void DrawIntegerElement(JArray array, int index)
    {
        int value = array[index].Value<int>();
        int newValue = EditorGUILayout.IntField(value);

        if (newValue != value)
        {
            array[index] = newValue;
        }
    }
    
    public static void DrawFloatElement(JArray array, int index)
    {
        float value = array[index].Value<float>();
        float newValue = EditorGUILayout.FloatField(value);

        if (newValue != value)
        {
            array[index] = newValue;
        }
    }
    
    public static void DrawJObjectElement(JArray array, int index)
    {
        var newObj = array[index] as JObject ?? new JObject();
        DrawJObject(newObj);
        array[index] = newObj;
    }
    
    public static void DrawJArrayElement(JArray array, int index)
    {
        var newArray = array[index] as JArray ?? new JArray();
        DrawJArray(newArray);
        array[index] = newArray;
    }
}
