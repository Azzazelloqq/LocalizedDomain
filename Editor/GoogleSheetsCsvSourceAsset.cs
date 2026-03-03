using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using LocalizedDomain;
using UnityEngine;

namespace LocalizedDomain.Editor
{
[CreateAssetMenu(menuName = "LocalizedDomain/Sources/Google Sheets (CSV)",
	fileName = "GoogleSheetsCsvSource")]
public sealed class GoogleSheetsCsvSourceAsset : LocalizationSourceAsset
{
	[SerializeField]
	private string _sheetId;

	[SerializeField]
	private string _gid = "0";

	[SerializeField]
	private string _csvUrlOverride;

	[SerializeField]
	private int _keyColumnIndex;

	[SerializeField]
	private int _commentColumnIndex = 1;

	[SerializeField]
	private int _localeStartIndex = 2;

	[SerializeField]
	private bool _trimValues = true;

	public override LocalizationProject Load()
	{
		var url = BuildUrl();
		var csv = DownloadCsv(url);
		return ParseCsv(csv);
	}

	private string BuildUrl()
	{
		if (!string.IsNullOrWhiteSpace(_csvUrlOverride))
		{
			return _csvUrlOverride;
		}

		if (string.IsNullOrWhiteSpace(_sheetId))
		{
			throw new InvalidOperationException("Sheet Id is empty.");
		}

		return $"https://docs.google.com/spreadsheets/d/{_sheetId}/export?format=csv&gid={_gid}";
	}

	private static string DownloadCsv(string url)
	{
		using (var client = new HttpClient())
		{
			return client.GetStringAsync(url).GetAwaiter().GetResult();
		}
	}

	private LocalizationProject ParseCsv(string csv)
	{
		var rows = CsvParser.Parse(csv);
		var project = new LocalizationProject();

		if (rows.Count == 0)
		{
			return project;
		}

		var header = rows[0];
		var localeColumns = new List<LocaleColumn>();

		for (var i = _localeStartIndex; i < header.Count; i++)
		{
			var headerValue = Normalize(header[i]);
			if (string.IsNullOrWhiteSpace(headerValue))
			{
				continue;
			}

			var locale = ParseLocaleHeader(headerValue);
			if (!string.IsNullOrWhiteSpace(locale?.Code))
			{
				localeColumns.Add(new LocaleColumn(i, locale));
				project.Locales.Add(locale);
			}
		}

		for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
		{
			var row = rows[rowIndex];
			var key = GetCell(row, _keyColumnIndex);
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			var entry = new LocalizationEntry(key)
			{
				Comment = GetCell(row, _commentColumnIndex)
			};

			foreach (var column in localeColumns)
			{
				var value = GetCell(row, column.Index);
				if (!string.IsNullOrWhiteSpace(value))
				{
					entry.Values[column.Locale.Code] = value;
				}
			}

			project.Entries.Add(entry);
		}

		return project;
	}

	private string GetCell(List<string> row, int index)
	{
		if (index < 0 || index >= row.Count)
		{
			return null;
		}

		return Normalize(row[index]);
	}

	private string Normalize(string value)
	{
		if (value == null)
		{
			return null;
		}

		return _trimValues ? value.Trim() : value;
	}

	private static LocaleInfo ParseLocaleHeader(string header)
	{
		var parts = header.Split(new[] { '|' }, 2, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
		{
			return null;
		}

		var code = parts[0].Trim();
		var displayName = parts.Length > 1 ? parts[1].Trim() : code;
		return new LocaleInfo(code, displayName);
	}

	private sealed class LocaleColumn
	{
		public LocaleColumn(int index, LocaleInfo locale)
		{
			Index = index;
			Locale = locale;
		}

		public int Index { get; }
		public LocaleInfo Locale { get; }
	}

	private static class CsvParser
	{
		public static List<List<string>> Parse(string csv)
		{
			var rows = new List<List<string>>();
			if (string.IsNullOrEmpty(csv))
			{
				return rows;
			}

			var row = new List<string>();
			var field = new StringBuilder();
			var inQuotes = false;

			for (var i = 0; i < csv.Length; i++)
			{
				var c = csv[i];
				if (inQuotes)
				{
					if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"')
					{
						field.Append('"');
						i++;
						continue;
					}

					if (c == '"')
					{
						inQuotes = false;
						continue;
					}

					field.Append(c);
					continue;
				}

				if (c == '"')
				{
					inQuotes = true;
					continue;
				}

				if (c == ',')
				{
					row.Add(field.ToString());
					field.Clear();
					continue;
				}

				if (c == '\n')
				{
					row.Add(field.ToString());
					field.Clear();
					rows.Add(row);
					row = new List<string>();
					continue;
				}

				if (c == '\r')
				{
					continue;
				}

				field.Append(c);
			}

			row.Add(field.ToString());
			rows.Add(row);

			return rows;
		}
	}
}
}