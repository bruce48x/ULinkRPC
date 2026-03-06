using ULinkRPC.CodeGen;
using Xunit;

namespace ULinkRPC.CodeGen.Tests;

public class CodeWriterTests
{
    [Fact]
    public void Line_WritesTextWithNewline()
    {
        var w = new CodeWriter();
        w.Line("hello");
        Assert.Equal("hello\n", w.ToString());
    }

    [Fact]
    public void Line_EmptyWritesNewlineOnly()
    {
        var w = new CodeWriter();
        w.Line();
        Assert.Equal("\n", w.ToString());
    }

    [Fact]
    public void OpenBlock_IndentsSubsequentLines()
    {
        var w = new CodeWriter();
        w.OpenBlock("class Foo");
        w.Line("int x;");
        w.CloseBlock();

        var expected = "class Foo\n{\n    int x;\n}\n";
        Assert.Equal(expected, w.ToString());
    }

    [Fact]
    public void NestedBlocks_DoubleIndent()
    {
        var w = new CodeWriter();
        w.OpenBlock("namespace A");
        w.OpenBlock("class B");
        w.Line("int x;");
        w.CloseBlock();
        w.CloseBlock();

        var expected = "namespace A\n{\n    class B\n    {\n        int x;\n    }\n}\n";
        Assert.Equal(expected, w.ToString());
    }

    [Fact]
    public void CloseBlock_WithSuffix_AppendsSuffix()
    {
        var w = new CodeWriter();
        w.OpenBlock("server.Register(1, async (req, ct) =>");
        w.Line("return null;");
        w.CloseBlock(");");

        var expected = "server.Register(1, async (req, ct) =>\n{\n    return null;\n});\n";
        Assert.Equal(expected, w.ToString());
    }

    [Fact]
    public void WriteUsings_EnumerableOverload()
    {
        var w = new CodeWriter();
        w.WriteUsings(new[] { "System", "System.Linq" });

        Assert.Equal("using System;\nusing System.Linq;\n", w.ToString());
    }

    [Fact]
    public void WriteUsings_ParamsOverload()
    {
        var w = new CodeWriter();
        w.WriteUsings("A", "B");

        Assert.Equal("using A;\nusing B;\n", w.ToString());
    }
}
