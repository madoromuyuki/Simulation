using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PondChecks
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var pond = Object.FindFirstObjectByType<TidepoolEvolution>();
        if (pond == null || pond.organismPrefabs.Length != 3 || pond.organismRoot.childCount == 0)
            throw new System.Exception("Missing pond objects.");
        Debug.Log("POND_SCENE_CHECK_PASSED");
    }
}
