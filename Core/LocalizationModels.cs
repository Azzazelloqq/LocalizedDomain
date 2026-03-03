using System;
using System.Collections.Generic;

namespace LocalizedDomain
{
public sealed class LocaleInfo
{
	public LocaleInfo()
	{
	}

	public LocaleInfo(string code, string displayName = null, string systemLanguage = null)
	{
		Code = code;
		DisplayName = displayName ?? code;
		SystemLanguage = systemLanguage;
	}

	public string Code { get; set; }
	public string DisplayName { get; set; }
	public string SystemLanguage { get; set; }
}

public sealed class LocalizationEntry
{
	public LocalizationEntry()
	{
	}

	public LocalizationEntry(string key)
	{
		Key = key;
	}

	public string Key { get; set; }
	public string Comment { get; set; }

	public Dictionary<string, string> Values { get; } =
		new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, string> Metadata { get; } =
		new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LocalizationProject
{
	public const int CurrentVersion = 1;

	public int Version { get; set; } = CurrentVersion;
	public List<LocaleInfo> Locales { get; } = new();
	public List<LocalizationEntry> Entries { get; } = new();

	public Dictionary<string, string> Metadata { get; } =
		new(StringComparer.OrdinalIgnoreCase);

	public LocalizationEntry FindEntry(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return null;
		}

		foreach (var entry in Entries)
		{
			if (string.Equals(entry?.Key, key, StringComparison.Ordinal))
			{
				return entry;
			}
		}

		return null;
	}
}
}