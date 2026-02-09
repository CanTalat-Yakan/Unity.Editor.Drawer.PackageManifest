#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEssentials
{
    /// <summary>
    /// Centralized factories for the IMGUI and UI Toolkit reorderable lists used by the package manifest editors.
    /// </summary>
    internal static class PackageManifestReorderableLists
    {
        // -----------------
        // IMGUI (ReorderableList)
        // -----------------

        internal static ReorderableList CreateDependenciesIMGUI(
            List<PackageManifestData.Dependency> dependencies,
            Func<int, PackageManifestData.Dependency> getElementAt,
            Action onAdd)
        {
            var list = new ReorderableList(dependencies, typeof(PackageManifestData.Dependency), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Dependencies"),
                drawElementCallback = (rect, index, _, _) =>
                {
                    var element = getElementAt(index);
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    float nameWidth = rect.width * 0.6f;
                    float versionWidth = rect.width * 0.4f - 20;

                    element.name = EditorGUI.TextField(
                        new Rect(rect.x, rect.y, nameWidth, rect.height),
                        GUIContent.none,
                        element.name);

                    element.version = EditorGUI.TextField(
                        new Rect(rect.x + nameWidth + 5, rect.y, versionWidth, rect.height),
                        GUIContent.none,
                        element.version);
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 4
            };

            list.onAddCallback = _ => onAdd?.Invoke();
            return list;
        }

        internal static ReorderableList CreateKeywordsIMGUI(List<string> keywords, Action onAdd)
        {
            var list = new ReorderableList(keywords, typeof(string), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Keywords"),
                drawElementCallback = (rect, index, _, _) =>
                {
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    keywords[index] = EditorGUI.TextField(rect, keywords[index]);
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 4
            };

            list.onAddCallback = _ => onAdd?.Invoke();
            return list;
        }

        internal static ReorderableList CreateSamplesIMGUI(List<PackageManifestData.Sample> samples, Action onAdd)
        {
            var list = new ReorderableList(samples, typeof(PackageManifestData.Sample), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Samples"),
                drawElementCallback = (rect, index, _, _) =>
                {
                    var element = samples[index];
                    float y = rect.y + 2;
                    float lineHeight = EditorGUIUtility.singleLineHeight;
                    float spacing = 2;

                    element.displayName = EditorGUI.TextField(
                        new Rect(rect.x, y, rect.width, lineHeight),
                        "Display Name", element.displayName);
                    y += lineHeight + spacing;

                    element.description = EditorGUI.TextField(
                        new Rect(rect.x, y, rect.width, lineHeight),
                        "Description", element.description);
                    y += lineHeight + spacing;

                    element.path = EditorGUI.TextField(
                        new Rect(rect.x, y, rect.width, lineHeight),
                        "Path", element.path);

                    samples[index] = element;
                },
                elementHeightCallback = _ => (EditorGUIUtility.singleLineHeight + 2) * 3
            };

            list.onAddCallback = _ => onAdd?.Invoke();
            return list;
        }

        // -----------------
        // UI Toolkit (plain list binding)
        // -----------------

        internal static CustomReorderableListUIToolkitPlain CreateDependenciesUitk(
            List<PackageManifestData.Dependency> dependencies)
        {
            return new CustomReorderableListUIToolkit<PackageManifestData.Dependency>(
                dependencies,
                header: "Dependencies",
                makeItem: () =>
                {
                    var row = new VisualElement();
                    row.SetFlex(direction: FlexDirection.Row, alignItems: Align.Center, alignContent: Align.Center);
                    row.SetPadding(20,0, 0,0);
                    row.SetMargin(3,8,2,2);

                    var nameField = new TextField { isDelayed = true };
                    var versionField = new TextField { isDelayed = true };

                    row.RegisterCallback<GeometryChangedEvent>(_ =>
                    {
                        float w = row.resolvedStyle.width;
                        if (w <= 0) return;
                        float nameWidth = w * 0.6f;
                        float versionWidth = w * 0.4f - 15f;
                        nameField.SetWidth((int)nameWidth);
                        versionField.SetWidth((int)Mathf.Max(60f, versionWidth));
                    });

                    nameField.SetMargin(0, 2, 0, 0);

                    row.Add(nameField);
                    row.Add(versionField);
                    return row;
                },
                bindItem: (e, index) =>
                {
                    var d = dependencies[index] ?? new PackageManifestData.Dependency();

                    var nameField = (TextField)e.ElementAt(0);
                    var versionField = (TextField)e.ElementAt(1);

                    nameField.SetValueWithoutNotify(d.name ?? string.Empty);
                    versionField.SetValueWithoutNotify(d.version ?? string.Empty);

                    nameField.UnregisterCallback<ChangeEvent<string>>(OnNameChanged);
                    versionField.UnregisterCallback<ChangeEvent<string>>(OnVersionChanged);

                    void OnNameChanged(ChangeEvent<string> evt)
                    {
                        d.name = evt.newValue ?? string.Empty;
                        dependencies[index] = d;
                    }

                    void OnVersionChanged(ChangeEvent<string> evt)
                    {
                        d.version = evt.newValue ?? string.Empty;
                        dependencies[index] = d;
                    }

                    nameField.RegisterCallback<ChangeEvent<string>>(OnNameChanged);
                    versionField.RegisterCallback<ChangeEvent<string>>(OnVersionChanged);
                },
                onAdd: () => dependencies.Add(new PackageManifestData.Dependency
                {
                    name = "com.example.new-package",
                    version = "1.0.0"
                }),
                onRemoveAt: i => dependencies.RemoveAt(i),
                maxHeight: 240f).AsNonGeneric();
        }

        internal static CustomReorderableListUIToolkitPlain CreateKeywordsUitk(List<string> keywords)
        {
            return new CustomReorderableListUIToolkit<string>(
                keywords,
                header: "Keywords",
                makeItem: () =>
                {
                    var tf = new TextField { isDelayed = true };
                    tf.SetFlex(direction: FlexDirection.Row, alignItems: Align.Center, alignContent: Align.Center);
                    tf.SetPadding(20,0,0,0);
                    tf.SetMargin(2,3,2,2);
                    tf.SetHeight(20);
                    return tf;
                },
                bindItem: (e, index) =>
                {
                    var tf = (TextField)e;
                    tf.SetValueWithoutNotify(keywords[index] ?? string.Empty);
                    tf.UnregisterCallback<ChangeEvent<string>>(OnChanged);

                    void OnChanged(ChangeEvent<string> evt)
                    {
                        keywords[index] = evt.newValue ?? string.Empty;
                    }

                    tf.RegisterCallback<ChangeEvent<string>>(OnChanged);
                },
                onAdd: () => keywords.Add(string.Empty),
                onRemoveAt: i => keywords.RemoveAt(i),
                maxHeight: 200f).AsNonGeneric();
        }

        internal static CustomReorderableListUIToolkitPlain CreateSamplesUitk(List<PackageManifestData.Sample> samples)
        {
            return new CustomReorderableListUIToolkit<PackageManifestData.Sample>(
                samples,
                header: "Samples",
                makeItem: () =>
                {
                    var box = new VisualElement();
                    box.SetFlex(direction: FlexDirection.Column, alignContent: Align.Center);
                    box.SetPadding(4 + 20, 4, 4, 4);
                    box.SetHeight(64);

                    var display = new TextField("Display Name") { isDelayed = true };
                    var desc = new TextField("Description") { isDelayed = true };
                    var path = new TextField("Path") { isDelayed = true };

                    display.SetMargin(0,0,3,0);
                    desc.SetMargin(0,0,2,0);
                    path.SetMargin(0,0,2,0);
                    
                    display.labelElement.SetMinWidth(160);
                    desc.labelElement.SetMinWidth(160);
                    path.labelElement.SetMinWidth(160);

                    box.Add(display);
                    box.Add(desc);
                    box.Add(path);
                    return box;
                },
                bindItem: (e, index) =>
                {
                    var s = samples[index] ?? new PackageManifestData.Sample();

                    var display = (TextField)e.ElementAt(0);
                    var desc = (TextField)e.ElementAt(1);
                    var path = (TextField)e.ElementAt(2);

                    display.SetValueWithoutNotify(s.displayName ?? string.Empty);
                    desc.SetValueWithoutNotify(s.description ?? string.Empty);
                    path.SetValueWithoutNotify(s.path ?? string.Empty);

                    display.UnregisterCallback<ChangeEvent<string>>(OnDisplay);
                    desc.UnregisterCallback<ChangeEvent<string>>(OnDesc);
                    path.UnregisterCallback<ChangeEvent<string>>(OnPath);

                    void OnDisplay(ChangeEvent<string> evt)
                    {
                        s.displayName = evt.newValue ?? string.Empty;
                        samples[index] = s;
                    }

                    void OnDesc(ChangeEvent<string> evt)
                    {
                        s.description = evt.newValue ?? string.Empty;
                        samples[index] = s;
                    }

                    void OnPath(ChangeEvent<string> evt)
                    {
                        s.path = evt.newValue ?? string.Empty;
                        samples[index] = s;
                    }

                    display.RegisterCallback<ChangeEvent<string>>(OnDisplay);
                    desc.RegisterCallback<ChangeEvent<string>>(OnDesc);
                    path.RegisterCallback<ChangeEvent<string>>(OnPath);
                },
                onAdd: () => samples.Add(new PackageManifestData.Sample
                {
                    displayName = string.Empty,
                    description = string.Empty,
                    path = "Samples~/"
                }),
                onRemoveAt: i => samples.RemoveAt(i),
                maxHeight: 360f).AsNonGeneric();
        }
    }
}
#endif