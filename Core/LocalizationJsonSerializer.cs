using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LocalizedDomain
{
public sealed class LocalizationJsonSerializer : ILocalizationSerializer
{
	public string Format => "json";

	public string Serialize(LocalizationProject project)
	{
		if (project == null)
		{
			throw new ArgumentNullException(nameof(project));
		}

		var writer = new JsonWriter();
		writer.BeginObject()
			.Name("version").Value(project.Version > 0
				? project.Version
				: LocalizationProject.CurrentVersion)
			.Name("locales").BeginArray();

		foreach (var locale in project.Locales)
		{
			if (locale == null)
			{
				continue;
			}

			writer.BeginObject()
				.Name("code").Value(locale.Code)
				.Name("displayName").Value(locale.DisplayName);

			if (!string.IsNullOrWhiteSpace(locale.SystemLanguage))
			{
				writer.Name("systemLanguage").Value(locale.SystemLanguage);
			}

			writer.EndObject();
		}

		writer.EndArray()
			.Name("entries").BeginArray();

		foreach (var entry in project.Entries)
		{
			if (entry == null)
			{
				continue;
			}

			writer.BeginObject()
				.Name("key").Value(entry.Key)
				.Name("comment").Value(entry.Comment)
				.Name("values").BeginObject();

			foreach (var pair in entry.Values)
			{
				writer.Name(pair.Key).Value(pair.Value);
			}

			writer.EndObject();

			if (entry.Metadata.Count > 0)
			{
				writer.Name("metadata").BeginObject();
				foreach (var pair in entry.Metadata)
				{
					writer.Name(pair.Key).Value(pair.Value);
				}

				writer.EndObject();
			}

			writer.EndObject();
		}

		writer.EndArray();

		if (project.Metadata.Count > 0)
		{
			writer.Name("metadata").BeginObject();
			foreach (var pair in project.Metadata)
			{
				writer.Name(pair.Key).Value(pair.Value);
			}

			writer.EndObject();
		}

		writer.EndObject();
		return writer.ToString();
	}

	public LocalizationProject Deserialize(string payload)
	{
		if (string.IsNullOrWhiteSpace(payload))
		{
			throw new ArgumentException("Payload is empty.", nameof(payload));
		}

		var reader = new JsonReader(payload);
		var root = AsObject(reader.ReadValue(), "root");
		var project = new LocalizationProject();

		if (root.TryGetValue("version", out var versionValue))
		{
			project.Version = AsInt(versionValue, LocalizationProject.CurrentVersion);
		}

		if (root.TryGetValue("locales", out var localesValue))
		{
			var locales = AsArray(localesValue, "locales");
			foreach (var localeValue in locales)
			{
				var localeObject = AsObject(localeValue, "locale");
				var code = AsString(localeObject, "code");
				if (string.IsNullOrWhiteSpace(code))
				{
					continue;
				}

				var displayName = AsString(localeObject, "displayName") ?? code;
				var systemLanguage = AsString(localeObject, "systemLanguage");
				project.Locales.Add(new LocaleInfo(code, displayName, systemLanguage));
			}
		}

		if (root.TryGetValue("entries", out var entriesValue))
		{
			var entries = AsArray(entriesValue, "entries");
			foreach (var entryValue in entries)
			{
				var entryObject = AsObject(entryValue, "entry");
				var key = AsString(entryObject, "key");
				if (string.IsNullOrWhiteSpace(key))
				{
					continue;
				}

				var entry = new LocalizationEntry(key)
				{
					Comment = AsString(entryObject, "comment")
				};

				if (entryObject.TryGetValue("values", out var valuesValue))
				{
					foreach (var pair in AsObject(valuesValue, "values"))
					{
						if (string.IsNullOrWhiteSpace(pair.Key))
						{
							continue;
						}

						entry.Values[pair.Key] = pair.Value?.ToString() ?? string.Empty;
					}
				}

				if (entryObject.TryGetValue("metadata", out var metadataValue))
				{
					foreach (var pair in AsObject(metadataValue, "metadata"))
					{
						if (string.IsNullOrWhiteSpace(pair.Key))
						{
							continue;
						}

						entry.Metadata[pair.Key] = pair.Value?.ToString() ?? string.Empty;
					}
				}

				project.Entries.Add(entry);
			}
		}

		if (root.TryGetValue("metadata", out var projectMetadata))
		{
			foreach (var pair in AsObject(projectMetadata, "metadata"))
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
				{
					continue;
				}

				project.Metadata[pair.Key] = pair.Value?.ToString() ?? string.Empty;
			}
		}

		return project;
	}

	private static Dictionary<string, object> AsObject(object value, string name)
	{
		if (value is Dictionary<string, object> map)
		{
			return map;
		}

		throw new FormatException($"Expected object for {name}.");
	}

	private static List<object> AsArray(object value, string name)
	{
		if (value is List<object> list)
		{
			return list;
		}

		throw new FormatException($"Expected array for {name}.");
	}

	private static string AsString(Dictionary<string, object> map, string key)
	{
		return map.TryGetValue(key, out var value)
			? value?.ToString()
			: null;
	}

	private static int AsInt(object value, int fallback)
	{
		if (value is int intValue)
		{
			return intValue;
		}

		if (value is long longValue)
		{
			return (int)longValue;
		}

		if (value is double doubleValue)
		{
			return (int)doubleValue;
		}

		if (value is string text && int.TryParse(text,
				NumberStyles.Integer,
				CultureInfo.InvariantCulture,
				out var parsed))
		{
			return parsed;
		}

		return fallback;
	}
}

