using System.Collections.Generic;
using UnityEngine;
#if LOCALIZEDDOMAIN_ADDRESSABLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace LocalizedDomain.Unity
{
#if LOCALIZEDDOMAIN_ADDRESSABLES
public sealed class LocalizationAddressablesLoader : MonoBehaviour
{
	[SerializeField]
	private string _address;

	[SerializeField]
	private bool _loadOnAwake = true;

	[SerializeField]
	private string _defaultLocale = "en";

	[SerializeField]
	private List<string> _fallbackLocales = new();

	[SerializeField]
	private bool _releaseHandleOnDisable = true;

	private AsyncOperationHandle<TextAsset> _handle;
	private bool _hasHandle;

	private void Awake()
	{
		if (_loadOnAwake)
		{
			StartLoad();
		}
	}

	public void StartLoad()
	{
		if (string.IsNullOrWhiteSpace(_address))
		{
			return;
		}

		_handle = Addressables.LoadAssetAsync<TextAsset>(_address);
		_hasHandle = true;
		_handle.Completed += OnCompleted;
	}

	private void OnDisable()
	{
		if (_releaseHandleOnDisable && _hasHandle)
		{
			Addressables.Release(_handle);
			_hasHandle = false;
		}
	}

	private void OnCompleted(AsyncOperationHandle<TextAsset> handle)
	{
		if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
		{
			LocalizationRuntime.Initialize(handle.Result, _defaultLocale, _fallbackLocales);
			return;
		}

		Debug.LogError("Localization addressables load failed.");
	}
}
#else
    public sealed class LocalizationAddressablesLoader : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning(
                "LocalizationAddressablesLoader requires com.unity.addressables package.");
        }
    }
#endif
}