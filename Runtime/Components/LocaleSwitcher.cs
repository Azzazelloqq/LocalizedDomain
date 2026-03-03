using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LocalizedDomain.Unity
{
public abstract class LocaleSwitcherBase : MonoBehaviour
{
	[SerializeField]
	private bool _autoDetectSystemLanguage = true;

	[SerializeField]
	private bool _refreshOptionsOnEnable = true;

	protected readonly List<string> LocaleCodes = new();

	protected virtual void OnEnable()
	{
		if (_refreshOptionsOnEnable)
		{
			RefreshOptions();
		}

		if (_autoDetectSystemLanguage)
		{
			TrySetFromSystemLanguage();
		}

		Subscribe();
	}

	protected virtual void OnDisable()
	{
		Unsubscribe();
	}

	public void RefreshOptions()
	{
		LocaleCodes.Clear();
		var labels = new List<string>();

		var locales = LocalizationRuntime.Service.Store.Locales;
		if (locales != null)
		{
			foreach (var locale in locales)
			{
				if (string.IsNullOrWhiteSpace(locale?.Code))
				{
					continue;
				}

				LocaleCodes.Add(locale.Code);
				labels.Add(string.IsNullOrWhiteSpace(locale.DisplayName)
					? locale.Code
					: locale.DisplayName);
			}
		}

		ApplyOptions(labels);
		if (LocaleCodes.Count > 0)
		{
			SetSelectedIndex(GetCurrentIndex());
		}
	}

	public void SetLocaleByIndex(int index)
	{
		if (index < 0 || index >= LocaleCodes.Count)
		{
			return;
		}

		LocalizationRuntime.Service.SetLocale(LocaleCodes[index]);
	}

	protected abstract void ApplyOptions(List<string> labels);
	protected abstract void SetSelectedIndex(int index);
	protected abstract void Subscribe();
	protected abstract void Unsubscribe();

	private int GetCurrentIndex()
	{
		var current = LocalizationRuntime.Service.CurrentLocale;
		if (string.IsNullOrWhiteSpace(current))
		{
			return 0;
		}

		return LocaleCodes.FindIndex(code =>
			string.Equals(code, current, StringComparison.OrdinalIgnoreCase));
	}

	private void TrySetFromSystemLanguage()
	{
		var systemName = Application.systemLanguage.ToString();
		var locales = LocalizationRuntime.Service.Store.Locales;
		foreach (var locale in locales)
		{
			if (locale == null)
			{
				continue;
			}

			if (!string.IsNullOrWhiteSpace(locale.SystemLanguage) &&
				string.Equals(locale.SystemLanguage, systemName, StringComparison.OrdinalIgnoreCase))
			{
				LocalizationRuntime.Service.SetLocale(locale.Code);
				return;
			}
		}

		throw new Exception($"System language {systemName} doesn't support in locales list");
	}

	[RequireComponent(typeof(Dropdown))]
	public sealed class LocaleSwitcher : LocaleSwitcherBase
	{
		[SerializeField]
		private Dropdown _dropdown;

		private void Awake()
		{
			if (_dropdown == null)
			{
				_dropdown = GetComponent<Dropdown>();
			}

			if (_dropdown == null)
			{
				throw new NullReferenceException("LocaleSwitcher requires a Dropdown reference.");
			}
		}

		protected override void ApplyOptions(List<string> labels)
		{
			_dropdown.ClearOptions();
			_dropdown.AddOptions(labels);
		}

		protected override void SetSelectedIndex(int index)
		{
			if (_dropdown.options.Count == 0)
			{
				return;
			}

			var clamped = Mathf.Clamp(index, 0, _dropdown.options.Count - 1);
			_dropdown.SetValueWithoutNotify(clamped);
		}

		protected override void Subscribe()
		{
			_dropdown.onValueChanged.AddListener(SetLocaleByIndex);
		}

		protected override void Unsubscribe()
		{
			_dropdown.onValueChanged.RemoveListener(SetLocaleByIndex);
		}
	}
}
}