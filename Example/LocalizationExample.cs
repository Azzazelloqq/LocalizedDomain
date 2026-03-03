using System.Collections.Generic;
using UnityEngine;
using LocalizedDomain.Unity;

namespace LocalizedDomain.Example
{
public sealed class LocalizationExample : MonoBehaviour
{
	[SerializeField]
	private string _key = "menu.play";

	[SerializeField]
	private string _fallback = "Play";

	[SerializeField]
	private string _localeOverride;

	[SerializeField]
	private List<LocalizationArgument> _arguments = new();

	private void Start()
	{
		if (!string.IsNullOrWhiteSpace(_localeOverride))
		{
			LocalizationRuntime.Service.SetLocale(_localeOverride);
		}

		var args = BuildArguments();
		var text = LocalizationRuntime.Service.Get(_key, _fallback, args);
		Debug.Log(text);
	}

	private IReadOnlyDictionary<string, string> BuildArguments()
	{
		if (_arguments == null || _arguments.Count == 0)
		{
			return null;
		}

		var result = new Dictionary<string, string>();
		foreach (var arg in _arguments)
		{
			if (arg == null || string.IsNullOrWhiteSpace(arg.Key))
			{
				continue;
			}

			result[arg.Key] = arg.Value ?? string.Empty;
		}

		return result.Count > 0 ? result : null;
	}

	[System.Serializable]
	public sealed class LocalizationArgument
	{
		public string Key;
		public string Value;
	}
}
}