#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;

namespace UnityEssentials
{
    internal static class PackageManifestAsmdefDependencyFetcher
    {
        [Serializable]
        private sealed class AsmdefData
        {
            public string name;
            public string[] references;
        }

        public readonly struct FetchResult
        {
            public readonly List<PackageManifestData.Dependency> Dependencies;
            public readonly List<string> Unresolved;
            public readonly List<string> Warnings;

            public FetchResult(List<PackageManifestData.Dependency> dependencies, List<string> unresolved, List<string> warnings)
            {
                Dependencies = dependencies;
                Unresolved = unresolved;
                Warnings = warnings;
            }
        }

        public static FetchResult FetchFromPackageRoot(string packageJsonPath, bool includeUnityPackages = true)
        {
            var deps = new Dictionary<string, PackageManifestData.Dependency>(StringComparer.OrdinalIgnoreCase);
            var unresolved = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrEmpty(packageJsonPath) || !File.Exists(packageJsonPath))
            {
                warnings.Add("package.json path is invalid.");
                return new FetchResult(new List<PackageManifestData.Dependency>(), unresolved, warnings);
            }

            // Read current package name so we can ignore self.
            TryReadPackageId(packageJsonPath, out var currentPackageName, out _, out var currentReadError);
            if (string.IsNullOrWhiteSpace(currentPackageName) && !string.IsNullOrWhiteSpace(currentReadError))
                warnings.Add($"Unable to read current package name: {currentReadError}");

            var packageRoot = Path.GetDirectoryName(packageJsonPath);
            if (string.IsNullOrEmpty(packageRoot) || !Directory.Exists(packageRoot))
            {
                warnings.Add("package.json folder is invalid.");
                return new FetchResult(new List<PackageManifestData.Dependency>(), unresolved, warnings);
            }

            // Build a name->asmdefPath index so name-based references can be resolved.
            var asmdefNameToPath = BuildAsmdefNameIndex();

            // Find asmdefs in this package.
            var packageAsmdefs = Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories)
                .Where(p => !p.Contains(string.Concat(Path.DirectorySeparatorChar, "Library", Path.DirectorySeparatorChar)))
                .ToArray();

            if (packageAsmdefs.Length == 0)
            {
                warnings.Add("No .asmdef files found under this package.");
                return new FetchResult(new List<PackageManifestData.Dependency>(), unresolved, warnings);
            }

