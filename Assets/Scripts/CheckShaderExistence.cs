using UnityEngine;
using UnityEditor;

public class CheckShaderExistence : MonoBehaviour
{
    [MenuItem("Tools/Check Shader Existence")]
    public static void CheckShader()
    {
        string shaderPath = "Assets/Shaders/YourShader.shader"; // Update this path to your shader file
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

        if (shader != null)
        {
            Debug.Log("Shader exists: " + shaderPath);
        }
        else
        {
            Debug.LogError("Shader does not exist: " + shaderPath);
        }
    }
}
