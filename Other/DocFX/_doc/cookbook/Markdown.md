# Markdown
Library info: <a href='https://github.com/xoofx/markdig'>Markdig</a>. NuGet: <u title='Paste the underlined text in menu -> Tools -> NuGet'>Markdig</u>.

```csharp
/*/ nuget -\Markdig; /*/

var md = """
## Header
Text.
""";
var s = Markdig.Markdown.ToHtml(md);
print.it(s);
```

