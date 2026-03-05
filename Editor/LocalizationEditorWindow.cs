using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LocalizedDomain;
using UnityEditor;
using UnityEngine;

namespace LocalizedDomain.Editor
{
public sealed class LocalizationEditorWindow : EditorWindow
{
	private const string PrefsSettingsGuid = "LocalizedDomain.Editor.SettingsGuid";
	private const string PrefsKeySearch = "LocalizedDomain.Editor.KeySearch";
	private const string PrefsLocaleSearch = "LocalizedDomain.Editor.LocaleSearch";
	private const string PrefsSelectedKey = "LocalizedDomain.Editor.SelectedKey";
	private const string PrefsAutoSave = "LocalizedDomain.Editor.AutoSave";
	private static readonly string[] SystemLanguageOptions = BuildSystemLanguageOptions();

	[SerializeField]
	private LocalizationEditorSettings _settings;

	[SerializeField]
	private bool _showLocales = true;

	[SerializeField]
	private bool _showDiagnostics = true;

	[SerializeField]
	private bool _deleteLocaleValues = true;

	[SerializeField]
	private string _keySearch;

	[SerializeField]
	private string _localeSearch;

	[SerializeField]
	private string _newKey;

	[SerializeField]
	private string _newLocaleCode;

	[SerializeField]
	private string _newLocaleName;

	[SerializeField]
	private string _newLocaleSystemLanguage;

	[SerializeField]
	private int _diagnosticLocaleIndex;

	private SerializedObject _settingsSerialized;
	private LocalizationProject _project;
	private LocalizationEntry _selectedEntry;
	private string _statusMessage;
	private string _localeError;
	private string _loadedJsonPath;
	private bool _hasUnsavedChanges;
	private bool _autoSave;
	private Vector2 _keyListScroll;
	private Vector2 _detailScroll;
	private Vector2 _localeListScroll;
	private Vector2 _diagnosticScroll;

	[MenuItem("Tools/LocalizedDomain/Localization Editor")]
	public static void Open()
	{
		GetWindow<LocalizationEditorWindow>("Localization");
	}

	private void OnEnable()
	{
		LoadPrefs();
		TryRestoreSettings();
		if (_settings != null)
		{
			LoadFromJson(false);
		}
	}

	private void OnDisable()
	{
		SavePrefs();
	}

	private void OnGUI()
	{
		DrawSettingsSection();

		if (_settings == null)
		{
			return;
		}

		HandleJsonPathChange();
		EnsureProjectExists();

		DrawActionToolbar();
		DrawLocalesPanel();

		EditorGUILayout.Space();
		EditorGUILayout.BeginHorizontal();
		DrawKeyList();
		DrawEntryDetails();
		EditorGUILayout.EndHorizontal();

		DrawDiagnostics();

		if (!string.IsNullOrWhiteSpace(_statusMessage))
		{
			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
		}
	}

