using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace SandboxStudio.Tests.Unit.Localization;

/// <summary>
/// Pins key/placeholder parity across all language resource files so adding a
/// string in one language cannot silently leave another language untranslated
/// or break <see cref="string.Format(string, object[])"/> call sites.
/// </summary>
public sealed class ReswParityTests
{
    private static readonly Regex PlaceholderPattern = new(@"\{\d+\}", RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SandboxStudio.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repository root containing SandboxStudio.sln must be reachable");
        return directory!.FullName;
    }

    private static string StringsRoot() =>
        Path.Combine(RepoRoot(), "src", "SandboxStudio.UI", "Strings");

    private static XElement LoadResw(string languageTag) =>
        XElement.Load(Path.Combine(StringsRoot(), languageTag, "Resources.resw"));

    private static Dictionary<string, string> Entries(XElement root) =>
        root.Elements("data")
            .ToDictionary(
                element => (string)element.Attribute("name")!,
                element => element.Element("value")?.Value ?? string.Empty);

    [Fact]
    public void EveryLanguageShipsAResourcesFile()
    {
        Directory.Exists(Path.Combine(StringsRoot(), "en-US")).Should().BeTrue();
        Directory.Exists(Path.Combine(StringsRoot(), "es-ES")).Should().BeTrue();
        File.Exists(Path.Combine(StringsRoot(), "en-US", "Resources.resw")).Should().BeTrue();
        File.Exists(Path.Combine(StringsRoot(), "es-ES", "Resources.resw")).Should().BeTrue();
    }

    [Fact]
    public void SpanishResources_DefineExactlyTheEnglishKeys()
    {
        var english = Entries(LoadResw("en-US"));
        var spanish = Entries(LoadResw("es-ES"));

        var missing = english.Keys.Except(spanish.Keys).ToList();
        var extra = spanish.Keys.Except(english.Keys).ToList();

        missing.Should().BeEmpty("every English key needs a Spanish value");
        extra.Should().BeEmpty("Spanish must not define keys absent from English");
    }

    [Fact]
    public void SpanishValues_AreNotLeftInEnglish()
    {
        var english = Entries(LoadResw("en-US"));
        var spanish = Entries(LoadResw("es-ES"));

        // Language endonyms and product names are intentionally identical.
        var allowedIdentical = new HashSet<string>
        {
            "LanguageEnglishItem.Text",
            "LanguageSpanishItem.Text",
        };

        var identical = english
            .Where(pair => !allowedIdentical.Contains(pair.Key))
            .Where(pair => spanish.TryGetValue(pair.Key, out var value) && value == pair.Value)
            .Select(pair => pair.Key)
            .ToList();

        identical.Should().BeEmpty("a Spanish value still equal to English is likely an untranslated copy");
    }

    [Fact]
    public void EveryXamlUid_HasAResourceKeyPerLanguage()
    {
        var xaml = File.ReadAllText(
            Path.Combine(RepoRoot(), "src", "SandboxStudio.UI", "MainWindow.xaml"));
        var uids = Regex.Matches(xaml, @"x:Uid=""(\w+)""")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();

        uids.Should().NotBeEmpty();
        foreach (var language in new[] { "en-US", "es-ES" })
        {
            var keys = Entries(LoadResw(language)).Keys;
            var orphan = uids.Where(uid => !keys.Any(key => key.StartsWith(uid + ".", StringComparison.Ordinal))).ToList();
            orphan.Should().BeEmpty($"every x:Uid in MainWindow.xaml needs at least one resource key in {language}");
        }
    }

    [Fact]
    public void EveryLocalizerKey_ExistsPerLanguage()
    {
        var sourceFiles = Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "src", "SandboxStudio.UI"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(
                Path.Combine(RepoRoot(), "src", "SandboxStudio.UI", "ViewModels"), "*.cs", SearchOption.AllDirectories));

        var usedKeys = sourceFiles
            .SelectMany(file => Regex.Matches(
                File.ReadAllText(file),
                @"Localizer\.(?:Get|Format)\(""(\w+)"""))
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();

        usedKeys.Should().NotBeEmpty();
        foreach (var language in new[] { "en-US", "es-ES" })
        {
            var keys = Entries(LoadResw(language)).Keys;
            var missing = usedKeys.Except(keys).ToList();
            missing.Should().BeEmpty($"every Localizer key must resolve in {language}");
        }
    }

    [Fact]
    public void Placeholders_MatchAcrossLanguages()
    {
        var english = Entries(LoadResw("en-US"));
        var spanish = Entries(LoadResw("es-ES"));

        var mismatched = english
            .Where(pair => spanish.ContainsKey(pair.Key))
            .Where(pair => PlaceholderPattern.Matches(pair.Value).Select(m => m.Value)
                .OrderBy(m => m)
                .SequenceEqual(PlaceholderPattern.Matches(spanish[pair.Key]).Select(m => m.Value).OrderBy(m => m))
                is false)
            .Select(pair => pair.Key)
            .ToList();

        mismatched.Should().BeEmpty("format placeholders must line up or string.Format throws at runtime");
    }
}
