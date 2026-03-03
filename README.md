# LocalizedDomain

Flexible localization library with Unity editor tooling and a Unity-agnostic core.

## Features
- Core library without Unity dependencies (`LocalizedDomain.Core`)
- Unity runtime wrapper (`LocalizedDomain.Unity`)
- Editor tools for import/export and validation
- Google Sheets CSV import (public sheets)
- Versioned JSON export format
- Optional DialogSystem adapter

## Data format (JSON)
Schema example:

{
  "version": 1,
  "locales": [
    { "code": "en", "displayName": "English" }
  ],
  "entries": [
    {
      "key": "menu.play",
      "comment": "Play",
      "values": { "en": "Play" }
    }
  ],
  "metadata": {
    "source": "google_sheets"
  }
}

## Editor workflow
1. Create a `LocalizationEditorSettings` asset.
2. Set **JSON Path** (this is the source of truth for edits).
3. Optional: create a `GoogleSheetsCsvSource` asset (Sheet Id or CSV URL) and assign it.
4. Open `Tools/LocalizedDomain/Localization Editor`.
5. Manage locales in the **Locales** foldout (codes + display names).
6. Use the left **Keys** list to add/search entries.
7. Edit locale texts in the right **Entry Details** panel (supports locale filter).
8. Click **Save JSON** to persist changes (or enable Auto-save).
9. Use **Diagnostics** to see missing coverage and fill from default locale.

Default JSON path: `Assets/Resources/Localization/localization.json`.

## Google Sheets CSV format
Header row format:
- Column 0: key
- Column 1: comment
- Columns 2..N: locale codes (optional `code|DisplayName`)

Example:
key,comment,en|English,ru|Russian
menu.play,Play,Play,Play_ru

## Runtime usage
Add `LocalizationRuntimeLoader` to a scene and assign the JSON TextAsset.

Or load manually:
LocalizationRuntime.LoadFromJson(json);
LocalizationRuntime.Service.SetLocale("en");
var text = LocalizationRuntime.Service.Get("menu.play", "Play");

## Unity convenience components
- `LocalizedUIText` for legacy `UnityEngine.UI.Text`.
- `LocaleSwitcher` for `UnityEngine.UI.Dropdown`.
- `LocalizedTMPText` + `LocaleSwitcherTMP` (in separate assembly `LocalizedDomain.Unity.TextMeshPro`).
`LocaleSwitcher` uses `LocaleInfo.SystemLanguage` (set per locale) for auto-detect.

## Addressables loading
If you use Addressables, add `LocalizationAddressablesLoader` (assembly `LocalizedDomain.Unity.Addressables`).

## ICU pluralization
Use ICU-style syntax in translations:
```
{count, plural, one{# apple} few{# apples} many{# apples} other{# apples}}
```

## ICU select (gender, variants)
```
{gender, select, male{He} female{She} other{They}}
```

## Throttled UI updates
Mass locale changes can be processed in batches to avoid frame spikes.
Configure `LocalizationUpdateScheduler.MaxPerFrame` (0 = no throttling).

To use key picker in inspector:
```
[LocalizationKey] public string Key;
```

## Compile core outside Unity
Use `dotnet build LocalizedDomain.Core.External.csproj` at repo root.

## Custom sources
Implement `LocalizationSourceAsset` to plug in any import source.
