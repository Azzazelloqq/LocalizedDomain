using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LocalizedDomain.Editor
{
[CreateAssetMenu(menuName = "LocalizedDomain/Localization Editor Settings",
	fileName = "LocalizationEditorSettings")]
public sealed class LocalizationEditorSettings : ScriptableObject
{
	[SerializeField]
	private LocalizationSourceAsset _source;

	[FormerlySerializedAs("_exportPath")]
	[SerializeField]
	private string _jsonPath =
		"Assets/Resources/Localization/localization.json";

	[SerializeField]
	private string _defaultLocale = "en";

	[SerializeField]
	private List<string> _fallbackLocales = new();

	public LocalizationSourceAsset Source => _source;
	public string JsonPath => _jsonPath;
	public string DefaultLocale => _defaultLocale;
	public IReadOnlyList<string> FallbackLocales => _fallbackLocales;
}
}