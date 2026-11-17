using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Unity remembers the open scene only in the Library folder, which is not on GitHub,
// so a fresh clone opens an empty Untitled scene. This opens the game scene instead.
[InitializeOnLoad]
static class OpenStartScene
{
    const string ScenePath = "Assets/Scenes/Maps/Guns1.2.unity";

    static OpenStartScene()
    {
        EditorApplication.delayCall += OpenIfEmpty;
    }

    static void OpenIfEmpty()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SessionState.GetBool("OpenStartScene.Done", false)) return;
        SessionState.SetBool("OpenStartScene.Done", true);

        Scene active = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(active.path) || active.isDirty) return;   // a real scene is already open

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            EditorSceneManager.OpenScene(ScenePath);
    }
}
