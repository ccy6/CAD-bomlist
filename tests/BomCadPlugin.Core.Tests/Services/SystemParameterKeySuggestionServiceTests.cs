using BomCadPlugin.Core.Services;
using Xunit;

namespace BomCadPlugin.Core.Tests.Services;

public sealed class SystemParameterKeySuggestionServiceTests
{
    [Fact]
    public void SuggestAvailableKey_WhenKeyExists_AppendsFirstAvailableNumber()
    {
        var suggestion = SystemParameterKeySuggestionService.SuggestAvailableKey(["L", "L1"], "L");

        Assert.Equal("l2", suggestion);
    }

    [Fact]
    public void SuggestAvailableKey_WhenKeyHasNumber_IncrementsFromBaseName()
    {
        var suggestion = SystemParameterKeySuggestionService.SuggestAvailableKey(["L", "L1", "L2"], "L1");

        Assert.Equal("l3", suggestion);
    }

    [Fact]
    public void IsKeyInUse_IgnoresCaseAndWhitespace()
    {
        var isInUse = SystemParameterKeySuggestionService.IsKeyInUse(["L"], " l ");

        Assert.True(isInUse);
    }
}
