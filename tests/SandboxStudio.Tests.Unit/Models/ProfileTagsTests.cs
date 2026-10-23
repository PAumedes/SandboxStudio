using FluentAssertions;
using SandboxStudio.Core.Models;
using Xunit;

namespace SandboxStudio.Tests.Unit.Models;

public sealed class ProfileTagsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" , ; ,")]
    [InlineData("#")]
    [InlineData(" , # ; ")]
    public void Parse_WhenInputHasNoTagContent_ReturnsEmpty(string? input)
    {
        ProfileTags.Parse(input).Should().BeEmpty();
    }

    [Fact]
    public void Parse_SplitsOnCommasAndSemicolonsAndTrimsEntries()
    {
        var tags = ProfileTags.Parse("  Security , Development; Tools ");

        tags.Should().Equal("Security", "Development", "Tools");
    }

    [Fact]
    public void Parse_StripsLeadingHashCharacters()
    {
        var tags = ProfileTags.Parse("#Security, ##Tools, # spaced");

        tags.Should().Equal("Security", "Tools", "spaced");
    }

    [Fact]
    public void Parse_SkipsEmptyEntriesBetweenSeparators()
    {
        var tags = ProfileTags.Parse("a,,b; ;c");

        tags.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Parse_KeepsTrailingAndInteriorHashCharacters()
    {
        var tags = ProfileTags.Parse("C#, Dev#");

        tags.Should().Equal("C#", "Dev#");
    }

    [Fact]
    public void Parse_RemovesDuplicateTagsIgnoringCaseKeepingFirstCasing()
    {
        var tags = ProfileTags.Parse("Dev, dev, DEV, Tools, tools");

        tags.Should().Equal("Dev", "Tools");
    }

    [Fact]
    public void Parse_PreservesInteriorSpacesInTagNames()
    {
        var tags = ProfileTags.Parse("dev tools, ai");

        tags.Should().Equal("dev tools", "ai");
    }

    [Fact]
    public void Format_JoinsTagsWithCommaAndSpace()
    {
        ProfileTags.Format(new[] { "Security", "Dev" }).Should().Be("Security, Dev");
    }

    [Fact]
    public void Format_WhenEmpty_ReturnsEmptyString()
    {
        ProfileTags.Format(Array.Empty<string>()).Should().BeEmpty();
    }

    [Fact]
    public void Format_WhenTagsAreNull_ThrowsArgumentNullException()
    {
        var act = () => ProfileTags.Format(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Format_RoundTripsWithParse()
    {
        ProfileTags.Parse(ProfileTags.Format(new[] { "Security", "Dev" }))
            .Should().Equal("Security", "Dev");
    }

    [Fact]
    public void MatchesAll_WhenProfileTagsAreNull_ThrowsArgumentNullException()
    {
        var act = () => ProfileTags.MatchesAll(null!, new[] { "Dev" });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MatchesAll_WhenRequiredTagsAreNull_ThrowsArgumentNullException()
    {
        var act = () => ProfileTags.MatchesAll(new[] { "Dev" }, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MatchesAll_WhenNoTagsAreRequired_ReturnsTrue()
    {
        ProfileTags.MatchesAll(new[] { "Dev" }, Array.Empty<string>()).Should().BeTrue();
    }

    [Fact]
    public void MatchesAll_WhenProfileHasEveryRequiredTag_ReturnsTrue()
    {
        ProfileTags.MatchesAll(new[] { "Security", "Dev", "Tools" }, new[] { "Dev", "Security" })
            .Should().BeTrue();
    }

    [Fact]
    public void MatchesAll_WhenAnyRequiredTagIsMissing_ReturnsFalse()
    {
        ProfileTags.MatchesAll(new[] { "Dev" }, new[] { "Dev", "Security" }).Should().BeFalse();
    }

    [Fact]
    public void MatchesAll_MatchesTagsIgnoringCase()
    {
        ProfileTags.MatchesAll(new[] { "security" }, new[] { "SECURITY" }).Should().BeTrue();
    }

    [Fact]
    public void MatchesAll_WhenProfileHasNoTagsAndTagsAreRequired_ReturnsFalse()
    {
        ProfileTags.MatchesAll(Array.Empty<string>(), new[] { "Dev" }).Should().BeFalse();
    }
}
