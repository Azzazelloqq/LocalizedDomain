using System;
using System.Collections.Generic;
using System.Text;

namespace LocalizedDomain
{
public sealed class InMemoryLocalizationStore : ILocalizationStore
{
	private readonly List<LocaleInfo> _locales = new();

	private readonly Dictionary<string, Dictionary<string, string>> _byLocale =
		new(StringComparer.OrdinalIgnoreCase);

	private readonly HashSet<string> _keys =
		new(StringComparer.Ordinal);

	public IReadOnlyList<LocaleInfo> Locales => _locales;
	public IReadOnlyCollection<string> Keys => _keys;

	public void Load(LocalizationProject project)
	{
		Clear();

		if (project == null)
		{
			return;
		}

		foreach (var locale in project.Locales)
		{
			if (!string.IsNullOrWhiteSpace(locale?.Code))
			{
				_locales.Add(new LocaleInfo(locale.Code, locale.DisplayName));
			}
		}

		foreach (var entry in project.Entries)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
			{
				continue;
			}

			_keys.Add(entry.Key);

			foreach (var pair in entry.Values)
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
				{
					continue;
				}

				if (!_byLocale.TryGetValue(pair.Key, out var map))
				{
					map = new Dictionary<string, string>(StringComparer.Ordinal);
					_byLocale[pair.Key] = map;
				}

				map[entry.Key] = pair.Value ?? string.Empty;
			}
		}
	}

	public void Clear()
	{
		_locales.Clear();
		_byLocale.Clear();
		_keys.Clear();
	}

	public bool TryGet(string locale, string key, out string text)
	{
		text = null;

		if (string.IsNullOrWhiteSpace(locale) || string.IsNullOrWhiteSpace(key))
		{
			return false;
		}

		if (_byLocale.TryGetValue(locale, out var map) &&
			map.TryGetValue(key, out text))
		{
			return true;
		}

		text = null;
		return false;
	}
}

public sealed class LocalizationResolver : ILocalizationResolver
{
	public bool TryResolve(
		ILocalizationStore store,
		string key,
		string locale,
		IReadOnlyList<string> fallbackLocales,
		out string text)
	{
		text = null;

		if (store == null || string.IsNullOrWhiteSpace(key))
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(locale) &&
			store.TryGet(locale, key, out text))
		{
			return true;
		}

		if (fallbackLocales == null)
		{
			return false;
		}

		foreach (var fallback in fallbackLocales)
		{
			if (store.TryGet(fallback, key, out text))
			{
				return true;
			}
		}

		text = null;
		return false;
	}
}

public sealed class DefaultLocalizationFormatter : ILocalizationFormatter
{
	public string Format(string template, IReadOnlyDictionary<string, string> args, string locale)
	{
		if (string.IsNullOrEmpty(template) || args == null || args.Count == 0)
		{
			return template ?? string.Empty;
		}

		return IcuFormatter.Format(template, args, locale);
	}
}

internal static class IcuFormatter
{
	private const string PluralKeyword = "plural";

	public static string Format(
		string template,
		IReadOnlyDictionary<string, string> args,
		string locale)
	{
		if (string.IsNullOrEmpty(template))
		{
			return template ?? string.Empty;
		}

		var builder = new StringBuilder(template.Length + 16);
		for (var i = 0; i < template.Length; i++)
		{
			var c = template[i];
			if (c == '{')
			{
				if (i + 1 < template.Length && template[i + 1] == '{')
				{
					builder.Append('{');
					i++;
					continue;
				}

				if (TryReadExpression(template, ref i, out var expr))
				{
					builder.Append(ResolveExpression(expr, args, locale));
					continue;
				}

				builder.Append('{');
				continue;
			}

			if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
			{
				builder.Append('}');
				i++;
				continue;
			}

			builder.Append(c);
		}

		return builder.ToString();
	}

	private static string ResolveExpression(
		string expression,
		IReadOnlyDictionary<string, string> args,
		string locale)
	{
		if (string.IsNullOrWhiteSpace(expression))
		{
			return "{}";
		}

		if (TryResolvePlural(expression, args, locale, out var result))
		{
			return result;
		}

		if (TryResolveSelect(expression, args, locale, out result))
		{
			return result;
		}

		var key = expression.Trim();
		if (args != null && args.TryGetValue(key, out var value))
		{
			return value ?? string.Empty;
		}

		return "{" + expression + "}";
	}

