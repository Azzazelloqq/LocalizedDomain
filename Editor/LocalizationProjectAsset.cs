using System;
using System.Collections.Generic;
using LocalizedDomain;
using UnityEngine;

namespace LocalizedDomain.Editor
{
[CreateAssetMenu(menuName = "LocalizedDomain/Localization Project",
	fileName = "LocalizationProject")]
public sealed class LocalizationProjectAsset : ScriptableObject
{
	[SerializeField]
	private List<LocaleInfoData> _locales = new();

	[SerializeField]
	private List<EntryData> _entries = new();

	[SerializeField]
	private List<MetadataEntry> _metadata = new();

	public IReadOnlyList<LocaleInfoData> Locales => _locales;
	public IReadOnlyList<EntryData> Entries => _entries;
	public List<LocaleInfoData> LocalesMutable => _locales;
	public List<EntryData> EntriesMutable => _entries;

	public LocalizationProject ToProject()
	{
		var project = new LocalizationProject();

		foreach (var locale in _locales)
		{
			if (string.IsNullOrWhiteSpace(locale?.Code))
			{
				continue;
			}

			project.Locales.Add(new LocaleInfo(locale.Code, locale.DisplayName, locale.SystemLanguage));
		}

		foreach (var entryData in _entries)
		{
			if (entryData == null || string.IsNullOrWhiteSpace(entryData.Key))
			{
				continue;
			}

			var entry = new LocalizationEntry(entryData.Key)
			{
				Comment = entryData.Comment
			};

			foreach (var value in entryData.Values)
			{
				if (string.IsNullOrWhiteSpace(value?.Locale))
				{
					continue;
				}

				entry.Values[value.Locale] = value.Text ?? string.Empty;
			}

			foreach (var meta in entryData.Metadata)
			{
				if (string.IsNullOrWhiteSpace(meta?.Key))
				{
					continue;
				}

				entry.Metadata[meta.Key] = meta.Value ?? string.Empty;
			}

			project.Entries.Add(entry);
		}

		foreach (var meta in _metadata)
		{
			if (string.IsNullOrWhiteSpace(meta?.Key))
			{
				continue;
			}

			project.Metadata[meta.Key] = meta.Value ?? string.Empty;
		}

		return project;
	}

	public void FromProject(LocalizationProject project)
	{
		_locales.Clear();
		_entries.Clear();
		_metadata.Clear();

		if (project == null)
		{
			return;
		}

		foreach (var locale in project.Locales)
		{
			if (string.IsNullOrWhiteSpace(locale?.Code))
			{
				continue;
			}

			_locales.Add(new LocaleInfoData
			{
				Code = locale.Code,
				DisplayName = locale.DisplayName,
				SystemLanguage = locale.SystemLanguage
			});
		}

		foreach (var entry in project.Entries)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
			{
				continue;
			}

			var entryData = new EntryData
			{
				Key = entry.Key,
				Comment = entry.Comment
			};

			foreach (var value in entry.Values)
			{
				entryData.Values.Add(new LocalizedValueData
				{
					Locale = value.Key,
					Text = value.Value
				});
			}

			foreach (var meta in entry.Metadata)
			{
				entryData.Metadata.Add(new MetadataEntry
				{
					Key = meta.Key,
					Value = meta.Value
				});
			}

			_entries.Add(entryData);
		}

		foreach (var meta in project.Metadata)
		{
			_metadata.Add(new MetadataEntry
			{
				Key = meta.Key,
				Value = meta.Value
			});
		}
	}

	public EntryData CreateEntry(string key)
	{
		var entry = new EntryData
		{
			Key = key ?? string.Empty
		};

		EnsureEntryHasAllLocales(entry);
		_entries.Add(entry);
		return entry;
	}

	public void RemoveEntry(EntryData entry)
	{
		if (entry == null)
		{
			return;
		}

		_entries.Remove(entry);
	}

	public bool EnsureEntryHasAllLocales(EntryData entry)
	{
		if (entry == null)
		{
			return false;
		}

		var changed = false;
		foreach (var locale in _locales)
		{
			if (string.IsNullOrWhiteSpace(locale?.Code))
			{
				continue;
			}

			if (!TryGetValue(entry, locale.Code, out _))
			{
				entry.Values.Add(new LocalizedValueData
				{
					Locale = locale.Code,
					Text = string.Empty
				});
				changed = true;
			}
		}

		return changed;
	}

