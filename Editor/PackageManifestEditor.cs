#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace UnityEssentials
{
    public partial class PackageManifest
    {
        public EditorWindowBuilder Window;
        public Action Repaint;
        public Action Close;

        [MenuItem("Assets/Edit Package Manifest", true)]
        private static bool ValidateOpenPackageEditor()
        {
            const string checkPath = "package.json";
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return Path.GetFileName(path) == checkPath;
        }

        [MenuItem("Assets/Edit Package Manifest", false, -79)]
        private static void ShowWindow()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            var editor = new PackageManifest(path);
            var window = EditorWindowBuilder
                .CreateInstance("Edit Package Manifest", new(400, 500), new(700, 800))
                .SetInitialization(editor.Initialization)
                .SetHeader(editor.Header, EditorWindowStyle.HelpBox)
                .SetBody(editor.Body, EditorWindowStyle.BigMargin)
                .SetFooter(editor.Footer, EditorWindowStyle.HelpBox)
                .GetRepaintEvent(out editor.Repaint)
                .GetCloseEvent(out editor.Close)
                .ShowAsUtility();
        }

        private void Initialization()
        {
            if (!string.IsNullOrEmpty(_jsonPath) && File.Exists(_jsonPath))
            {
                string json = File.ReadAllText(_jsonPath);
                _jsonData = JsonConvert.DeserializeObject<PackageManifestData>(json) ?? new PackageManifestData();

                _jsonData.dependencies ??= new();
                InitializeDependenciesList();

                _jsonData.keywords ??= new();
                InitializeKeywordsList();

                _jsonData.samples ??= new();
                InitializeSamplesList();
            }
            else _jsonData = new();

            _initialized = true; 

            GUI.FocusControl(null);
        }

        private void Header()
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
                ParsePackageName(_jsonData.name, out string organizationName, out string packageName);

                packageName = EditorGUILayout.TextField("Name", packageName);
                packageName = SanitizeNamePart(packageName);

                organizationName = EditorGUILayout.TextField("Organization name", organizationName);
                organizationName = SanitizeNamePart(organizationName);

                _jsonData.displayName = EditorGUILayout.TextField("Display Name", _jsonData.displayName);
                _jsonData.name = ComposePackageName(organizationName, packageName);
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

        private void Body()
        {
            EditorGUILayout.Space(10);

            if (_dependenciesList == null)
                InitializeDependenciesList();
            _dependenciesList.DoLayoutList();

            EditorGUILayout.Space();

            if (_keywordsList == null)
                InitializeKeywordsList();
            _keywordsList.DoLayoutList();

            EditorGUILayout.Space();

            if (_samplesList == null)
                InitializeSamplesList();
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

        private void Footer()
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
                    Save();
            }
        }
    }
}
#endif