	private static bool TryResolvePlural(
		string expression,
		IReadOnlyDictionary<string, string> args,
		string locale,
		out string result)
	{
		result = null;
		var parts = SplitTopLevel(expression, ',');
		if (parts.Count < 3)
		{
			return false;
		}

		var variable = parts[0].Trim();
		if (!string.Equals(parts[1].Trim(), PluralKeyword, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var optionsText = string.Join(",", parts.GetRange(2, parts.Count - 2)).Trim();
		if (string.IsNullOrWhiteSpace(variable) || string.IsNullOrWhiteSpace(optionsText))
		{
			return false;
		}

		if (args == null || !args.TryGetValue(variable, out var rawValue))
		{
			return false;
		}

		if (!int.TryParse(rawValue, out var count))
		{
			count = 0;
		}

		var options = ParseOptions(optionsText);
		if (options.Count == 0)
		{
			return false;
		}

		var exactKey = "=" + count;
		if (options.TryGetValue(exactKey, out var exactValue))
		{
			result = ReplaceNumber(Format(exactValue, args, locale), rawValue);
			return true;
		}

		var category = PluralRules.GetCategory(locale, count);
		if (options.TryGetValue(category, out var value))
		{
			result = ReplaceNumber(Format(value, args, locale), rawValue);
			return true;
		}

		if (options.TryGetValue("other", out var other))
		{
			result = ReplaceNumber(Format(other, args, locale), rawValue);
			return true;
		}

		return false;
	}

	private static bool TryResolveSelect(
		string expression,
		IReadOnlyDictionary<string, string> args,
		string locale,
		out string result)
	{
		result = null;
		var parts = SplitTopLevel(expression, ',');
		if (parts.Count < 3)
		{
			return false;
		}

		var variable = parts[0].Trim();
		if (!string.Equals(parts[1].Trim(), "select", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (args == null || !args.TryGetValue(variable, out var selector))
		{
			return false;
		}

		var optionsText = string.Join(",", parts.GetRange(2, parts.Count - 2)).Trim();
		var options = ParseOptions(optionsText);
		if (options.Count == 0)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(selector) &&
			options.TryGetValue(selector, out var value))
		{
			result = Format(value, args, locale);
			return true;
		}

		if (options.TryGetValue("other", out var other))
		{
			result = Format(other, args, locale);
			return true;
		}

		return false;
	}

	private static string ReplaceNumber(string template, string value)
	{
		return string.IsNullOrWhiteSpace(template)
			? template ?? string.Empty
			: template.Replace("#", value ?? string.Empty);
	}

	private static Dictionary<string, string> ParseOptions(string text)
	{
		var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var index = 0;
		while (index < text.Length)
		{
			SkipWhitespace(text, ref index);
			if (index >= text.Length)
			{
				break;
			}

			var selector = ReadUntil(text, ref index, '{');
			SkipWhitespace(text, ref index);
			if (index >= text.Length || text[index] != '{')
			{
				break;
			}

			var value = ReadBlock(text, ref index);
			if (!string.IsNullOrWhiteSpace(selector))
			{
				options[selector.Trim()] = value;
			}
		}

		return options;
	}

	private static List<string> SplitTopLevel(string text, char separator)
	{
		var result = new List<string>();
		var depth = 0;
		var start = 0;

		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			if (c == '{')
			{
				depth++;
			}
			else if (c == '}')
			{
				depth = Math.Max(0, depth - 1);
			}
			else if (c == separator && depth == 0)
			{
				result.Add(text.Substring(start, i - start));
				start = i + 1;
			}
		}

		result.Add(text.Substring(start));
		return result;
	}

	private static bool TryReadExpression(string text, ref int index, out string expression)
	{
		expression = null;
		if (index >= text.Length || text[index] != '{')
		{
			return false;
		}

		var depth = 0;
		var start = index + 1;
		for (var i = index; i < text.Length; i++)
		{
			if (text[i] == '{')
			{
				depth++;
			}
			else if (text[i] == '}')
			{
				depth--;
				if (depth == 0)
				{
					expression = text.Substring(start, i - start);
					index = i;
					return true;
				}
			}
		}

		return false;
	}

	private static string ReadBlock(string text, ref int index)
	{
		if (index >= text.Length || text[index] != '{')
		{
			return string.Empty;
		}

		var depth = 0;
		var start = index + 1;
		for (var i = index; i < text.Length; i++)
		{
			if (text[i] == '{')
			{
				depth++;
			}
			else if (text[i] == '}')
			{
				depth--;
				if (depth == 0)
				{
					var value = text.Substring(start, i - start);
					index = i + 1;
					return value;
				}
			}
		}

		index = text.Length;
		return text.Substring(start);
	}

	private static string ReadUntil(string text, ref int index, char stopChar)
	{
		var start = index;
		while (index < text.Length && text[index] != stopChar)
		{
			index++;
		}

		return text.Substring(start, index - start);
	}

	private static void SkipWhitespace(string text, ref int index)
	{
		while (index < text.Length && char.IsWhiteSpace(text[index]))
		{
			index++;
		}
	}
}

internal static class PluralRules
{
	public static string GetCategory(string locale, int value)
	{
		var lang = NormalizeLocale(locale);
		var n = Math.Abs(value);

		switch (lang)
		{
			case "ru":
			case "uk":
			case "be":
				return SlavicRule(n);
			case "pl":
				return PolishRule(n);
			case "fr":
				return n == 0 || n == 1 ? "one" : "other";
			default:
				return n == 1 ? "one" : "other";
		}
	}

	private static string SlavicRule(int n)
	{
		var mod10 = n % 10;
		var mod100 = n % 100;
		if (mod10 == 1 && mod100 != 11)
		{
			return "one";
		}

		if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
		{
			return "few";
		}

		if (mod10 == 0 || (mod10 >= 5 && mod10 <= 9) || (mod100 >= 11 && mod100 <= 14))
		{
			return "many";
		}

		return "other";
	}

	private static string PolishRule(int n)
	{
		if (n == 1)
		{
			return "one";
		}

		var mod10 = n % 10;
		var mod100 = n % 100;
		if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
		{
			return "few";
		}

		return "many";
	}

	private static string NormalizeLocale(string locale)
	{
		if (string.IsNullOrWhiteSpace(locale))
		{
			return string.Empty;
		}

		var lower = locale.ToLowerInvariant();
		var separatorIndex = lower.IndexOfAny(new[] { '-', '_' });
		if (separatorIndex > 0)
		{
			return lower.Substring(0, separatorIndex);
		}

		return lower;
	}
}
}