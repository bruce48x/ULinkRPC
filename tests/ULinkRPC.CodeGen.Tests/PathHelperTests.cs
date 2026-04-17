using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class PathHelperTests
{
    #region ToNamespaceIdentifier

    [Theory]
    [InlineData("Rpc", "Rpc")]
    [InlineData("Generated", "Generated")]
    [InlineData("my-module", "mymodule")]
    [InlineData("3d_models", "_3d_models")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("valid_name123", "valid_name123")]
    [InlineData("has spaces", "hasspaces")]
    [InlineData("special!@#chars", "specialchars")]
    public void ToNamespaceIdentifier_Sanitizes(string input, string expected)
    {
        Assert.Equal(expected, PathHelper.ToNamespaceIdentifier(input));
    }

    #endregion

    #region IsUnityProject

    [Fact]
    public void IsUnityProject_TrueWhenBothAssetsAndPackagesExist()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"unity_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            Directory.CreateDirectory(Path.Combine(dir, "Packages"));
            Assert.True(PathHelper.IsUnityProject(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsUnityProject_FalseWhenOnlyAssetsExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"unity_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "Assets"));
            Assert.False(PathHelper.IsUnityProject(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsUnityProject_FalseWhenNeitherExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"unity_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            Assert.False(PathHelper.IsUnityProject(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    #endregion

    #region FindUnityProjectRoot

    [Fact]
    public void FindUnityProjectRoot_FindsRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"unity_root_{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "Assets", "Scripts", "Rpc");
        try
        {
            Directory.CreateDirectory(nested);
            Directory.CreateDirectory(Path.Combine(root, "Packages"));

            var found = PathHelper.FindUnityProjectRoot(nested);
            Assert.NotNull(found);
            Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(found));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FindUnityProjectRoot_ReturnsNullWhenNotFound()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"no_unity_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            Assert.Null(PathHelper.FindUnityProjectRoot(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void FindServerProjectRoot_FindsAncestorWithCsproj()
    {
        var root = Path.Combine(Path.GetTempPath(), $"server_root_{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "Generated");
        try
        {
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "Sample.Server.csproj"), "<Project />");

            var found = PathHelper.FindServerProjectRoot(nested);
            Assert.NotNull(found);
            Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(found));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FindServerProjectRoot_ReturnsNullWhenNoCsprojExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"no_server_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            Assert.Null(PathHelper.FindServerProjectRoot(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    #endregion

    #region DeriveNamespaceFromOutputPath

    [Fact]
    public void DeriveNamespaceFromOutputPath_Empty_ReturnsFallback()
    {
        Assert.Equal(PathHelper.DefaultUnityRuntimeNamespace,
            PathHelper.DeriveNamespaceFromOutputPath(""));
    }

    [Fact]
    public void DeriveNamespaceFromOutputPath_AssetsScripts_SkipsPrefix()
    {
        var path = Path.Combine("C:", "MyUnity", "Assets", "Scripts", "Rpc", "Generated");
        var result = PathHelper.DeriveNamespaceFromOutputPath(path);
        Assert.Equal("Rpc.Generated", result);
    }

    [Fact]
    public void DeriveNamespaceFromOutputPath_AssetsScriptsOnly_ReturnsFallback()
    {
        var path = Path.Combine("C:", "MyUnity", "Assets", "Scripts");
        var result = PathHelper.DeriveNamespaceFromOutputPath(path);
        Assert.Equal(PathHelper.DefaultUnityRuntimeNamespace, result);
    }

    [Fact]
    public void DeriveNamespaceFromOutputPath_DeepNesting()
    {
        var path = Path.Combine("C:", "Project", "Assets", "Scripts", "Game", "Network", "Rpc");
        var result = PathHelper.DeriveNamespaceFromOutputPath(path);
        Assert.Equal("Game.Network.Rpc", result);
    }

    [Fact]
    public void DeriveNamespaceFromOutputPath_NoAssetsScripts()
    {
        var path = Path.Combine("C:", "SomeDir", "MyModule");
        var result = PathHelper.DeriveNamespaceFromOutputPath(path);
        Assert.Contains("MyModule", result);
    }

    #endregion
}
