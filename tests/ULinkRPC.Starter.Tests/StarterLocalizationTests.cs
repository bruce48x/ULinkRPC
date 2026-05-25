using System.Globalization;
using ULinkRPC.Starter;
using Xunit;

namespace ULinkRPC.Starter.Tests;

public sealed class StarterLocalizationTests
{
    [Theory]
    [InlineData("en-US", (int)StarterLanguage.English)]
    [InlineData("zh", (int)StarterLanguage.SimplifiedChinese)]
    [InlineData("zh-CN", (int)StarterLanguage.SimplifiedChinese)]
    [InlineData("zh-Hans", (int)StarterLanguage.SimplifiedChinese)]
    [InlineData("zh-SG", (int)StarterLanguage.SimplifiedChinese)]
    [InlineData("zh-TW", (int)StarterLanguage.TraditionalChinese)]
    [InlineData("zh-Hant", (int)StarterLanguage.TraditionalChinese)]
    [InlineData("zh-CHT", (int)StarterLanguage.TraditionalChinese)]
    [InlineData("zh-HK", (int)StarterLanguage.TraditionalChinese)]
    [InlineData("zh-MO", (int)StarterLanguage.TraditionalChinese)]
    public void DetectLanguage_MapsSupportedCultures(string cultureName, int expected)
    {
        Assert.Equal((StarterLanguage)expected, StarterText.DetectLanguage(CultureInfo.GetCultureInfo(cultureName)));
    }

    [Fact]
    public void PrintUsage_UsesSimplifiedChinese_ForZhCnUiCulture()
    {
        var output = WithUiCulture("zh-CN", CaptureStdout(StarterCli.PrintUsage));

        Assert.Contains("用法:", output);
        Assert.Contains("ulinkrpc-starter new", output);
    }

    [Fact]
    public void PrintUsage_UsesTraditionalChinese_ForZhTwUiCulture()
    {
        var output = WithUiCulture("zh-TW", CaptureStdout(StarterCli.PrintUsage));

        Assert.Contains("用法:", output);
        Assert.Contains("ulinkrpc-starter new", output);
    }

    [Theory]
    [InlineData("zh-CN", "未知命令: codegen")]
    [InlineData("zh-TW", "未知命令: codegen")]
    public void TryParseArgs_LocalizesErrors_ForChineseUiCultures(string cultureName, string expectedError)
    {
        var result = WithUiCulture(cultureName, () =>
        {
            var ok = StarterCli.TryParseArgs(["codegen"], out _, out var error);
            return (ok, error);
        });

        Assert.False(result.ok);
        Assert.Equal(expectedError, result.error);
    }

    [Theory]
    [InlineData("zh-CN", "  3) 使用 Unity 2022 LTS（中国网络环境默认配置） 打开 \"Client\"。")]
    [InlineData("zh-TW", "  3) 使用 Unity 2022 LTS（中國網路環境預設配置） 開啟 \"Client\"。")]
    public void OpenClientStep_LocalizesUnityCnNextStep(string cultureName, string expected)
    {
        var text = StarterText.ForCulture(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expected, text.OpenClientStep(ClientEngineKind.UnityCn));
    }

    private static Func<string> CaptureStdout(Action action) => () =>
    {
        var previousOut = Console.Out;
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    };

    private static T WithUiCulture<T>(string cultureName, Func<T> action)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var culture = CultureInfo.GetCultureInfo(cultureName);

        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
