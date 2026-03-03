using UnityEngine;

namespace LocalizedDomain.Editor
{
public abstract class LocalizationSourceAsset : ScriptableObject
{
	public abstract LocalizationProject Load();
}
}