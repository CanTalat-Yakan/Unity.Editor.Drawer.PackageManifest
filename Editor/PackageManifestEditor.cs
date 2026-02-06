#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityEssentials
{
    public class PackageManifestEditor
    {
        private string _jsonPath;
        private PackageManifestData _jsonData;

        private readonly List<PackageManifestData.Dependency> _dependencies = new();
        private readonly List<string> _keywords = new();
        private readonly List<PackageManifestData.Sample> _samples = new();
        
        private ReorderableList _dependenciesList;
        private ReorderableList _keywordsList;
        private ReorderableList _samplesList;

        private bool _authorFoldout = true;
        private bool _linksFoldout = true;
        private bool _advancedFoldout;
        private bool _initialized;
        
        public EditorWindowBuilder Window;
        public Action Repaint;
        public Action Close;

        public PackageManifestEditor(string path) =>
            _jsonPath = path;

        public void Initialization()
        {
            if (!string.IsNullOrEmpty(_jsonPath) && File.Exists(_jsonPath))
            {
                string json = File.ReadAllText(_jsonPath);
                _jsonData = PackageManifestUtilities.DeserializeOrNew(json, out _);

                // Shared lists
                _dependencies.Clear();
                foreach (var kvp in _jsonData.dependencies)
                    _dependencies.Add(new PackageManifestData.Dependency { name = kvp.Key, version = kvp.Value });

                _keywords.Clear();
                _keywords.AddRange(_jsonData.keywords);

                _samples.Clear();
                _samples.AddRange(_jsonData.samples);

                _dependenciesList = PackageManifestReorderableLists.CreateDependenciesIMGUI(
                    _dependencies,
                    i => _dependencies[i],
                    onAdd: () => _dependencies.Add(new PackageManifestData.Dependency
                    {
                        name = "com.example.new-package",
                        version = "1.0.0"
                    }));

                _keywordsList = PackageManifestReorderableLists.CreateKeywordsIMGUI(
                    _keywords,
                    onAdd: () => _keywords.Add(string.Empty));

                _samplesList = PackageManifestReorderableLists.CreateSamplesIMGUI(
                    _samples,
                    onAdd: () => _samples.Add(new PackageManifestData.Sample
                    {
                        displayName = string.Empty,
                        description = string.Empty,
                        path = "Samples~/"
                    }));
            }
            else _jsonData = new();

            _initialized = true;

            GUI.FocusControl(null);
        }

        public void Header()
        {
            if (!_initialized)
            {
                EditorGUILayout.LabelField("Loading...");
                return;
            }

            if (_jsonData == null)
            {
                EditorGUILayout.LabelField("Invalid or missing package.json.");
                if (GUILayout.Button("Close"))
                    Close();
                return;
            }
            EditorGUILayout.LabelField("Information");

            EditorGUI.indentLevel++;
            {
                PackageManifestUtilities.ParsePackageName(_jsonData.name, out string organizationName, out string packageName);

                packageName = EditorGUILayout.TextField("Name", packageName);
                packageName = PackageManifestUtilities.SanitizeNamePart(packageName);

                organizationName = EditorGUILayout.TextField("Organization name", organizationName);
                organizationName = PackageManifestUtilities.SanitizeNamePart(organizationName);

                _jsonData.displayName = EditorGUILayout.TextField("Display Name", _jsonData.displayName);
                _jsonData.name = PackageManifestUtilities.ComposePackageName(organizationName, packageName);
                _jsonData.version = EditorGUILayout.TextField("Version", _jsonData.version);

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Unity Version");

                var unityVersionParts = (_jsonData.unity ?? "2022.1").Split('.');
                int unityMajor = 0, unityMinor = 0;
                int.TryParse(unityVersionParts[0], out unityMajor);
                if (unityVersionParts.Length > 1)
                    int.TryParse(unityVersionParts[1], out unityMinor);

                unityMajor = EditorGUILayout.IntField("Major", unityMajor);
                unityMinor = EditorGUILayout.IntField("Minor", unityMinor);
                _jsonData.unity = $"{unityMajor}.{unityMinor}";
                _jsonData.unityRelease = EditorGUILayout.TextField("Release", _jsonData.unityRelease);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Description");
            GUIStyle wordWrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _jsonData.description = EditorGUILayout.TextArea(_jsonData.description, wordWrapStyle, GUILayout.MinHeight(80));
        }

        public void Body()
        {
            EditorGUILayout.Space(10);

            _dependenciesList ??= PackageManifestReorderableLists.CreateDependenciesIMGUI(
                _dependencies,
                i => _dependencies[i],
                onAdd: () => _dependencies.Add(new PackageManifestData.Dependency
                {
                    name = "com.example.new-package",
                    version = "1.0.0"
                }));
            _dependenciesList.DoLayoutList();

            EditorGUILayout.Space();

            _keywordsList ??= PackageManifestReorderableLists.CreateKeywordsIMGUI(
                _keywords,
                onAdd: () => _keywords.Add(string.Empty));
            _keywordsList.DoLayoutList();

            EditorGUILayout.Space();

            _samplesList ??= PackageManifestReorderableLists.CreateSamplesIMGUI(
                _samples,
                onAdd: () => _samples.Add(new PackageManifestData.Sample
                {
                    displayName = string.Empty,
                    description = string.Empty,
                    path = "Samples~/"
                }));
            _samplesList.DoLayoutList();

            EditorGUILayout.Space(10);

            _authorFoldout = EditorGUILayout.Foldout(_authorFoldout, "Author", true);
            if (_authorFoldout)
            {
                EditorGUI.indentLevel++;
                _jsonData.author.name = EditorGUILayout.TextField("Name", _jsonData.author.name);
                _jsonData.author.email = EditorGUILayout.TextField("Email", _jsonData.author.email);
                _jsonData.author.url = EditorGUILayout.TextField("URL", _jsonData.author.url);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(10);

            _linksFoldout = EditorGUILayout.Foldout(_linksFoldout, "Links", true);
            if (_linksFoldout)
            {
                EditorGUI.indentLevel++;
                _jsonData.documentationUrl = EditorGUILayout.TextField("Documentation URL", _jsonData.documentationUrl);
                _jsonData.changelogUrl = EditorGUILayout.TextField("Changelog URL", _jsonData.changelogUrl);
                _jsonData.licensesUrl = EditorGUILayout.TextField("Licenses URL", _jsonData.licensesUrl);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            _advancedFoldout = EditorGUILayout.Foldout(_advancedFoldout, "Advanced", true);
            if (_advancedFoldout)
            {
                EditorGUI.indentLevel++;
                {
                    EditorGUILayout.HelpBox(
                        "If unchecked, the assets in this package will always " +
                        "be visible in the Project window and Object Picker." +
                        "\n(Default: hidden)", MessageType.Info);
                    _jsonData.hideInEditor = EditorGUILayout.Toggle(new GUIContent("Hide In Editor"), _jsonData.hideInEditor);
                }
                EditorGUI.indentLevel--;
            }
        }

        public void Footer()
        {
            using (new GUILayout.HorizontalScope())
            {
                string packageName = $"{_jsonData?.name ?? "N/A"} {_jsonData?.version ?? "N/A"}";
                GUIStyle packageNameLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic };
                packageNameLabelStyle.active.textColor = packageNameLabelStyle.normal.textColor * 0.85f;
                if (GUILayout.Button(packageName, packageNameLabelStyle))
                    EditorGUIUtility.systemCopyBuffer = _jsonData?.name;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Revert", GUILayout.Width(100)))
                    Initialization();
                if (GUILayout.Button("Apply", GUILayout.Width(100)))
                {
                    if (PackageManifestUtilities.SaveToFile(_jsonPath, _jsonData, _dependencies, _keywords, _samples, out _))
                        AssetDatabase.Refresh();
                    Close();
                }
            }
        }
    }
}
#endif