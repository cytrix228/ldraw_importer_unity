using UnityEngine;
using UnityEditor;

public class GetShaderGUID : MonoBehaviour
{
    [MenuItem("Tools/Get Shader GUID")]
    public static void GetShaderGUIDMethod()
    {
        string shaderPath = "Assets/Shaders/YourShader.shader"; // Update this path to your shader file
        string guid = AssetDatabase.AssetPathToGUID(shaderPath);

        if (!string.IsNullOrEmpty(guid))
        {
            Debug.Log("Shader GUID: " + guid);
        }
        else
        {
            Debug.LogError("Shader not found at path: " + shaderPath);
        }
    }
}
