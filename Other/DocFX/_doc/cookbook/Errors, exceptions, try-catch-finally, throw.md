# Errors, exceptions, try-catch-finally, throw
A script with errors can't run. Errors must be fixed.

```csharp
print.it(1) //error, no semicolon
print.it(u); //error, u does not exist
```

Exceptions occur when something fails at run time. Then the script ends or jumps to a <span style='color:#00f;font-weight:bold'>catch</span> part of the nearest <span style='color:#00f;font-weight:bold'>try-catch</span> statement that embraces that code. <a href='https://www.google.com/search?q=try+catch%2C+C%23+reference'>More info</a>.

```csharp
print.it(1);
try {
	var w = wnd.find(0, "* - Notepadd");
	print.it(2);
}
catch {
	print.it("exception");
	return; //remove this, and the script will continue at print.it(3);
	//throw; //add this instead of return, and the script will end like without try/catch
	//throw new InvalidOperationException("message"); //or throw another exception
}
print.it(3);
```

Can get exception info.

```csharp
try { run.it("notepad11.exe"); }
catch (Exception e) { print.it(e); }
print.it("continue");
```

Can differently handle exceptions of different types. Can use <span style='color:#00f;font-weight:bold'>when</span>.

```csharp
string text = null;
try { text = File.ReadAllText(@"D:\file45678"); }
catch (FileNotFoundException) { } //continue
catch (Exception e) when (e is not ArgumentException) { //handle all other exceptions except ArgumentException
	print.it(e.ToStringWithoutStack());
	return;
}
print.it(text ?? "file not found");
```

Can use <span style='color:#00f;font-weight:bold'>try-finally</span> and <span style='color:#00f;font-weight:bold'>try-catch-finally</span>. <a href='https://www.google.com/search?q=try+finally%2C+C%23+reference'>More info</a>. The <span style='color:#00f;font-weight:bold'>finally</span> code runs when the <span style='color:#00f;font-weight:bold'>try</span> code is leaved in whatever way (finished, exception, <span style='color:#00f;font-weight:bold'>return</span>, <span style='color:#00f;font-weight:bold'>goto</span>, etc).

```csharp
print.it(1);
try {
	run.it("notepad22.exe");
	print.it(2);
	if (keys.isCapsLock) return;
}
//catch { print.it("exception"); } //optional. Try to uncomment this and observe the difference.
finally { print.it("finally"); }
print.it(3);
```

Note: The <span style='color:#00f;font-weight:bold'>finally</span> code does not run if <a href='https://www.google.com/search?q=C%23+Environment.Exit'>Environment.Exit</a> or some other function terminates the process.

With disposable objects instead of try-finally-Dispose can be used the <b><a href='https://www.google.com/search?q=using+statement%2C+C%23+reference'>using</a></b> statement.

To throw exceptions use the <b><a href='https://www.google.com/search?q=throw+statement%2C+C%23+reference'>throw</a></b> statement.

```csharp
Func1(1);
Func1(-1);

void Func1(int i) {
	if (i < 0) throw new ArgumentException("Can't be negative", nameof(i));
	if (keys.isCapsLock) throw new InvalidOperationException("CapsLock");
	print.it(i);
}
```

Two ways to insert <span style='color:#00f;font-weight:bold'>try</span> code quickly:
- Type tryc and select tryCatchFinallySnippet.
- Click or select code, and use menu Edit -> Surround.
