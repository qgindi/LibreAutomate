# Strings and characters
<a href='https://www.google.com/search?q=Strings+(C%23+Programming+Guide)'>Strings</a> contain text. In code the text is enclosed in "". It is a sequence of characters.

Create variable <i>s</i> of type <span style='color:#00f;font-weight:bold'>string</span>, and assign a string. Then get the third character.

```csharp
string s = "Abc, 12";
char c1 = s[2]; //character c
```

String text is read-only. String functions never modify it; they return a modified copy if need.

```csharp
//s[2] = 'd'; //error
string s2 = s.ReplaceAt(2, 1, "d"); //creates new string
print.it(s, s2);
```

For non-read-only text use char array instead.

```csharp
char[] a = s.ToCharArray();
a[2] = 'e';
s = new string(a); //assign new string to variable s; forget the old string
print.it(s);
```

Get character properties.

```csharp
char c2 = 'A';
print.it(char.IsLetter(c2), char.IsUpper(c2), char.GetUnicodeCategory(c2), c2.IsAsciiDigit());
```

Get uppercase or lowercase version.

```csharp
print.it(char.ToUpper(c1), char.ToLower(c2), s.Upper());
```

Enumerate characters in a string or array. Get character codes.

```csharp
for (int i = 0; i < s.Length; i++) {
	var c3 = s[i];
	print.it(c3, (ushort)c3);
}
```

Strings and characters are Unicode UTF-16. A character is 2 bytes, more than 65000 possible values. More characters exists, but they are rarely used; they are encoded as 2 surrogate char.