            // Resolve dependencies of all asmdefs.
            var referencedAsmdefPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asmdefPath in packageAsmdefs)
            {
                TryParseAsmdef(asmdefPath, out var asmdef, out var parseError);
                if (asmdef == null)
                {
                    unresolved.Add($"{ToProjectRelativeIfPossible(asmdefPath)} (parse error: {parseError})");
                    continue;
                }

                if (asmdef.references == null)
                    continue;

                foreach (var reference in asmdef.references)
                {
                    if (string.IsNullOrWhiteSpace(reference))
                        continue;

                    var resolvedPath = ResolveAsmdefReferenceToPath(reference, asmdefNameToPath);
                    if (string.IsNullOrEmpty(resolvedPath))
                    {
                        unresolved.Add(reference);
                        continue;
                    }

                    // Ignore self references.
                    if (Path.GetFullPath(resolvedPath) == Path.GetFullPath(asmdefPath))
                        continue;

                    referencedAsmdefPaths.Add(resolvedPath);
                }
            }

            foreach (var referencedAsmdefPath in referencedAsmdefPaths)
            {
                var referencedFolder = Path.GetDirectoryName(referencedAsmdefPath);
                if (string.IsNullOrEmpty(referencedFolder) || !Directory.Exists(referencedFolder))
                {
                    unresolved.Add(ToProjectRelativeIfPossible(referencedAsmdefPath));
                    continue;
                }

                if (!TryFindNearestPackageJson(referencedFolder, out var depPackageJsonPath))
                {
                    unresolved.Add(ToProjectRelativeIfPossible(referencedAsmdefPath));
                    continue;
                }

                TryReadPackageId(depPackageJsonPath, out var packageName, out var packageVersion, out var readError);
                if (string.IsNullOrEmpty(packageName))
                {
                    unresolved.Add($"{ToProjectRelativeIfPossible(depPackageJsonPath)} (invalid package.json: {readError})");
                    continue;
                }

                // Ignore self dependency.
                if (!string.IsNullOrWhiteSpace(currentPackageName) &&
                    string.Equals(packageName, currentPackageName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!includeUnityPackages && packageName.StartsWith("com.unity.", StringComparison.OrdinalIgnoreCase))
                    continue;

                deps[packageName] = new PackageManifestData.Dependency
                {
                    name = packageName,
                    version = packageVersion ?? string.Empty
                };
            }

            var list = deps.Values.OrderBy(d => d.name, StringComparer.OrdinalIgnoreCase).ToList();
            return new FetchResult(list, unresolved.Distinct().OrderBy(s => s).ToList(), warnings);
        }

        private static Dictionary<string, string> BuildAsmdefNameIndex()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allAsmdefs = AssetDatabase.FindAssets("t:asmdef");
            foreach (var guid in allAsmdefs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    continue;

                TryParseAsmdef(path, out var asmdef, out _);
                if (asmdef == null || string.IsNullOrEmpty(asmdef.name))
                    continue;

                if (!dict.ContainsKey(asmdef.name))
                    dict.Add(asmdef.name, path);
                // If there are duplicates, keep the first and let unresolved paths be handled via GUID refs.
            }
            return dict;
        }

        private static string ResolveAsmdefReferenceToPath(string reference, Dictionary<string, string> asmdefNameToPath)
        {
            // Common formats:
            // - "GUID:xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
            // - "Unity.Some.Assembly" (asmdef name)
            if (reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
            {
                var guid = reference.Substring("GUID:".Length).Trim();
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return string.IsNullOrEmpty(path) ? null : path;
            }

            return asmdefNameToPath.TryGetValue(reference.Trim(), out var byNamePath) ? byNamePath : null;
        }

        private static void TryParseAsmdef(string asmdefPathOrProjectRelative, out AsmdefData asmdef, out string error)
        {
            asmdef = null;
            error = null;
            try
            {
                // AssetDatabase paths are project-relative. Directory.GetFiles returns absolute paths.
                var path = asmdefPathOrProjectRelative;
                if (!path.Contains("Assets") && File.Exists(path) == false)
                {
                    error = "Path not found.";
                    return;
                }

                var fullPath = File.Exists(path) ? path : Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    // Might be project-relative
                    var combined = Path.GetFullPath(path);
                    if (!File.Exists(combined))
                    {
                        error = "File not found.";
                        return;
                    }
                    fullPath = combined;
                }

                var json = File.ReadAllText(fullPath);
                asmdef = JsonConvert.DeserializeObject<AsmdefData>(json);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                asmdef = null;
            }
        }

        private static bool TryFindNearestPackageJson(string startFolder, out string packageJsonPath)
        {
            packageJsonPath = null;
            try
            {
                var dir = new DirectoryInfo(startFolder);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "package.json");
                    if (File.Exists(candidate))
                    {
                        packageJsonPath = candidate;
                        return true;
                    }

                    // Stop at project root (folder containing Assets/).
                    if (Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                        break;

                    dir = dir.Parent;
                }
            }
            catch { }

            return false;
        }

        private static void TryReadPackageId(string packageJsonPath, out string name, out string version, out string error)
        {
            name = null;
            version = null;
            error = null;

            try
            {
                var json = PackageManifestUtilities.SafeReadFile(packageJsonPath, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "File is empty.";
                    return;
                }

                // Reuse the existing schema so fields match the editor.
                var data = PackageManifestUtilities.TryDeserialize(json, out var deserializeError);
                if (data == null)
                {
                    error = deserializeError ?? "Unknown JSON error.";
                    return;
                }

                name = data.name;
                version = data.version;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }

        private static string ToProjectRelativeIfPossible(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            path = path.Replace('\\', '/');
            var assetsIndex = path.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
                return path.Substring(assetsIndex + 1);

            return path;
        }
    }
}
#endif

