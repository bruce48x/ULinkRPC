using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class CliParserTests
{
    [Fact]
    public void TryParseCliArguments_ValidArgs_Succeeds()
    {
        var args = new[]
        {
            "--contracts", "/some/path",
            "--mode", "unity",
            "--output", "/out",
            "--namespace", "My.Ns"
        };

        Assert.True(CliParser.TryParseCliArguments(args, out var options, out _));
        Assert.Equal("/some/path", options.ContractsPath);
        Assert.Equal(OutputMode.Unity, options.Mode);
        Assert.Equal("/out", options.OutputPath);
        Assert.Equal("My.Ns", options.UnityNamespace);
    }

    [Fact]
    public void TryParseCliArguments_ServerMode_Succeeds()
    {
        var args = new[]
        {
            "--contracts", "/contracts",
            "--mode", "server",
            "--server-output", "/server-out",
            "--server-namespace", "My.Server.Ns"
        };

        Assert.True(CliParser.TryParseCliArguments(args, out var options, out _));
        Assert.Equal(OutputMode.Server, options.Mode);
        Assert.Equal("/server-out", options.ServerOutputPath);
        Assert.Equal("My.Server.Ns", options.ServerNamespace);
    }

    [Fact]
    public void TryParseCliArguments_UnknownMode_Fails()
    {
        var args = new[] { "--contracts", "/c", "--mode", "invalid" };

        Assert.False(CliParser.TryParseCliArguments(args, out _, out var error));
        Assert.Contains("Unknown mode", error);
    }

    [Fact]
    public void TryParseCliArguments_UnknownOption_Fails()
    {
        var args = new[] { "--unknown" };

        Assert.False(CliParser.TryParseCliArguments(args, out _, out var error));
        Assert.Contains("Unknown or incomplete option", error);
    }

    [Fact]
    public void TryParseCliArguments_EmptyArgs_Succeeds()
    {
        Assert.True(CliParser.TryParseCliArguments([], out var options, out _));
        Assert.Equal(OutputMode.Unknown, options.Mode);
    }

    [Fact]
    public void TryResolveGenerationOptions_MissingContracts_Fails()
    {
        var raw = new RawOptions("", "", "", "", "", OutputMode.Unity);

        Assert.False(CliParser.TryResolveGenerationOptions(raw, out _, out var error));
        Assert.Contains("--contracts", error);
    }

    [Fact]
    public void TryResolveGenerationOptions_MissingMode_Fails()
    {
        var raw = new RawOptions("/some/path", "", "", "", "", OutputMode.Unknown);

        Assert.False(CliParser.TryResolveGenerationOptions(raw, out _, out var error));
        Assert.Contains("--mode", error);
    }
}
