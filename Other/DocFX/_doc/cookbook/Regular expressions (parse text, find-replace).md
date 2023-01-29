# Regular expressions (parse text, find-replace)
Regular expressions are used to parse text: compare, find/get substrings, find-replace, split. Use when you can't get the desired result with <a href='Simple string functions.md'>simple string functions</a> easily.

This tool can help to create regular expressions: menu Code -> Regex. It's like another cookbook.

Although .NET has a regular expression engine (class <a href='https://www.google.com/search?q=System.Text.RegularExpressions.Regex+class'>Regex</a>), the automation library uses another engine - <a href='https://www.google.com/search?q=PCRE+regular+expression'>PCRE</a>. It's because some library functions can't use .NET. Both engines are equally powerful and use the same syntax with few unique features. Both can be used in scripts, but with <a href='/api/Au.elm.html'>elm</a> functions only PCRE.

The PCRE regular expression class is <a href='/api/Au.regexp.html'>regexp</a>. And there are string extension methods that use it internally.

- <a href='/api/Au.regexp.IsMatch.html'>regexp.IsMatch</a> and <a href='/api/Au.Types.ExtString.RxIsMatch.html'>string.RxIsMatch</a> - compare the string or find a substring.
- <a href='/api/Au.regexp.Match.html'>regexp.Match</a> and <a href='/api/Au.Types.ExtString.RxMatch.html'>string.RxMatch</a> - compare/find, and get match info.
- <a href='/api/Au.regexp.FindAll.html'>regexp.FindAll</a> and <a href='/api/Au.Types.ExtString.RxFindAll.html'>string.RxFindAll</a> - find/get all matches.
- <a href='/api/Au.regexp.Replace.html'>regexp.Replace</a> and <a href='/api/Au.Types.ExtString.RxReplace.html'>string.RxReplace</a> - find and replace all.
- <a href='/api/Au.regexp.Split.html'>regexp.Split</a> and <a href='/api/Au.Types.ExtString.RxSplit.html'>string.RxSplit</a> - split.

```csharp
var s = "one two22 three333 four";
```

Find. This example uses a <b>regexp</b> object.

```csharp
var x = new regexp(@"\b(\w+?)(\d+)\b");
if (x.IsMatch(s)) print.it("found");
```

This is the same with an extension method.

```csharp
if (s.RxIsMatch(@"\b(\w+?)(\d+)\b")) print.it("found");
```

Find and get match info. The m[1] and m[2] are substrings that match regex parts enclosed in ().

```csharp
if(s.RxMatch(@"\b(\w+?)(\d+)\b", out var m)) print.it(m.Start, m.End, m.Value, m[1].Value, m[2].Value);
```

Find in part of string.

```csharp
int from = 8;
if (s.RxMatch(@"[a-z]+", out var mm, range: from..)) print.it(mm);
```

Find all matches.

```csharp
foreach(var k in s.RxFindAll(@"\b(\w+?)(\d+)\b")) print.it(k.Value, k[1].Value, k[2].Value);
```

Find and replace all matches.

```csharp
var s2 = s.RxReplace(@"\b(\w+?)(\d+)\b", "'$2$1'");
print.it(s2);
```

Find and replace max 1 match.

```csharp
print.it(s.RxReplace(@"\b(\w+?)(\d+)\b", "'$2$1'", 1));
```

Find-replace and get the number of replacements.

```csharp
int n = s.RxReplace(@"\b(\w+?)(\d+)\b", "'$2$1'", out string s3);
print.it(n, s3);
```

Find-replace using a callback function.

```csharp
var s4 = s.RxReplace(@"\b([a-z])(\w+)", m => m[1].Value.Upper() + m[2].Value);
print.it(s4);

var s5 = s.RxReplace(@"\b(\w+?)(\d+)\b", m => m.ExpandReplacement("$1+$2").Upper());
print.it(s5);
```

Find-replace using a custom function in the replacement string.

```csharp
regexp.addReplaceFunc("Upper", (m, g, v) => m[g].Value.Upper());
regexp.addReplaceFunc("Add", (m, g, v) => (m[g].Value.ToInt() + v.ToInt()).ToString());

print.it(s.RxReplace(@"\b(\w+?)(\d+)\b", "${+Upper}"));
print.it(s.RxReplace(@"\b([a-z])(\w+)\b", "${+Upper(1)}$2")); //1 is group index
print.it(s.RxReplace(@"\d+", "${+Add(0, 2)}")); //0 is group index, 2 is any value
```

Split the string.

```csharp
s = "one, two,three , four";
var a = s.RxSplit(@" *, *");
for(int i = 0; i < a.Length; i++) print.it(i, a[i]);
```

There are to ways to insert an unknown string into a regular expression: 1. Enclose it in \Q \E. 2. Escape special characters in it.

```csharp
string c = clipboard.text;
if (s.RxIsMatch(@"...\Q" + c + @"\E...")) print.it("is match");
if (s.RxIsMatch(@"..." + Regex.Escape(c) + @"...")) print.it("is match");
```