internal sealed class JsonWriter
{
	private readonly StringBuilder _builder = new();
	private readonly Stack<JsonScope> _scopes = new();
	private bool _expectValue;

	public JsonWriter BeginObject()
	{
		WriteValuePrefix();
		_builder.Append('{');
		_scopes.Push(new JsonScope(true));
		return this;
	}

	public JsonWriter EndObject()
	{
		_builder.Append('}');
		_scopes.Pop();
		return this;
	}

	public JsonWriter BeginArray()
	{
		WriteValuePrefix();
		_builder.Append('[');
		_scopes.Push(new JsonScope(false));
		return this;
	}

	public JsonWriter EndArray()
	{
		_builder.Append(']');
		_scopes.Pop();
		return this;
	}

	public JsonWriter Name(string name)
	{
		if (_scopes.Count == 0 || !_scopes.Peek().IsObject)
		{
			throw new InvalidOperationException("Property name outside object.");
		}

		var scope = _scopes.Peek();
		if (!scope.First)
		{
			_builder.Append(',');
		}

		scope.First = false;

		WriteString(name ?? string.Empty);
		_builder.Append(':');
		_expectValue = true;
		return this;
	}

	public JsonWriter Value(string value)
	{
		WriteValuePrefix();
		if (value == null)
		{
			_builder.Append("null");
		}
		else
		{
			WriteString(value);
		}

		return this;
	}

	public JsonWriter Value(int value)
	{
		WriteValuePrefix();
		_builder.Append(value.ToString(CultureInfo.InvariantCulture));
		return this;
	}

	public JsonWriter Value(bool value)
	{
		WriteValuePrefix();
		_builder.Append(value ? "true" : "false");
		return this;
	}

	public override string ToString()
	{
		return _builder.ToString();
	}

	private void WriteValuePrefix()
	{
		if (_expectValue)
		{
			_expectValue = false;
			return;
		}

		if (_scopes.Count == 0)
		{
			return;
		}

		var scope = _scopes.Peek();
		if (!scope.First)
		{
			_builder.Append(',');
		}

		scope.First = false;
	}

	private void WriteString(string value)
	{
		_builder.Append('"');
		foreach (var c in value)
		{
			switch (c)
			{
				case '\\':
					_builder.Append("\\\\");
					break;
				case '"':
					_builder.Append("\\\"");
					break;
				case '\n':
					_builder.Append("\\n");
					break;
				case '\r':
					_builder.Append("\\r");
					break;
				case '\t':
					_builder.Append("\\t");
					break;
				default:
					_builder.Append(c);
					break;
			}
		}

		_builder.Append('"');
	}

	private sealed class JsonScope
	{
		public JsonScope(bool isObject)
		{
			IsObject = isObject;
		}

		public bool IsObject { get; }
		public bool First { get; set; } = true;
	}
}

internal sealed class JsonReader
{
	private readonly string _text;
	private int _index;

	public JsonReader(string text)
	{
		_text = text ?? string.Empty;
	}

	public object ReadValue()
	{
		SkipWhitespace();
		if (_index >= _text.Length)
		{
			throw new FormatException("Unexpected end of JSON.");
		}

