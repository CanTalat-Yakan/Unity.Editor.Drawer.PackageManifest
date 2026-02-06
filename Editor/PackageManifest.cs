#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace UnityEssentials
{
    public class PackageManifest
    {
        [InitializeOnLoadMethod]
        private static void Initialize() =>
            TextAssetHook.Add(PackageManifestRenderer.RenderFunction, ".json");

        [MenuItem("Assets/Edit Package Manifest", true)]
        private static bool ValidateOpenPackageEditor()
        {
            const string checkPath = "package.json";
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return Path.GetFileName(path) == checkPath;
        }

        [MenuItem("Assets/Edit Package Manifest", false, 1100)]
        private static void ShowWindow()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            var editor = new PackageManifestEditor(path);
            EditorWindowBuilder
                .CreateInstance("Edit Package Manifest", new(400, 500), new(700, 800))
                .SetInitialization(editor.Initialization)
                .SetHeader(editor.Header, EditorWindowStyle.HelpBox)
                .SetBody(editor.Body, EditorWindowStyle.BigMargin)
                .SetFooter(editor.Footer, EditorWindowStyle.HelpBox)
                .GetRepaintEvent(out editor.Repaint)
                .GetCloseEvent(out editor.Close)
                .ShowAsUtility();
        }

    }
}
#endif