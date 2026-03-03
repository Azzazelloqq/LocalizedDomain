using System;
using System.Collections.Generic;

namespace LocalizedDomain
{
public sealed class LocalizationService : ILocalizationProvider
{
	private readonly ILocalizationStore _store;
	private readonly ILocalizationResolver _resolver;
	private readonly ILocalizationFormatter _formatter;
	private readonly List<string> _fallbackLocales = new();

	public LocalizationService()
		: this(new InMemoryLocalizationStore(),
			new LocalizationResolver(),
			new DefaultLocalizationFormatter())
	{
	}

	public LocalizationService(
		ILocalizationStore store,
		ILocalizationResolver resolver,
		ILocalizationFormatter formatter)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
		_formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
	}

	public event Action<string> LocaleChanged;
	public event Action DataReloaded;

	public string CurrentLocale { get; private set; }
	public IReadOnlyList<string> FallbackLocales => _fallbackLocales;
	public ILocalizationStore Store => _store;

	public void Load(LocalizationProject project)
	{
		_store.Load(project);
		DataReloaded?.Invoke();
	}

	public void Clear()
	{
		_store.Clear();
		DataReloaded?.Invoke();
	}

	public void SetLocale(string locale)
	{
		if (string.Equals(CurrentLocale, locale, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		CurrentLocale = locale;
		LocaleChanged?.Invoke(locale);
	}

	public void SetFallbackLocales(IEnumerable<string> locales)
	{
		_fallbackLocales.Clear();

		if (locales == null)
		{
			return;
		}

		foreach (var locale in locales)
		{
			if (!string.IsNullOrWhiteSpace(locale))
			{
				_fallbackLocales.Add(locale);
			}
		}
	}

	public bool TryGet(string key, out string text)
	{
		return _resolver.TryResolve(_store, key, CurrentLocale,
			_fallbackLocales, out text);
	}

	public string Get(
		string key,
		string fallback = null,
		IReadOnlyDictionary<string, string> args = null)
	{
		if (!TryGet(key, out var text))
		{
			text = fallback ?? string.Empty;
		}

		if (args == null || args.Count == 0)
		{
			return text;
		}

		return _formatter.Format(text, args, CurrentLocale);
	}
}
}