		var c = _text[_index];
		switch (c)
		{
			case '{':
				return ReadObject();
			case '[':
				return ReadArray();
			case '"':
				return ReadString();
			case 't':
				ReadLiteral("true");
				return true;
			case 'f':
				ReadLiteral("false");
				return false;
			case 'n':
				ReadLiteral("null");
				return null;
			default:
				if (c == '-' || char.IsDigit(c))
				{
					return ReadNumber();
				}

				break;
		}

		throw new FormatException($"Unexpected token '{c}'.");
	}

	private Dictionary<string, object> ReadObject()
	{
		Expect('{');
		SkipWhitespace();

		var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
		if (TryConsume('}'))
		{
			return result;
		}

		while (true)
		{
			SkipWhitespace();
			var key = ReadString();
			SkipWhitespace();
			Expect(':');
			var value = ReadValue();
			result[key] = value;

			SkipWhitespace();
			if (TryConsume('}'))
			{
				return result;
			}

			Expect(',');
		}
	}

	private List<object> ReadArray()
	{
		Expect('[');
		SkipWhitespace();

		var list = new List<object>();
		if (TryConsume(']'))
		{
			return list;
		}

		while (true)
		{
			list.Add(ReadValue());
			SkipWhitespace();
			if (TryConsume(']'))
			{
				return list;
			}

			Expect(',');
		}
	}

	private string ReadString()
	{
		Expect('"');
		var builder = new StringBuilder();
		while (_index < _text.Length)
		{
			var c = _text[_index++];
			if (c == '"')
			{
				return builder.ToString();
			}

			if (c == '\\')
			{
				if (_index >= _text.Length)
				{
					throw new FormatException("Unexpected end of string.");
				}

				var esc = _text[_index++];
				switch (esc)
				{
					case '"':
						builder.Append('"');
						break;
					case '\\':
						builder.Append('\\');
						break;
					case '/':
						builder.Append('/');
						break;
					case 'b':
						builder.Append('\b');
						break;
					case 'f':
						builder.Append('\f');
						break;
					case 'n':
						builder.Append('\n');
						break;
					case 'r':
						builder.Append('\r');
						break;
					case 't':
						builder.Append('\t');
						break;
					case 'u':
						builder.Append(ReadUnicode());
						break;
					default:
						throw new FormatException($"Invalid escape '\\{esc}'.");
				}

				continue;
			}

			builder.Append(c);
		}

		throw new FormatException("Unterminated string.");
	}

	private char ReadUnicode()
	{
		if (_index + 4 > _text.Length)
		{
			throw new FormatException("Invalid unicode escape.");
		}

		var hex = _text.Substring(_index, 4);
		_index += 4;
		if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
				out var code))
		{
			return (char)code;
		}

		throw new FormatException("Invalid unicode escape.");
	}

	private object ReadNumber()
	{
		var start = _index;
		if (_text[_index] == '-')
		{
			_index++;
		}

		while (_index < _text.Length && char.IsDigit(_text[_index]))
		{
			_index++;
		}

		if (_index < _text.Length && _text[_index] == '.')
		{
			_index++;
			while (_index < _text.Length && char.IsDigit(_text[_index]))
			{
				_index++;
			}
		}

		if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
		{
			_index++;
			if (_index < _text.Length &&
				(_text[_index] == '+' || _text[_index] == '-'))
			{
				_index++;
			}

			while (_index < _text.Length && char.IsDigit(_text[_index]))
			{
				_index++;
			}
		}

		var token = _text.Substring(start, _index - start);
		if (token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0)
		{
			return double.Parse(token, CultureInfo.InvariantCulture);
		}

		if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
				out var longValue))
		{
			if (longValue >= int.MinValue && longValue <= int.MaxValue)
			{
				return (int)longValue;
			}

			return longValue;
		}

		return double.Parse(token, CultureInfo.InvariantCulture);
	}

	private void ReadLiteral(string literal)
	{
		for (var i = 0; i < literal.Length; i++)
		{
			if (_index + i >= _text.Length || _text[_index + i] != literal[i])
			{
				throw new FormatException($"Expected '{literal}'.");
			}
		}

		_index += literal.Length;
	}

	private void SkipWhitespace()
	{
		while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
		{
			_index++;
		}
	}

	private void Expect(char expected)
	{
		if (_index >= _text.Length || _text[_index] != expected)
		{
			throw new FormatException($"Expected '{expected}'.");
		}

		_index++;
	}

	private bool TryConsume(char expected)
	{
		if (_index < _text.Length && _text[_index] == expected)
		{
			_index++;
			return true;
		}

		return false;
	}
}
}