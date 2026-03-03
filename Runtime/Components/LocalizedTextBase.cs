using System;
using System.Collections.Generic;
using LocalizedDomain;
using UnityEngine;

namespace LocalizedDomain.Unity
{
public abstract class LocalizedTextBase : MonoBehaviour
{
	[LocalizationKey]
	[SerializeField]
	private string _key;

	[SerializeField]
	private string _fallback;

	[SerializeField]
	private bool _autoRefresh = true;

	[SerializeField]
	private List<LocalizedArgument> _arguments = new();

	private readonly Dictionary<string, string> _argsCache =
		new(StringComparer.Ordinal);

	public string Key
	{
		get => _key;
		set
		{
			if (_key == value)
			{
				return;
			}

			_key = value;
			Refresh();
		}
	}

	public string Fallback
	{
		get => _fallback;
		set
		{
			if (_fallback == value)
			{
				return;
			}

			_fallback = value;
			Refresh();
		}
	}

	public bool AutoRefresh
	{
		get => _autoRefresh;
		set => _autoRefresh = value;
	}

	public IReadOnlyList<LocalizedArgument> Arguments => _arguments;

	protected virtual void OnEnable()
	{
		LocalizationUpdateScheduler.Register(this);
		Refresh();
	}

	protected virtual void OnDisable()
	{
		LocalizationUpdateScheduler.Unregister(this);
	}

	protected virtual void OnValidate()
	{
		if (!Application.isPlaying)
		{
			Refresh();
		}
	}

	public void Refresh()
	{
		if (!Application.isPlaying)
		{
			RefreshImmediate();
			return;
		}

		LocalizationUpdateScheduler.RequestRefresh(this);
	}

	internal void RefreshImmediate()
	{
		if (LocalizationRuntime.Service == null)
		{
			return;
		}

		var text = LocalizationRuntime.Service.Get(_key, _fallback, BuildArgs());
		ApplyText(text);
	}

	protected abstract void ApplyText(string text);

	private IReadOnlyDictionary<string, string> BuildArgs()
	{
		_argsCache.Clear();
		if (_arguments == null)
		{
			return _argsCache;
		}

		foreach (var arg in _arguments)
		{
			if (arg == null || string.IsNullOrWhiteSpace(arg.Key))
			{
				continue;
			}

			_argsCache[arg.Key] = arg.Value ?? string.Empty;
		}

		return _argsCache;
	}
}

[Serializable]
public sealed class LocalizedArgument
{
	public string Key;
	public string Value;
}
}