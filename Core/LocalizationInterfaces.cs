using System.Collections.Generic;

namespace LocalizedDomain
{
public interface ILocalizationStore
{
	IReadOnlyList<LocaleInfo> Locales { get; }
	IReadOnlyCollection<string> Keys { get; }
	bool TryGet(string locale, string key, out string text);
	void Load(LocalizationProject project);
	void Clear();
}

public interface ILocalizationResolver
{
	bool TryResolve(
		ILocalizationStore store,
		string key,
		string locale,
		IReadOnlyList<string> fallbackLocales,
		out string text);
}

public interface ILocalizationFormatter
{
	string Format(string template, IReadOnlyDictionary<string, string> args, string locale);
}

public interface ILocalizationSerializer
{
	string Format { get; }
	string Serialize(LocalizationProject project);
	LocalizationProject Deserialize(string payload);
}

public interface ILocalizationSource
{
	LocalizationProject Load();
}

public interface ILocalizationProvider
{
	string CurrentLocale { get; }
	IReadOnlyList<string> FallbackLocales { get; }
	bool TryGet(string key, out string text);

	string Get(
		string key,
		string fallback = null,
		IReadOnlyDictionary<string, string> args = null);
}
}