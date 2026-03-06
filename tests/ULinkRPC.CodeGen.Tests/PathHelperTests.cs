using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class PathHelperTests
{
    [Theory]
    [InlineData("Rpc", "Rpc")]
    [InlineData("RpcGenerated", "RpcGenerated")]
    [InlineData("my-module", "mymodule")]
    [InlineData("3d_models", "_3d_models")]
    [InlineData("", "")]
    public void ToNamespaceIdentifier_Sanitizes(string input, string expected)
    {
        Assert.Equal(expected, PathHelper.ToNamespaceIdentifier(input));
    }

    [Fact]
    public void DeriveNamespaceFromOutputPath_Empty_ReturnsFallback()
    {
        Assert.Equal(PathHelper.DefaultUnityRuntimeNamespace,
            PathHelper.DeriveNamespaceFromOutputPath(""));
    }

    [Fact]
    public void DeriveNamespaceFromOutputPath_AssetsScripts_SkipsPrefix()
    {
        var path = Path.Combine("C:", "MyUnity", "Assets", "Scripts", "Rpc", "RpcGenerated");
        var result = PathHelper.DeriveNamespaceFromOutputPath(path);
        Assert.Equal("Rpc.Generated", result);
    }

    [Fact]
    public void DeriveNamespaceFromOutputPath_NoAssetsScripts_UsesFullPath()
    {
        var path = Path.Combine("C:", "SomeDir", "Rpc");
        var result = PathHelper.DeriveNamespaceFromOutputPath(path);
        Assert.Contains("Rpc", result);
    }
}
