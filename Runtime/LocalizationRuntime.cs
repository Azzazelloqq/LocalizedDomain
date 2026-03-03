using System.Collections;
using System.Collections.Generic;
using LocalizedDomain;
using UnityEngine;
using UnityEngine.Networking;

namespace LocalizedDomain.Unity
{
public static class LocalizationRuntime
{
	private static readonly LocalizationService ServiceInstance = new();
	private static readonly LocalizationJsonSerializer Serializer = new();

	public static LocalizationService Service => ServiceInstance;

	public static void Initialize(
		TextAsset jsonAsset,
		string defaultLocale,
		IEnumerable<string> fallbackLocales)
	{
		if (jsonAsset != null)
		{
			LoadFromJson(jsonAsset.text);
		}

		if (!string.IsNullOrWhiteSpace(defaultLocale))
		{
			ServiceInstance.SetLocale(defaultLocale);
		}

		ServiceInstance.SetFallbackLocales(fallbackLocales);
	}

	public static void LoadFromJson(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return;
		}

		var project = Serializer.Deserialize(json);
		ServiceInstance.Load(project);
	}
}

public sealed class LocalizationRuntimeLoader : MonoBehaviour
{
	[SerializeField]
	private TextAsset _jsonAsset;

	[SerializeField]
	private string _defaultLocale = "en";

	[SerializeField]
	private List<string> _fallbackLocales = new();

	[SerializeField]
	private bool _loadOnAwake = true;

	private void Awake()
	{
		if (_loadOnAwake)
		{
			Initialize();
		}
	}

	public void Initialize()
	{
		LocalizationRuntime.Initialize(_jsonAsset, _defaultLocale, _fallbackLocales);
	}
}

public sealed class LocalizationRemoteLoader : MonoBehaviour
{
	[SerializeField]
	private string _url;

	[SerializeField]
	private bool _loadOnStart;

	[SerializeField]
	private int _timeoutSeconds = 10;

	private void Start()
	{
		if (_loadOnStart)
		{
			StartCoroutine(Load());
		}
	}

	public IEnumerator Load()
	{
		if (string.IsNullOrWhiteSpace(_url))
		{
			yield break;
		}

		using (var request = UnityWebRequest.Get(_url))
		{
			request.timeout = Mathf.Max(1, _timeoutSeconds);
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"Localization remote load failed: {request.error}");
				yield break;
			}

			LocalizationRuntime.LoadFromJson(request.downloadHandler.text);
		}
	}
}
}