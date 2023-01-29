# File read (load), write (save)
There are many ways to read and write files of various formats.
Class <a href='https://www.google.com/search?q=System.IO.File+class'>File</a> reads/writes simple text or raw binary data.

```csharp
string file = @"C:\Test\test.txt";
```

Read all text to a string.

```csharp
string text = File.ReadAllText(file);
print.it(text);
```

Read if file exists.

```csharp
string text2 = File.Exists(file) ? File.ReadAllText(file) : null;
```

Write all text from a string.

```csharp
File.WriteAllText(file, text); //creates new file or overwrites existing file
```

Read text lines.

```csharp
string[] lines = File.ReadAllLines(file);
foreach (var s in lines) {
	print.it(s);
}
```

Write text lines from List.

```csharp
var a = new List<string>();
for (int i = 1; i <= 10; i++) a.Add("line " + i);
File.WriteAllLines(file, a);
```

Usually text in files is Unicode, encoded as UTF-8 or UTF-16. The <b>ReadX</b> functions can read both. The <b>WriteX</b> functions write UTF-8 by default. Let's write UTF-16:

```csharp
File.WriteAllText(file, text, Encoding.Unicode);
```

If the file already exists, the <b>WriteX</b> functions overwrite (replace) it, else they create new file.
The <b>AppendX</b> functions append to existing file or create new file.

```csharp
File.AppendAllText(file, "line\r\n"); //append a string

File.AppendAllLines(file, a); //append a list or array
```

Read/write binary (non-text) data.

```csharp
byte[] b = File.ReadAllBytes(file);
File.WriteAllBytes(file, b);
```

All above functions immediately fail if the file is locked at that time (eg an app is writing the file). Also the write functions can corrupt the file in some cases. To read and write in a safer way, use <a href='/api/Au.filesystem.html'>filesystem</a> functions.

```csharp
text = filesystem.loadText(file); //waits max 2 s if locked
text = filesystem.waitIfLocked(() => File.ReadAllText(file), 10_000); //waits max 10 s if locked
```

See also <a href='/api/Au.filesystem.saveText.html'>filesystem.saveText</a> and similar functions. They write in a safer way and can create a backup file.
