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
    }
}
#endif