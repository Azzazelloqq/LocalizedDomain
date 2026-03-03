using UnityEngine;
using UnityEngine.UI;

namespace LocalizedDomain.Unity
{
[RequireComponent(typeof(Text))]
public sealed class LocalizedUIText : LocalizedTextBase
{
	private Text _text;

	protected override void ApplyText(string text)
	{
		if (_text == null)
		{
			_text = GetComponent<Text>();
		}

		if (_text != null)
		{
			_text.text = text ?? string.Empty;
		}
	}
}
}