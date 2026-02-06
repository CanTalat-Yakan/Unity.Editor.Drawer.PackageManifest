#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEssentials
{
    public class PackageManifestRenderer
    {
        public static VisualElement RenderFunction(string content, string assetPath)
        {
            if (!Path.GetFileName(assetPath).Equals("package.json", StringComparison.InvariantCultureIgnoreCase))
                return null;

            var data = PackageManifestUtilities.DeserializeOrNew(content, out var error);

            var dependencies = new List<PackageManifestData.Dependency>();
            foreach (var kvp in data.dependencies)
                dependencies.Add(new PackageManifestData.Dependency { name = kvp.Key, version = kvp.Value });

            var keywords = new List<string>();
            keywords.AddRange(data.keywords);

            var samples = new List<PackageManifestData.Sample>();
            samples.AddRange(data.samples);

            var host = new VisualElement();
            host.SetFlex(grow: 1, direction: FlexDirection.Column);

            var root = new VisualElement();
            root.SetFlex(grow: 1, direction: FlexDirection.Column);
            root.SetPadding(6);
            host.Add(root);

            if (!string.IsNullOrWhiteSpace(error))
                root.Add(new HelpBox($"Invalid JSON: {error}", HelpBoxMessageType.Error));

            // Main scroll
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.SetFlex(grow: 1);
            scroll.SetScrollbarVisibility(horizontal: ScrollerVisibility.Hidden);
            scroll.SetMinWidth(0);
            scroll.contentContainer.SetPadding(0, 3, 0, 0);
            root.Add(scroll);

            var footer = new VisualElement();
            var packageLabel = new Label();
            var revertBtn = new Button();
            var applyBtn = new Button();

            void UpdateFooter()
            {
                var pn = data.name ?? "N/A";
                var ver = data.version ?? "N/A";
                packageLabel.text = $"{pn} {ver}";
                applyBtn.SetEnabled(!string.IsNullOrEmpty(assetPath));
            }

            Label SectionTitle(string text)
            {
                var l = new Label(text);
                l.SetFont(style: FontStyle.Bold);
                l.SetMargin(0, 0, 2, 2);
                return l;
            }

            VisualElement Space(float px) =>
                new() { style = { height = px } };

            TextField TextField(string label, string value, Action<string> onChanged = null)
            {
                var tf = new TextField(label) { value = value ?? string.Empty };
                tf.labelElement.SetMinWidth(160);
                tf.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
                return tf;
            }

            IntegerField IntegerField(string label, int value)
            {
                var f = new IntegerField(label) { value = value };
                f.labelElement.SetMinWidth(160);
                return f;
            }

            TextField TextArea(string label, string value, float minHeight)
            {
                var tf = new TextField(label) { value = value ?? string.Empty, multiline = true };
                tf.SetMinHeight((int)minHeight);
                tf.labelElement.SetMinWidth(160);
                return tf;
            }

            // --- Information ---
            scroll.Add(SectionTitle("Information"));

            PackageManifestUtilities.ParsePackageName(data.name, out var organizationName, out var packageName);

            var nameField = TextField("Name", packageName);
            var orgField = TextField("Organization name", organizationName);
            var displayNameField = TextField("Display Name", data.displayName);
            var versionField = TextField("Version", data.version);

            scroll.Add(nameField);
            scroll.Add(orgField);
            scroll.Add(displayNameField);
            scroll.Add(versionField);

            scroll.Add(Space(6));
            scroll.Add(SectionTitle("Unity Version"));

            var unityVersionParts = (data.unity ?? "2022.1").Split('.');
            int.TryParse(unityVersionParts.ElementAtOrDefault(0), out var unityMajor);
            int.TryParse(unityVersionParts.ElementAtOrDefault(1), out var unityMinor);

            var unityMajorField = IntegerField("Major", unityMajor);
            var unityMinorField = IntegerField("Minor", unityMinor);
            var unityReleaseField = TextField("Release", data.unityRelease);

            scroll.Add(unityMajorField);
            scroll.Add(unityMinorField);
            scroll.Add(unityReleaseField);

            scroll.Add(Space(10));

            scroll.Add(SectionTitle("Description"));

            var descriptionField = TextArea("", data.description, minHeight: 90);
            descriptionField.label = "";
            scroll.Add(descriptionField);

            scroll.Add(Space(10));

            // --- Dependencies ---
            var dependenciesList = PackageManifestReorderableLists.CreateDependenciesUitk(dependencies);
            scroll.Add(dependenciesList.DoLayoutList());

            // --- Keywords ---
            scroll.Add(Space(10));
            var keywordsList = PackageManifestReorderableLists.CreateKeywordsUitk(keywords);
            scroll.Add(keywordsList.DoLayoutList());

            // --- Samples ---
            scroll.Add(Space(10));
            var samplesList = PackageManifestReorderableLists.CreateSamplesUitk(samples);
            scroll.Add(samplesList.DoLayoutList());

            // --- Foldouts ---
            scroll.Add(Space(10));

            var authorFoldout = new Foldout { text = "Author", value = true };
            var authorNameField = TextField("Name", data.author.name);
            var authorEmailField = TextField("Email", data.author.email);
            var authorUrlField = TextField("URL", data.author.url);
            authorFoldout.Add(authorNameField);
            authorFoldout.Add(authorEmailField);
            authorFoldout.Add(authorUrlField);
            scroll.Add(authorFoldout);

            scroll.Add(Space(10));

            var linksFoldout = new Foldout { text = "Links", value = true };
            var documentationUrlField = TextField("Documentation URL", data.documentationUrl);
            var changelogUrlField = TextField("Changelog URL", data.changelogUrl);
            var licensesUrlField = TextField("Licenses URL", data.licensesUrl);
            linksFoldout.Add(documentationUrlField);
            linksFoldout.Add(changelogUrlField);
            linksFoldout.Add(licensesUrlField);
            scroll.Add(linksFoldout);

            scroll.Add(Space(10));

            var advancedFoldout = new Foldout { text = "Advanced", value = false };
            advancedFoldout.Add(new HelpBox(
                "If unchecked, the assets in this package will always be visible in the Project window and Object Picker.\n(Default: hidden)",
                HelpBoxMessageType.Info));

            var hideInEditorToggle = new Toggle("Hide In Editor") { value = data.hideInEditor };
            advancedFoldout.Add(hideInEditorToggle);
            scroll.Add(advancedFoldout);

            // --- Footer ---
            footer.SetFlex(direction: FlexDirection.Row, alignItems: Align.Center);
            footer.SetMargin(0, 0, 8, 0);
            footer.SetPadding(0, 0, 6, 6);
            footer.SetTopBorder(1);

            packageLabel.SetFont(style: FontStyle.Italic);
            packageLabel.SetFlex(grow: 1);
            packageLabel.SetOpacity(0.85f);
            packageLabel.RegisterCallback<PointerDownEvent>(_ =>
            {
                EditorGUIUtility.systemCopyBuffer = data.name;
            });

            revertBtn.text = "Revert";
            revertBtn.style.width = 100;
            revertBtn.clicked += () =>
            {
                var json = PackageManifestUtilities.SafeReadFile(assetPath, fallback: content);
                host.Clear();
                host.Add(RenderFunction(json, assetPath));
            };

            applyBtn.text = "Apply";
            applyBtn.style.width = 100;
            applyBtn.clicked += () =>
            {
                try
                {
                    // Make sure delayed text fields flush their edits before reading values.
                    // (List items use isDelayed=true.)
                    // UI Toolkit doesn't expose a generic Panel.Focus(), so we shift focus to a dummy element.
                    var focusSink = new VisualElement { focusable = true };
                    host.Add(focusSink);
                    focusSink.Focus();
                    host.Remove(focusSink);

                    data.name = PackageManifestUtilities.ComposePackageName(orgField.value, nameField.value);
                    data.displayName = displayNameField.value;
                    data.version = versionField.value;

                    var major = Mathf.Max(0, unityMajorField.value);
                    var minor = Mathf.Max(0, unityMinorField.value);
                    data.unity = $"{major}.{minor}";
                    data.unityRelease = unityReleaseField.value;

                    data.description = descriptionField.value;

                    data.author.name = authorNameField.value;
                    data.author.email = authorEmailField.value;
                    data.author.url = authorUrlField.value;

                    data.documentationUrl = documentationUrlField.value;
                    data.changelogUrl = changelogUrlField.value;
                    data.licensesUrl = licensesUrlField.value;

                    data.hideInEditor = hideInEditorToggle.value;

                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        if (PackageManifestUtilities.SaveToFile(assetPath, data, dependencies, keywords, samples, out _))
                        {
                            AssetDatabase.Refresh();
                            UpdateFooter();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            };

            footer.Add(packageLabel);
            footer.Add(revertBtn);
            footer.Add(applyBtn);
            root.Add(footer);

            UpdateFooter();

            return host;
        }
    }
}
#endif