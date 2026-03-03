using DialogSystem.Runtime.Localization;
using LocalizedDomain;

namespace LocalizedDomain.DialogSystem
{
public sealed class DialogSystemLocalizationProvider : IDialogLocalizationProvider
{
	private readonly LocalizationService _service;

	public DialogSystemLocalizationProvider()
		: this(Unity.LocalizationRuntime.Service)
	{
	}

	public DialogSystemLocalizationProvider(LocalizationService service)
	{
		_service = service;
	}

	public bool TryGet(string key, out string text)
	{
		if (_service == null)
		{
			text = null;
			return false;
		}

		return _service.TryGet(key, out text);
	}
}
}