	public void SyncEntriesWithLocales()
	{
		foreach (var entry in _entries)
		{
			EnsureEntryHasAllLocales(entry);
		}
	}

	public void RemoveLocale(string localeCode, bool deleteValues)
	{
		if (string.IsNullOrWhiteSpace(localeCode))
		{
			return;
		}

		_locales.RemoveAll(locale =>
			locale != null && string.Equals(locale.Code, localeCode, StringComparison.OrdinalIgnoreCase));

		if (!deleteValues)
		{
			return;
		}

		foreach (var entry in _entries)
		{
			if (entry?.Values == null)
			{
				continue;
			}

			entry.Values.RemoveAll(value =>
				value != null && string.Equals(value.Locale, localeCode, StringComparison.OrdinalIgnoreCase));
		}
	}

	public void AddOrUpdateLocale(string code, string displayName)
	{
		if (string.IsNullOrWhiteSpace(code))
		{
			return;
		}

		var existing = _locales.Find(locale =>
			locale != null && string.Equals(locale.Code, code, StringComparison.OrdinalIgnoreCase));

		if (existing != null)
		{
			existing.DisplayName = displayName ?? existing.DisplayName;
		}
		else
		{
			_locales.Add(new LocaleInfoData
			{
				Code = code,
				DisplayName = displayName
			});
		}

		SyncEntriesWithLocales();
	}

	public bool TryRenameLocale(string oldCode, string newCode, out string error)
	{
		error = null;

		if (string.IsNullOrWhiteSpace(oldCode) || string.IsNullOrWhiteSpace(newCode))
		{
			error = "Locale code cannot be empty.";
			return false;
		}

		if (!string.Equals(oldCode, newCode, StringComparison.OrdinalIgnoreCase))
		{
			var exists = _locales.Exists(locale =>
				locale != null &&
				string.Equals(locale.Code, newCode, StringComparison.OrdinalIgnoreCase));
			if (exists)
			{
				error = $"Locale '{newCode}' already exists.";
				return false;
			}
		}

		foreach (var locale in _locales)
		{
			if (locale != null &&
				string.Equals(locale.Code, oldCode, StringComparison.OrdinalIgnoreCase))
			{
				locale.Code = newCode;
			}
		}

		foreach (var entry in _entries)
		{
			if (entry?.Values == null)
			{
				continue;
			}

			LocalizedValueData target = null;
			foreach (var value in entry.Values)
			{
				if (value != null &&
					string.Equals(value.Locale, oldCode, StringComparison.OrdinalIgnoreCase))
				{
					target = value;
					break;
				}
			}

			if (target == null)
			{
				continue;
			}

			if (TryGetValue(entry, newCode, out _))
			{
				entry.Values.Remove(target);
			}
			else
			{
				target.Locale = newCode;
			}
		}

		return true;
	}

	public static bool TryGetValue(EntryData entry, string locale, out LocalizedValueData value)
	{
		value = null;
		if (entry?.Values == null || string.IsNullOrWhiteSpace(locale))
		{
			return false;
		}

		foreach (var candidate in entry.Values)
		{
			if (candidate != null &&
				string.Equals(candidate.Locale, locale, StringComparison.OrdinalIgnoreCase))
			{
				value = candidate;
				return true;
			}
		}

		return false;
	}

	public static string GetValueText(EntryData entry, string locale)
	{
		return TryGetValue(entry, locale, out var value)
			? value.Text
			: string.Empty;
	}

	public static void SetValueText(EntryData entry, string locale, string text)
	{
		if (entry == null || string.IsNullOrWhiteSpace(locale))
		{
			return;
		}

		if (TryGetValue(entry, locale, out var value))
		{
			value.Text = text ?? string.Empty;
			return;
		}

		entry.Values.Add(new LocalizedValueData
		{
			Locale = locale,
			Text = text ?? string.Empty
		});
	}
}

[Serializable]
public sealed class LocaleInfoData
{
	public string Code;
	public string DisplayName;
	public string SystemLanguage;
}

[Serializable]
public sealed class EntryData
{
	public string Key;
	public string Comment;
	public List<LocalizedValueData> Values = new();
	public List<MetadataEntry> Metadata = new();
}

[Serializable]
public sealed class LocalizedValueData
{
	public string Locale;
	public string Text;
}

[Serializable]
public sealed class MetadataEntry
{
	public string Key;
	public string Value;
}
}