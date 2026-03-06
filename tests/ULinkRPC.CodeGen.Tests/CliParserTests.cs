using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class CliParserTests
{
    #region TryParseCliArguments

    [Fact]
    public void Parse_UnityMode_AllOptions()
    {
        var args = new[] { "--contracts", "/c", "--mode", "unity", "--output", "/o", "--namespace", "My.Ns" };

        Assert.True(CliParser.TryParseCliArguments(args, out var opt, out _));
        Assert.Equal("/c", opt.ContractsPath);
        Assert.Equal(OutputMode.Unity, opt.Mode);
        Assert.Equal("/o", opt.OutputPath);
        Assert.Equal("My.Ns", opt.UnityNamespace);
    }

    [Fact]
    public void Parse_ServerMode_AllOptions()
    {
        var args = new[] { "--contracts", "/c", "--mode", "server", "--server-output", "/so", "--server-namespace", "S.Ns" };

        Assert.True(CliParser.TryParseCliArguments(args, out var opt, out _));
        Assert.Equal(OutputMode.Server, opt.Mode);
        Assert.Equal("/so", opt.ServerOutputPath);
        Assert.Equal("S.Ns", opt.ServerNamespace);
    }

    [Fact]
    public void Parse_EmptyArgs_Succeeds()
    {
        Assert.True(CliParser.TryParseCliArguments([], out var opt, out _));
        Assert.Equal(OutputMode.Unknown, opt.Mode);
        Assert.Equal(string.Empty, opt.ContractsPath);
    }

    [Fact]
    public void Parse_UnknownMode_Fails()
    {
        var args = new[] { "--contracts", "/c", "--mode", "invalid" };
        Assert.False(CliParser.TryParseCliArguments(args, out _, out var error));
        Assert.Contains("Unknown mode", error);
    }

    [Fact]
    public void Parse_UnknownOption_Fails()
    {
        Assert.False(CliParser.TryParseCliArguments(["--unknown"], out _, out var error));
        Assert.Contains("Unknown or incomplete option", error);
    }

    [Fact]
    public void Parse_IncompleteOption_MissingValue_Fails()
    {
        Assert.False(CliParser.TryParseCliArguments(["--contracts"], out _, out var error));
        Assert.Contains("Unknown or incomplete option", error);
    }

    [Theory]
    [InlineData("unity", "Unity")]
    [InlineData("Unity", "Unity")]
    [InlineData("UNITY", "Unity")]
    [InlineData("server", "Server")]
    [InlineData("Server", "Server")]
    [InlineData("SERVER", "Server")]
    public void Parse_ModeCaseInsensitive(string modeStr, string expectedMode)
    {
        var args = new[] { "--contracts", "/c", "--mode", modeStr };
        Assert.True(CliParser.TryParseCliArguments(args, out var opt, out _));
        Assert.Equal(expectedMode, opt.Mode.ToString());
    }

    [Fact]
    public void Parse_MinimalUnityArgs()
    {
        var args = new[] { "--contracts", "/c", "--mode", "unity" };
        Assert.True(CliParser.TryParseCliArguments(args, out var opt, out _));
        Assert.Equal(string.Empty, opt.OutputPath);
        Assert.Equal(string.Empty, opt.UnityNamespace);
    }

    [Fact]
    public void Parse_MinimalServerArgs()
    {
        var args = new[] { "--contracts", "/c", "--mode", "server" };
        Assert.True(CliParser.TryParseCliArguments(args, out var opt, out _));
        Assert.Equal(string.Empty, opt.ServerOutputPath);
        Assert.Equal(string.Empty, opt.ServerNamespace);
    }

    #endregion

    #region TryResolveGenerationOptions

    [Fact]
    public void Resolve_MissingContracts_Fails()
    {
        var raw = new RawOptions("", "/o", "Ns", "", "", OutputMode.Unity);
        Assert.False(CliParser.TryResolveGenerationOptions(raw, out _, out var error));
        Assert.Contains("--contracts", error);
    }

    [Fact]
    public void Resolve_MissingMode_Fails()
    {
        var raw = new RawOptions("/c", "", "", "", "", OutputMode.Unknown);
        Assert.False(CliParser.TryResolveGenerationOptions(raw, out _, out var error));
        Assert.Contains("--mode", error);
    }

    [Fact]
    public void Resolve_ContractsPathNotFound_Fails()
    {
        var raw = new RawOptions("/nonexistent_path_xxx", "/o", "Ns", "", "", OutputMode.Unity);
        Assert.False(CliParser.TryResolveGenerationOptions(raw, out _, out var error));
        Assert.Contains("not found", error);
    }

    [Fact]
    public void Resolve_ServerMode_DefaultsOutputPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_contracts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var raw = new RawOptions(tempDir, "", "", "", "", OutputMode.Server);
            Assert.True(CliParser.TryResolveGenerationOptions(raw, out var opt, out _));
            Assert.EndsWith("Generated", opt.ServerOutputPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_ServerMode_ExplicitOutputPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_contracts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var raw = new RawOptions(tempDir, "", "", "/my/server/out", "My.Ns", OutputMode.Server);
            Assert.True(CliParser.TryResolveGenerationOptions(raw, out var opt, out _));
            Assert.Equal(Path.GetFullPath("/my/server/out"), opt.ServerOutputPath);
            Assert.Equal("My.Ns", opt.ServerNamespace);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("123Invalid")]
    [InlineData("has space")]
    [InlineData("dot..double")]
    [InlineData("trailing.")]
    [InlineData(".leading")]
    public void Resolve_UnityMode_InvalidNamespace_Fails(string badNs)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_contracts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var raw = new RawOptions(tempDir, "/out", badNs, "", "", OutputMode.Unity);
            Assert.False(CliParser.TryResolveGenerationOptions(raw, out _, out var error));
            Assert.Contains("Invalid namespace", error);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("123Bad")]
    [InlineData("has space")]
    public void Resolve_ServerMode_InvalidNamespace_Fails(string badNs)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_contracts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var raw = new RawOptions(tempDir, "", "", "/so", badNs, OutputMode.Server);
            Assert.False(CliParser.TryResolveGenerationOptions(raw, out _, out var error));
            Assert.Contains("Invalid namespace", error);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Resolve_UnityMode_ExplicitOutputAndNamespace()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_contracts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var raw = new RawOptions(tempDir, "/out", "Custom.Ns", "", "", OutputMode.Unity);
            Assert.True(CliParser.TryResolveGenerationOptions(raw, out var opt, out _));
            Assert.Equal(Path.GetFullPath("/out"), opt.OutputPath);
            Assert.Equal("Custom.Ns", opt.UnityNamespace);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion
}
