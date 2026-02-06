#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace UnityEssentials
{
    /// <summary>
    /// Shared helpers for reading/writing and normalizing Unity package.json data.
    /// Keep this editor-only to avoid pulling Newtonsoft into runtime builds.
    /// </summary>
    internal static class PackageManifestUtilities
    {
        internal static PackageManifestData DeserializeOrNew(string json, out string error)
        {
            var data = TryDeserialize(json, out error) ?? new PackageManifestData();
            EnsureDefaults(data);
            return data;
        }

        internal static PackageManifestData TryDeserialize(string json, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return new PackageManifestData();

                return JsonConvert.DeserializeObject<PackageManifestData>(json);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        internal static string SafeReadFile(string path, string fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return fallback ?? string.Empty;
                return File.ReadAllText(path);
            }
            catch
            {
                return fallback ?? string.Empty;
            }
        }

        internal static void EnsureDefaults(PackageManifestData data)
        {
            if (data == null) return;
            data.dependencies ??= new Dictionary<string, string>();
            data.keywords ??= new List<string>();
            data.samples ??= new List<PackageManifestData.Sample>();
            data.author ??= new PackageManifestData.Author();
        }

        internal static void NormalizeForSave(PackageManifestData data)
        {
            EnsureDefaults(data);
            NormalizeDependencies(data);
            NormalizeKeywords(data);
        }

        internal static void NormalizeDependencies(PackageManifestData data)
        {
            data.dependencies ??= new Dictionary<string, string>();

            // Remove empty keys
            var emptyKeys = data.dependencies
                .Where(kvp => string.IsNullOrWhiteSpace(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var k in emptyKeys)
                data.dependencies.Remove(k);
        }

        internal static void NormalizeKeywords(PackageManifestData data)
        {
            data.keywords ??= new List<string>();
            // Trim entries and remove empties
            for (var i = data.keywords.Count - 1; i >= 0; i--)
            {
                var v = (data.keywords[i] ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(v)) data.keywords.RemoveAt(i);
                else data.keywords[i] = v;
            }
        }

        internal static void ParsePackageName(string fullName, out string organizationName, out string packageName)
        {
            organizationName = string.Empty;
            packageName = string.Empty;

            if (string.IsNullOrEmpty(fullName))
                return;

            var parts = fullName.Split('.');
            if (parts.Length >= 3 && parts[0] == "com")
            {
                organizationName = parts[1];
                packageName = string.Join(".", parts, 2, parts.Length - 2);
            }
        }

        internal static string ComposePackageName(string organizationName, string packageName)
        {
            organizationName = SanitizeNamePart(organizationName);
            packageName = SanitizeNamePart(packageName);
            return $"com.{organizationName}.{packageName}";
        }

        internal static string SanitizeNamePart(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            input = input.ToLowerInvariant().Replace(" ", "-");
            input = Regex.Replace(input, @"[^a-z0-9\-]", "");
            return input;
        }

        internal static void CopyInto(PackageManifestData src, PackageManifestData dst)
        {
            if (src == null || dst == null) return;

            // Mutate existing instance so closures keep working.
            dst.name = src.name;
            dst.version = src.version;
            dst.displayName = src.displayName;
            dst.description = src.description;
            dst.unity = src.unity;
            dst.unityRelease = src.unityRelease;

            dst.dependencies = src.dependencies ?? new Dictionary<string, string>();
            dst.keywords = src.keywords ?? new List<string>();
            dst.samples = src.samples ?? new List<PackageManifestData.Sample>();
            dst.author = src.author ?? new PackageManifestData.Author();

            dst.documentationUrl = src.documentationUrl;
            dst.changelogUrl = src.changelogUrl;
            dst.licensesUrl = src.licensesUrl;
            dst.hideInEditor = src.hideInEditor;
        }

        internal static void SyncListsIntoData(
            PackageManifestData data,
            List<PackageManifestData.Dependency> dependencies,
            List<string> keywords,
            List<PackageManifestData.Sample> samples)
        {
            EnsureDefaults(data);

            // Dependencies list -> dictionary
            data.dependencies.Clear();
            if (dependencies != null)
            {
                foreach (var d in dependencies)
                {
                    var key = d?.name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
                    data.dependencies[key] = d?.version ?? string.Empty;
                }
            }

            // Keywords list
            data.keywords.Clear();
            if (keywords != null)
                data.keywords.AddRange(keywords);

            // Samples list
            data.samples.Clear();
            if (samples != null)
                data.samples.AddRange(samples);

            NormalizeForSave(data);
        }

        internal static bool SaveToFile(
            string jsonPath,
            PackageManifestData data,
            List<PackageManifestData.Dependency> dependencies,
            List<string> keywords,
            List<PackageManifestData.Sample> samples,
            out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(jsonPath))
                {
                    error = "Invalid path.";
                    return false;
                }

                SyncListsIntoData(data, dependencies, keywords, samples);

                File.WriteAllText(jsonPath, data.ToJson());
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
#endif