	private void DrawSettingsSection()
	{
		_settings = (LocalizationEditorSettings)EditorGUILayout.ObjectField(
			"Settings", _settings, typeof(LocalizationEditorSettings), false);

		if (_settings == null)
		{
			EditorGUILayout.HelpBox(
				"Assign a LocalizationEditorSettings asset to continue.",
				MessageType.Info);
			return;
		}

		if (_settingsSerialized == null || _settingsSerialized.targetObject != _settings)
		{
			_settingsSerialized = new SerializedObject(_settings);
		}

		_settingsSerialized.Update();
		EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("_source"));
		EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("_jsonPath"));
		EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("_defaultLocale"));
		EditorGUILayout.PropertyField(_settingsSerialized.FindProperty("_fallbackLocales"), true);
		_settingsSerialized.ApplyModifiedProperties();

		RememberSettingsAsset();
	}

	private void DrawActionToolbar()
	{
		var jsonPath = _settings.JsonPath;
		var fullPath = ResolveJsonPath(jsonPath);
		var exists = !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath);

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("JSON", EditorStyles.boldLabel);
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(jsonPath) ? "<empty>" : jsonPath);
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button(exists ? "Reload JSON" : "Create New", GUILayout.Width(120)))
		{
			LoadFromJson(true);
		}

		using (new EditorGUI.DisabledScope(!_hasUnsavedChanges))
		{
			if (GUILayout.Button("Save JSON", GUILayout.Width(100)))
			{
				SaveToJson();
			}
		}

		_autoSave = EditorGUILayout.ToggleLeft("Auto-save", _autoSave, GUILayout.Width(100));
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();
		EditorGUILayout.BeginHorizontal();
		using (new EditorGUI.DisabledScope(_settings.Source == null))
		{
			if (GUILayout.Button("Import From Source", GUILayout.Width(160)))
			{
				ImportFromSource();
			}
		}

		if (GUILayout.Button("Validate Project", GUILayout.Width(140)))
		{
			ValidateProject();
		}

		EditorGUILayout.EndHorizontal();

		if (_hasUnsavedChanges)
		{
			EditorGUILayout.HelpBox("Unsaved changes.", MessageType.Warning);
		}
	}

	private void DrawLocalesPanel()
	{
		EditorGUILayout.Space();
		_showLocales = EditorGUILayout.Foldout(_showLocales, "Locales", true);
		if (!_showLocales)
		{
			return;
		}

		EditorGUILayout.BeginVertical(EditorStyles.helpBox);

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Code", EditorStyles.miniLabel, GUILayout.Width(120));
		EditorGUILayout.LabelField("Display Name", EditorStyles.miniLabel);
		EditorGUILayout.LabelField("System Language", EditorStyles.miniLabel, GUILayout.Width(120));
		EditorGUILayout.LabelField("Remove", EditorStyles.miniLabel, GUILayout.Width(70));
		EditorGUILayout.EndHorizontal();
		GUILayout.Space(2f);

		_localeListScroll = EditorGUILayout.BeginScrollView(_localeListScroll, GUILayout.Height(160));
		var locales = _project.Locales;
		for (var i = 0; i < locales.Count; i++)
		{
			var locale = locales[i];
			if (locale == null)
			{
				continue;
			}

			EditorGUILayout.BeginHorizontal();
			var oldCode = locale.Code;
			var newCode = EditorGUILayout.TextField(oldCode ?? string.Empty, GUILayout.Width(120));
			var newName = EditorGUILayout.TextField(locale.DisplayName ?? string.Empty);
			var newSystemLanguage = DrawSystemLanguagePopup(locale.SystemLanguage, GUILayout.Width(120));

			if (GUILayout.Button("Remove", GUILayout.Width(70)))
			{
				if (EditorUtility.DisplayDialog("Remove locale",
						$"Remove locale '{oldCode}'?", "Remove", "Cancel"))
				{
					RemoveLocale(oldCode);
					_statusMessage = $"Locale '{oldCode}' removed.";
					_selectedEntry = null;
					break;
				}
			}

			EditorGUILayout.EndHorizontal();

			if (!string.Equals(oldCode, newCode, StringComparison.Ordinal))
			{
				if (!TryRenameLocale(oldCode, newCode, out var error))
				{
					_localeError = error;
				}
				else
				{
					_localeError = null;
					MarkDirty();
				}
			}

			if (!string.Equals(locale.DisplayName, newName, StringComparison.Ordinal))
			{
				locale.DisplayName = newName;
				MarkDirty();
			}

			if (!string.Equals(locale.SystemLanguage, newSystemLanguage, StringComparison.Ordinal))
			{
				locale.SystemLanguage = newSystemLanguage;
				MarkDirty();
			}
		}

		EditorGUILayout.EndScrollView();

		if (!string.IsNullOrWhiteSpace(_localeError))
		{
			EditorGUILayout.HelpBox(_localeError, MessageType.Warning);
		}

		EditorGUILayout.Space();
		EditorGUILayout.BeginHorizontal();
		_newLocaleCode = EditorGUILayout.TextField(_newLocaleCode ?? string.Empty, GUILayout.Width(120));
		_newLocaleName = EditorGUILayout.TextField(_newLocaleName ?? string.Empty);
		_newLocaleSystemLanguage = DrawSystemLanguagePopup(_newLocaleSystemLanguage, GUILayout.Width(120));
		if (GUILayout.Button("Add Locale", GUILayout.Width(100)))
		{
			AddLocale(_newLocaleCode, _newLocaleName, _newLocaleSystemLanguage);
		}

		EditorGUILayout.EndHorizontal();

		_deleteLocaleValues = EditorGUILayout.ToggleLeft(
			"Delete values when removing locale", _deleteLocaleValues);

		EditorGUILayout.EndVertical();
	}

	private void DrawKeyList()
	{
		EditorGUILayout.BeginVertical(GUILayout.Width(260));
		EditorGUILayout.LabelField("Keys", EditorStyles.boldLabel);

		EditorGUI.BeginChangeCheck();
		_keySearch = EditorGUILayout.TextField("Search", _keySearch);
		if (EditorGUI.EndChangeCheck())
		{
			EditorPrefs.SetString(PrefsKeySearch, _keySearch ?? string.Empty);
		}

		var filtered = GetFilteredEntries(_keySearch);
		if (_selectedEntry != null && !filtered.Contains(_selectedEntry))
		{
			_selectedEntry = null;
		}

		_keyListScroll = EditorGUILayout.BeginScrollView(_keyListScroll);
		foreach (var entry in filtered)
		{
			var label = string.IsNullOrWhiteSpace(entry.Key) ? "<empty>" : entry.Key;
			var isSelected = entry == _selectedEntry;
			var previousColor = GUI.backgroundColor;
			if (isSelected)
			{
				GUI.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 0.8f);
			}

			if (GUILayout.Button(label, EditorStyles.miniButton))
			{
				_selectedEntry = entry;
			}

			GUI.backgroundColor = previousColor;
		}

		EditorGUILayout.EndScrollView();

		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Add Key");
		EditorGUILayout.BeginHorizontal();
		_newKey = EditorGUILayout.TextField(_newKey ?? string.Empty);
		if (GUILayout.Button("Add", GUILayout.Width(60)))
		{
			AddKey(_newKey);
		}

		EditorGUILayout.EndHorizontal();

		EditorGUILayout.EndVertical();
	}

	private void DrawEntryDetails()
	{
		EditorGUILayout.BeginVertical();
		EditorGUILayout.LabelField("Entry Details", EditorStyles.boldLabel);

		if (_selectedEntry == null)
		{
			EditorGUILayout.HelpBox("Select a key from the list to edit.",
				MessageType.Info);
			EditorGUILayout.EndVertical();
			return;
		}

		EnsureEntryHasAllLocales(_selectedEntry);

		EditorGUI.BeginChangeCheck();
		var newKey = EditorGUILayout.TextField("Key", _selectedEntry.Key);
		var newComment = EditorGUILayout.TextField("Comment", _selectedEntry.Comment);
		if (EditorGUI.EndChangeCheck())
		{
			_selectedEntry.Key = newKey;
			_selectedEntry.Comment = newComment;
			MarkDirty();
		}

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Duplicate", GUILayout.Width(100)))
		{
			_selectedEntry = DuplicateEntry(_selectedEntry);
			MarkDirty();
		}

		if (GUILayout.Button("Remove", GUILayout.Width(100)))
		{
			if (EditorUtility.DisplayDialog("Remove entry",
					$"Remove key '{_selectedEntry.Key}'?", "Remove", "Cancel"))
			{
				_project.Entries.Remove(_selectedEntry);
				_selectedEntry = null;
				MarkDirty();
				EditorGUILayout.EndVertical();
				return;
			}
		}

		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();
		EditorGUI.BeginChangeCheck();
		var localeFilterLabel = new GUIContent(
			"Locale Filter",
			"Filters visible locale fields by code or display name. Leave empty to show all.");
		_localeSearch = EditorGUILayout.TextField(localeFilterLabel, _localeSearch);
		if (EditorGUI.EndChangeCheck())
		{
			EditorPrefs.SetString(PrefsLocaleSearch, _localeSearch ?? string.Empty);
		}

		_detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
		if (_project.Locales.Count == 0)
		{
			EditorGUILayout.HelpBox("No locales configured.", MessageType.Info);
		}
		else
		{
			foreach (var locale in _project.Locales)
			{
				if (locale == null || !LocaleMatchesFilter(locale, _localeSearch))
				{
					continue;
				}

				var title = string.IsNullOrWhiteSpace(locale.DisplayName)
					? locale.Code
					: $"{locale.Code} ({locale.DisplayName})";
				EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

				var current = GetValueText(_selectedEntry, locale.Code);
				EditorGUI.BeginChangeCheck();
				var updated = EditorGUILayout.TextArea(current, GUILayout.MinHeight(60));
				if (EditorGUI.EndChangeCheck())
				{
					SetValueText(_selectedEntry, locale.Code, updated);
					MarkDirty();
				}

				EditorGUILayout.Space();
			}
		}

		EditorGUILayout.EndScrollView();
		EditorGUILayout.EndVertical();
	}

	private void DrawDiagnostics()
	{
		EditorGUILayout.Space();
		_showDiagnostics = EditorGUILayout.Foldout(_showDiagnostics, "Diagnostics", true);
		if (!_showDiagnostics || _project == null)
		{
			return;
		}

		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		if (_project.Locales.Count == 0)
		{
			EditorGUILayout.HelpBox("No locales configured.", MessageType.Info);
			EditorGUILayout.EndVertical();
			return;
		}

		EditorGUILayout.LabelField("Coverage", EditorStyles.boldLabel);
		foreach (var locale in _project.Locales)
		{
			if (locale == null || string.IsNullOrWhiteSpace(locale.Code))
			{
				continue;
			}

			var missing = CountMissing(locale.Code);
			EditorGUILayout.LabelField($"{locale.Code}: {missing} missing");
		}

		EditorGUILayout.Space();
		var localeLabels = BuildLocaleLabels();
		if (localeLabels.Count > 0)
		{
			_diagnosticLocaleIndex = Mathf.Clamp(_diagnosticLocaleIndex, 0, _project.Locales.Count - 1);
			_diagnosticLocaleIndex = EditorGUILayout.Popup(
				"Missing keys", _diagnosticLocaleIndex, localeLabels.ToArray());

			var selectedLocale = _project.Locales[_diagnosticLocaleIndex];
			if (selectedLocale != null && GUILayout.Button("Fill missing from default locale"))
			{
				FillMissingFromDefault(selectedLocale.Code);
			}

			_diagnosticScroll = EditorGUILayout.BeginScrollView(_diagnosticScroll, GUILayout.Height(140));
			foreach (var entry in _project.Entries)
			{
				if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
				{
					continue;
				}

				if (IsMissing(entry, selectedLocale?.Code))
				{
					EditorGUILayout.LabelField(entry.Key);
				}
			}

			EditorGUILayout.EndScrollView();
		}

		EditorGUILayout.EndVertical();
	}

	private void AddKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return;
		}

		var trimmed = key.Trim();
		if (ContainsKey(trimmed))
		{
			_statusMessage = $"Key '{trimmed}' already exists.";
			return;
		}

		var entry = new LocalizationEntry(trimmed);
		_project.Entries.Add(entry);
		EnsureEntryHasAllLocales(entry);
		_selectedEntry = entry;
		_newKey = string.Empty;
		MarkDirty();
	}

	private void AddLocale(string code, string name, string systemLanguage)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return;
		}

		var trimmed = code.Trim();
		var displayName = name?.Trim();
		var systemLanguageValue = systemLanguage?.Trim();
		var existing = FindLocale(trimmed);
		if (existing != null)
		{
			existing.DisplayName = displayName ?? existing.DisplayName;
			existing.SystemLanguage = systemLanguageValue ?? existing.SystemLanguage;
			MarkDirty();
			return;
		}

		_project.Locales.Add(new LocaleInfo(trimmed, displayName, systemLanguageValue));
		EnsureAllEntriesHaveLocales();
		_newLocaleCode = string.Empty;
		_newLocaleName = string.Empty;
		_newLocaleSystemLanguage = string.Empty;
		MarkDirty();
	}

	private void RemoveLocale(string code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return;
		}

		_project.Locales.RemoveAll(locale =>
			locale != null && string.Equals(locale.Code, code, StringComparison.OrdinalIgnoreCase));

		if (_deleteLocaleValues)
		{
			foreach (var entry in _project.Entries)
			{
				entry?.Values?.Remove(code);
			}
		}

		MarkDirty();
	}

	private bool TryRenameLocale(string oldCode, string newCode, out string error)
	{
		error = null;
		if (string.IsNullOrWhiteSpace(oldCode) || string.IsNullOrWhiteSpace(newCode))
		{
			error = "Locale code cannot be empty.";
			return false;
		}

		var trimmedNew = newCode.Trim();
		if (!string.Equals(oldCode, trimmedNew, StringComparison.OrdinalIgnoreCase))
		{
			if (FindLocale(trimmedNew) != null)
			{
				error = $"Locale '{trimmedNew}' already exists.";
				return false;
			}
		}

		foreach (var locale in _project.Locales)
		{
			if (locale != null &&
				string.Equals(locale.Code, oldCode, StringComparison.OrdinalIgnoreCase))
			{
				locale.Code = trimmedNew;
			}
		}

		foreach (var entry in _project.Entries)
		{
			if (entry?.Values == null)
			{
				continue;
			}

			if (entry.Values.TryGetValue(oldCode, out var text))
			{
				if (!entry.Values.ContainsKey(trimmedNew))
				{
					entry.Values[trimmedNew] = text;
				}

				entry.Values.Remove(oldCode);
			}
		}

		return true;
	}

	private LocalizationEntry DuplicateEntry(LocalizationEntry source)
	{
		var copy = new LocalizationEntry(CreateUniqueKey(source?.Key))
		{
			Comment = source?.Comment
		};

		if (source?.Values != null)
		{
			foreach (var pair in source.Values)
			{
				copy.Values[pair.Key] = pair.Value;
			}
		}

		if (source?.Metadata != null)
		{
			foreach (var pair in source.Metadata)
			{
				copy.Metadata[pair.Key] = pair.Value;
			}
		}

		_project.Entries.Add(copy);
		EnsureEntryHasAllLocales(copy);
		return copy;
	}

	private void ImportFromSource()
	{
		if (_settings.Source == null)
		{
			_statusMessage = "Source is not set.";
			return;
		}

		try
		{
			_project = _settings.Source.Load();
			EnsureAllEntriesHaveLocales();
			_selectedEntry = null;
			_statusMessage = "Import completed.";
			MarkDirty();
		}
		catch (Exception ex)
		{
			_statusMessage = $"Import failed: {ex.Message}";
			Debug.LogException(ex);
		}
	}

	private void ValidateProject()
	{
		var issues = new List<string>();
		var keys = new HashSet<string>(StringComparer.Ordinal);
		var localeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var locale in _project.Locales)
		{
			if (string.IsNullOrWhiteSpace(locale?.Code))
			{
				issues.Add("Locale with empty code.");
				continue;
			}

			if (!localeCodes.Add(locale.Code))
			{
				issues.Add($"Duplicate locale code: {locale.Code}");
			}
		}

		foreach (var entry in _project.Entries)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
			{
				issues.Add("Entry with empty key.");
				continue;
			}

			if (!keys.Add(entry.Key))
			{
				issues.Add($"Duplicate key: {entry.Key}");
			}
		}

		_statusMessage = issues.Count == 0
			? "Validation passed."
			: string.Join("\n", issues);
	}

	private void LoadFromJson(bool force)
	{
		if (_settings == null)
		{
			return;
		}

		if (_hasUnsavedChanges && !force)
		{
			_statusMessage = "Unsaved changes. Reload to discard.";
			return;
		}

		var path = _settings.JsonPath;
		_loadedJsonPath = path;
		_project = new LocalizationProject();

		if (string.IsNullOrWhiteSpace(path))
		{
			_statusMessage = "JSON path is empty.";
			_hasUnsavedChanges = false;
			return;
		}

		var fullPath = ResolveJsonPath(path);
		if (!File.Exists(fullPath))
		{
			_statusMessage = "JSON not found. New project created.";
			_hasUnsavedChanges = false;
			return;
		}

		try
		{
			var json = File.ReadAllText(fullPath);
			var serializer = new LocalizationJsonSerializer();
			_project = serializer.Deserialize(json) ?? new LocalizationProject();
			EnsureAllEntriesHaveLocales();
			_hasUnsavedChanges = false;
			_statusMessage = $"Loaded from {path}.";
			RestoreSelection();
		}
		catch (Exception ex)
		{
			_statusMessage = $"Failed to load JSON: {ex.Message}";
		}
	}

	private void SaveToJson()
	{
		if (_settings == null)
		{
			return;
		}

		var path = _settings.JsonPath;
		if (string.IsNullOrWhiteSpace(path))
		{
			_statusMessage = "JSON path is empty.";
			return;
		}

		var fullPath = ResolveJsonPath(path);
		var directory = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		try
		{
			EnsureAllEntriesHaveLocales();
			var serializer = new LocalizationJsonSerializer();
			var json = serializer.Serialize(_project);
			File.WriteAllText(fullPath, json, new UTF8Encoding(false));
			AssetDatabase.Refresh();
			_hasUnsavedChanges = false;
			_statusMessage = $"Saved to {path}.";
		}
		catch (Exception ex)
		{
			_statusMessage = $"Failed to save JSON: {ex.Message}";
			Debug.LogException(ex);
		}
	}

	private void HandleJsonPathChange()
	{
		if (_settings == null)
		{
			return;
		}

		if (string.Equals(_loadedJsonPath, _settings.JsonPath, StringComparison.Ordinal))
		{
			return;
		}

		if (!_hasUnsavedChanges)
		{
			LoadFromJson(false);
		}
	}

	private void EnsureProjectExists()
	{
		if (_project == null)
		{
			_project = new LocalizationProject();
		}
	}

	private void MarkDirty()
	{
		_hasUnsavedChanges = true;
		if (_autoSave)
		{
			SaveToJson();
		}
	}

	private List<LocalizationEntry> GetFilteredEntries(string query)
	{
		var list = new List<LocalizationEntry>();
		foreach (var entry in _project.Entries)
		{
			if (entry == null)
			{
				continue;
			}

			if (MatchesSearch(entry.Key, query))
			{
				list.Add(entry);
			}
		}

		return list;
	}

	private static bool MatchesSearch(string value, string query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return true;
		}

		return !string.IsNullOrWhiteSpace(value) &&
				value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool LocaleMatchesFilter(LocaleInfo locale, string filter)
	{
		if (string.IsNullOrWhiteSpace(filter))
		{
			return true;
		}

		if (!string.IsNullOrWhiteSpace(locale?.Code) &&
			locale.Code.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}

		if (!string.IsNullOrWhiteSpace(locale?.DisplayName) &&
			locale.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return true;
		}

		return false;
	}

	private static string ResolveJsonPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}

		if (Path.IsPathRooted(path))
		{
			return path;
		}

		var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		return Path.Combine(projectRoot, path);
	}

	private bool ContainsKey(string key)
	{
		foreach (var entry in _project.Entries)
		{
			if (entry != null && string.Equals(entry.Key, key, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private string CreateUniqueKey(string baseKey)
	{
		var seed = string.IsNullOrWhiteSpace(baseKey) ? "new.key" : baseKey;
		var key = seed;
		var index = 1;

		while (ContainsKey(key))
		{
			key = $"{seed}_copy{index}";
			index++;
		}

		return key;
	}

	private LocaleInfo FindLocale(string code)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return null;
		}

		foreach (var locale in _project.Locales)
		{
			if (locale != null &&
				string.Equals(locale.Code, code, StringComparison.OrdinalIgnoreCase))
			{
				return locale;
			}
		}

		return null;
	}

	private void EnsureAllEntriesHaveLocales()
	{
		foreach (var entry in _project.Entries)
		{
			EnsureEntryHasAllLocales(entry);
		}
	}

	private int CountMissing(string localeCode)
	{
		var count = 0;
		foreach (var entry in _project.Entries)
		{
			if (entry == null)
			{
				continue;
			}

			if (IsMissing(entry, localeCode))
			{
				count++;
			}
		}

		return count;
	}

	private static bool IsMissing(LocalizationEntry entry, string localeCode)
	{
		if (entry == null || string.IsNullOrWhiteSpace(localeCode))
		{
			return false;
		}

		if (entry.Values == null)
		{
			return true;
		}

		return !entry.Values.TryGetValue(localeCode, out var text) ||
				string.IsNullOrWhiteSpace(text);
	}

	private List<string> BuildLocaleLabels()
	{
		var labels = new List<string>();
		foreach (var locale in _project.Locales)
		{
			if (locale == null)
			{
				labels.Add("<null>");
				continue;
			}

			if (string.IsNullOrWhiteSpace(locale.Code))
			{
				labels.Add("<empty>");
				continue;
			}

			labels.Add(string.IsNullOrWhiteSpace(locale.DisplayName)
				? locale.Code
				: $"{locale.Code} ({locale.DisplayName})");
		}

		return labels;
	}

	private void FillMissingFromDefault(string targetLocale)
	{
		if (string.IsNullOrWhiteSpace(targetLocale))
		{
			return;
		}

		var defaultLocale = _settings?.DefaultLocale;
		if (string.IsNullOrWhiteSpace(defaultLocale))
		{
			_statusMessage = "Default locale is not set.";
			return;
		}

		if (string.Equals(defaultLocale, targetLocale, StringComparison.OrdinalIgnoreCase))
		{
			_statusMessage = "Target locale equals default locale.";
			return;
		}

		var filled = 0;
		foreach (var entry in _project.Entries)
		{
			if (entry == null)
			{
				continue;
			}

			if (!IsMissing(entry, targetLocale))
			{
				continue;
			}

			if (entry.Values != null &&
				entry.Values.TryGetValue(defaultLocale, out var sourceText) &&
				!string.IsNullOrWhiteSpace(sourceText))
			{
				entry.Values[targetLocale] = sourceText;
				filled++;
			}
		}

		if (filled > 0)
		{
			MarkDirty();
			_statusMessage = $"Filled {filled} entries from {defaultLocale}.";
		}
		else
		{
			_statusMessage = "No missing entries to fill.";
		}
	}

	private void EnsureEntryHasAllLocales(LocalizationEntry entry)
	{
		if (entry == null)
		{
			return;
		}

		foreach (var locale in _project.Locales)
		{
			if (string.IsNullOrWhiteSpace(locale?.Code))
			{
				continue;
			}

			if (!entry.Values.ContainsKey(locale.Code))
			{
				entry.Values[locale.Code] = string.Empty;
			}
		}
	}

	private static string GetValueText(LocalizationEntry entry, string locale)
	{
		if (entry?.Values == null || string.IsNullOrWhiteSpace(locale))
		{
			return string.Empty;
		}

		return entry.Values.TryGetValue(locale, out var text)
			? text
			: string.Empty;
	}

	private static void SetValueText(LocalizationEntry entry, string locale, string text)
	{
		if (entry == null || string.IsNullOrWhiteSpace(locale))
		{
			return;
		}

		entry.Values[locale] = text ?? string.Empty;
	}

	private static string[] BuildSystemLanguageOptions()
	{
		var names = Enum.GetNames(typeof(SystemLanguage));
		var options = new string[names.Length + 1];
		options[0] = "<none>";
		for (var i = 0; i < names.Length; i++)
		{
			options[i + 1] = names[i];
		}

		return options;
	}

	private static int GetSystemLanguageIndex(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return 0;
		}

		for (var i = 1; i < SystemLanguageOptions.Length; i++)
		{
			if (string.Equals(SystemLanguageOptions[i], value, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1;
	}

	private static string DrawSystemLanguagePopup(string current, params GUILayoutOption[] options)
	{
		var index = GetSystemLanguageIndex(current);
		if (index < 0)
		{
			var customLabel = $"Custom: {current}";
			var customOptions = new string[SystemLanguageOptions.Length + 1];
			Array.Copy(SystemLanguageOptions, customOptions, SystemLanguageOptions.Length);
			customOptions[customOptions.Length - 1] = customLabel;

			var selected = EditorGUILayout.Popup(customOptions.Length - 1, customOptions, options);
			if (selected == customOptions.Length - 1)
			{
				return current ?? string.Empty;
			}

			return selected == 0 ? string.Empty : customOptions[selected];
		}

		var updated = EditorGUILayout.Popup(index, SystemLanguageOptions, options);
		return updated == 0 ? string.Empty : SystemLanguageOptions[updated];
	}

	private void RestoreSelection()
	{
		var preferred = EditorPrefs.GetString(PrefsSelectedKey, string.Empty);
		if (!string.IsNullOrWhiteSpace(preferred))
		{
			foreach (var entry in _project.Entries)
			{
				if (entry != null && string.Equals(entry.Key, preferred, StringComparison.Ordinal))
				{
					_selectedEntry = entry;
					return;
				}
			}
		}

		_selectedEntry = _project.Entries.Count > 0 ? _project.Entries[0] : null;
	}

	private void LoadPrefs()
	{
		_autoSave = EditorPrefs.GetBool(PrefsAutoSave, false);
		_keySearch = EditorPrefs.GetString(PrefsKeySearch, string.Empty);
		_localeSearch = EditorPrefs.GetString(PrefsLocaleSearch, string.Empty);
	}

	private void SavePrefs()
	{
		EditorPrefs.SetBool(PrefsAutoSave, _autoSave);
		EditorPrefs.SetString(PrefsKeySearch, _keySearch ?? string.Empty);
		EditorPrefs.SetString(PrefsLocaleSearch, _localeSearch ?? string.Empty);
		if (_selectedEntry != null && !string.IsNullOrWhiteSpace(_selectedEntry.Key))
		{
			EditorPrefs.SetString(PrefsSelectedKey, _selectedEntry.Key);
		}
	}

	private void TryRestoreSettings()
	{
		var guid = EditorPrefs.GetString(PrefsSettingsGuid, string.Empty);
		if (string.IsNullOrWhiteSpace(guid))
		{
			return;
		}

		var path = AssetDatabase.GUIDToAssetPath(guid);
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		_settings = AssetDatabase.LoadAssetAtPath<LocalizationEditorSettings>(path);
	}

	private void RememberSettingsAsset()
	{
		if (_settings == null)
		{
			return;
		}

		var path = AssetDatabase.GetAssetPath(_settings);
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		var guid = AssetDatabase.AssetPathToGUID(path);
		if (!string.IsNullOrWhiteSpace(guid))
		{
			EditorPrefs.SetString(PrefsSettingsGuid, guid);
		}
	}
}
}