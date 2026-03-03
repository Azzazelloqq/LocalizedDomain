using System;
using System.Collections.Generic;
using System.IO;
using LocalizedDomain;
using UnityEditor;
using UnityEngine;

namespace LocalizedDomain.Editor
{
[CustomPropertyDrawer(typeof(LocalizationKeyAttribute))]
public sealed class LocalizationKeyDrawer : PropertyDrawer
{
	private const string PrefsSettingsGuid = "LocalizedDomain.Editor.SettingsGuid";
	private static readonly LocalizationKeyCache Cache = new();

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		if (property.propertyType != SerializedPropertyType.String)
		{
			EditorGUI.PropertyField(position, property, label);
			return;
		}

		Cache.RefreshIfNeeded();
		var keys = Cache.Keys;
		if (keys.Count == 0)
		{
			EditorGUI.PropertyField(position, property, label);
			return;
		}

		var index = Mathf.Max(0, keys.IndexOf(property.stringValue));
		var newIndex = EditorGUI.Popup(position, label.text, index, keys.ToArray());
		if (newIndex >= 0 && newIndex < keys.Count)
		{
			property.stringValue = keys[newIndex];
		}
	}

	private sealed class LocalizationKeyCache
	{
		private readonly List<string> _keys = new();
		private string _lastPath;
		private DateTime _lastWrite;

		public List<string> Keys => _keys;

		public void RefreshIfNeeded()
		{
			var path = ResolveJsonPath();
			if (string.IsNullOrWhiteSpace(path))
			{
				_keys.Clear();
				return;
			}

			var fullPath = Path.GetFullPath(path);
			var exists = File.Exists(fullPath);
			if (!exists)
			{
				_keys.Clear();
				return;
			}

			var writeTime = File.GetLastWriteTimeUtc(fullPath);
			if (string.Equals(fullPath, _lastPath, StringComparison.Ordinal) &&
				writeTime == _lastWrite)
			{
				return;
			}

			_lastPath = fullPath;
			_lastWrite = writeTime;
			_keys.Clear();

			try
			{
				var json = File.ReadAllText(fullPath);
				var serializer = new LocalizationJsonSerializer();
				var project = serializer.Deserialize(json);
				foreach (var entry in project.Entries)
				{
					if (!string.IsNullOrWhiteSpace(entry?.Key))
					{
						_keys.Add(entry.Key);
					}
				}
			}
			catch (Exception)
			{
				_keys.Clear();
			}
		}

		private static string ResolveJsonPath()
		{
			var guid = EditorPrefs.GetString(PrefsSettingsGuid, string.Empty);
			if (string.IsNullOrWhiteSpace(guid))
			{
				return null;
			}

			var assetPath = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrWhiteSpace(assetPath))
			{
				return null;
			}

			var settings = AssetDatabase.LoadAssetAtPath<LocalizationEditorSettings>(assetPath);
			if (settings == null || string.IsNullOrWhiteSpace(settings.JsonPath))
			{
				return null;
			}

			var path = settings.JsonPath;
			if (Path.IsPathRooted(path))
			{
				return path;
			}

			var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			return Path.Combine(projectRoot, path);
		}
	}
}
}