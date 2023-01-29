# Convert Base64, hex, UTF-8, encoding, urlencode, htmlencode
To convert data format can be used class <a href='https://www.google.com/search?q=System.Convert+class'>Convert</a>.

Use Base64 format to store binary data in a short text string. Don't use Base64 in file names and URLs because it is case-sensitive and may contain character '/'.

```csharp
var b = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
var s = Convert.ToBase64String(b);
print.it(s, Convert.FromBase64String(s));
```

Use Hex format to store binary data in a case-insensitive text string. It can be used in file names and URLs.

```csharp
s = Convert.ToHexString(b);
print.it(s, Convert.FromHexString(s));
```

Another way to create a Hex string.

```csharp
print.it(BitConverter.ToString(b));
```

Convert C# string (UTF-16) to UTF-8.

```csharp
string utf16 = "Abc";
byte[] utf8 = Encoding.UTF8.GetBytes(utf16);
print.it("utf8", utf8);
```

Convert UTF-8 to C# string (UTF-16).

```csharp
print.it("utf16", Encoding.UTF8.GetString(utf8));
```

Use <a href='https://www.google.com/search?q=System.Net.WebUtility+class'>System.Net.WebUtility</a> functions to encode/decode URL parameters or HTML. See also <a href='/api/Au.internet.urlAppend.html'>internet.urlAppend</a>.

```csharp
var ue = System.Net.WebUtility.UrlEncode("one, two");
print.it(ue);

string p1 = "one, two", p2 = "three, four";
var url1 = internet.urlAppend("https://httpbin.org/get", "p1=" + p1, "p2=" + p2);
print.it(url1);
```

