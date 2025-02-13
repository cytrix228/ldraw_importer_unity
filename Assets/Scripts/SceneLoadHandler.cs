using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using LDraw;

public static class SceneLoadHandler
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
    	Debug.Log("Execute Initialize()");
    	Console.Write("Register OnSceneLoaded()");
    	
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("Scene loaded: " + scene.name);
        // Your post-load logic here

        //Debug.Log("LDrawConfig.Instance : " + LDrawConfig.Instance);
    }
}
