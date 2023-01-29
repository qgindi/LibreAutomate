# Run console program and get its output text
Function <a href='/api/Au.run.console.html'>run.console</a> executes a console program in invisible mode and gets text that would be displayed in the console window.

Print the output text when it exits.

```csharp
string v = "example";
int r1 = run.console(@"C:\Test\console1.exe", $@"/a ""{v}"" /etc");
```

Get and print the output text in real time.

```csharp
int r2 = run.console(s => print.it(s), @"C:\Test\console2.exe");
```

Get the output text when it exits.

```csharp
int r3 = run.console(out var text, @"C:\Test\console3.exe", encoding: Encoding.UTF8);
print.it(text);
```

If the output contains garbage text, need to specify an encoding, like in the above example. Many console programs use Encoding.UTF8 or Encoding.Unicode.
