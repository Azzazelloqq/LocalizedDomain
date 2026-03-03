using System.Collections.Generic;
using NUnit.Framework;

namespace LocalizedDomain.Tests
{
public sealed class LocalizationCoreTests
{
	[Test]
	public void JsonSerializer_RoundTrip_PreservesData()
	{
		var project = new LocalizationProject();
		project.Locales.Add(new LocaleInfo("en", "English", "English"));
		project.Locales.Add(new LocaleInfo("ru", "Russian"));
		project.Metadata["source"] = "tests";

		var entry = new LocalizationEntry("menu.play")
		{
			Comment = "Play button"
		};
		entry.Values["en"] = "Play";
		entry.Values["ru"] = "Play_ru";
		entry.Metadata["context"] = "main_menu";
		project.Entries.Add(entry);

		var serializer = new LocalizationJsonSerializer();
		var json = serializer.Serialize(project);
		var rehydrated = serializer.Deserialize(json);

		Assert.AreEqual(2, rehydrated.Locales.Count);
		Assert.AreEqual(1, rehydrated.Entries.Count);
		Assert.AreEqual("tests", rehydrated.Metadata["source"]);
		Assert.AreEqual("Play", rehydrated.Entries[0].Values["en"]);
		Assert.AreEqual("Play_ru", rehydrated.Entries[0].Values["ru"]);
		Assert.AreEqual("English", rehydrated.Locales[0].SystemLanguage);
	}

	[Test]
	public void Resolver_UsesFallbackLocale()
	{
		var project = new LocalizationProject();
		project.Locales.Add(new LocaleInfo("en", "English"));

		var entry = new LocalizationEntry("menu.exit");
		entry.Values["en"] = "Exit";
		project.Entries.Add(entry);

		var store = new InMemoryLocalizationStore();
		store.Load(project);

		var resolver = new LocalizationResolver();
		var resolved = resolver.TryResolve(store, "menu.exit", "fr",
			new[] { "en" }, out var text);

		Assert.IsTrue(resolved);
		Assert.AreEqual("Exit", text);
	}

	[Test]
	public void Formatter_ReplacesPlaceholders_AndEscapes()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["name"] = "Bob"
		};

		var result = formatter.Format("Hello {name}, {{test}}", args, "en");

		Assert.AreEqual("Hello Bob, {test}", result);
	}

	[Test]
	public void Formatter_KeepsUnknownPlaceholder()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["name"] = "Bob"
		};

		var result = formatter.Format("Hello {name} {unknown}", args, "en");

		Assert.AreEqual("Hello Bob {unknown}", result);
	}

	[Test]
	public void Store_UsesLocaleCaseInsensitively()
	{
		var project = new LocalizationProject();
		project.Locales.Add(new LocaleInfo("en", "English"));

		var entry = new LocalizationEntry("menu.play");
		entry.Values["en"] = "Play";
		project.Entries.Add(entry);

		var store = new InMemoryLocalizationStore();
		store.Load(project);

		Assert.IsTrue(store.TryGet("EN", "menu.play", out var text));
		Assert.AreEqual("Play", text);
		Assert.IsFalse(store.TryGet("en", "MENU.PLAY", out _));
	}

	[Test]
	public void Service_UsesFallbackAndFormats()
	{
		var project = new LocalizationProject();
		project.Locales.Add(new LocaleInfo("en", "English"));

		var entry = new LocalizationEntry("welcome.user");
		entry.Values["en"] = "Hi {name}";
		project.Entries.Add(entry);

		var service = new LocalizationService();
		service.Load(project);
		service.SetLocale("fr");
		service.SetFallbackLocales(new[] { "en" });

		var result = service.Get("welcome.user", "fallback",
			new Dictionary<string, string> { ["name"] = "Alex" });

		Assert.AreEqual("Hi Alex", result);
	}

	[Test]
	public void Resolver_ReturnsFalse_WhenMissingKey()
	{
		var project = new LocalizationProject();
		project.Locales.Add(new LocaleInfo("en", "English"));

		var store = new InMemoryLocalizationStore();
		store.Load(project);

		var resolver = new LocalizationResolver();
		var resolved = resolver.TryResolve(store, "missing", "en",
			new[] { "en" }, out var text);

		Assert.IsFalse(resolved);
		Assert.IsNull(text);
	}

	[Test]
	public void JsonSerializer_AllowsMissingSections()
	{
		var serializer = new LocalizationJsonSerializer();
		var json = "{\"version\":1,\"locales\":[{\"code\":\"en\"}]}";

		var project = serializer.Deserialize(json);

		Assert.AreEqual(1, project.Locales.Count);
		Assert.AreEqual(0, project.Entries.Count);
	}

	[Test]
	public void Formatter_IcuPlural_English()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["count"] = "1"
		};

		var template = "{count, plural, one{# apple} other{# apples}}";
		var result = formatter.Format(template, args, "en");

		Assert.AreEqual("1 apple", result);
	}

	[Test]
	public void Formatter_IcuPlural_RussianFew()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["count"] = "3"
		};

		var template = "{count, plural, one{# предмет} few{# предмета} many{# предметов} other{# предмета}}";
		var result = formatter.Format(template, args, "ru");

		Assert.AreEqual("3 предмета", result);
	}

	[Test]
	public void Formatter_IcuPlural_ExactMatch()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["count"] = "0"
		};

		var template = "{count, plural, =0{no items} one{# item} other{# items}}";
		var result = formatter.Format(template, args, "en");

		Assert.AreEqual("no items", result);
	}

	[Test]
	public void Formatter_IcuSelect_UsesSpecific()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["gender"] = "female"
		};

		var template = "{gender, select, male{He} female{She} other{They}}";
		var result = formatter.Format(template, args, "en");

		Assert.AreEqual("She", result);
	}

	[Test]
	public void Formatter_IcuSelect_UsesOther()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["gender"] = "unknown"
		};

		var template = "{gender, select, male{He} female{She} other{They}}";
		var result = formatter.Format(template, args, "en");

		Assert.AreEqual("They", result);
	}

	[Test]
	public void Formatter_IcuSelect_WithPluralNested()
	{
		var formatter = new DefaultLocalizationFormatter();
		var args = new Dictionary<string, string>
		{
			["gender"] = "male",
			["count"] = "2"
		};

		var template =
			"{gender, select, male{{count, plural, one{He has # item} other{He has # items}}} other{They have # items}}";
		var result = formatter.Format(template, args, "en");

		Assert.AreEqual("He has 2 items", result);
	}
}
}