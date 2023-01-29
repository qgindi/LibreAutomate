# return, goto (exit, end, stop, jump)
The <b><a href='https://www.google.com/search?q=jump+statements%2C+return%2C+C%23+reference'>return</a></b> statement is used to exit current function.

```csharp
Func1();
print.it(3);

void Func1() {
	print.it(1);
	if(keys.isShift) return;
	print.it(2);
}
```

If the function's return type is not <span style='color:#00f;font-weight:bold'>void</span>, the statement also returns a value.

```csharp
print.it(Add(2, 2));

int Add(int a, int b) {
	return a + b;
}
```

If <span style='color:#00f;font-weight:bold'>return</span> is directly in the script and not in a function, the script will exit.

```csharp
if(keys.isShift) return;
```

To exit the script from anywhere, can be used <a href='https://www.google.com/search?q=C%23+Environment.Exit'>Environment.Exit</a>. However it isn't a normal exit; for example <span style='color:#00f;font-weight:bold'>finally</span> blocks will not be executed. Consider to throw an exception instead.

```csharp
Func2();

void Func2() {
	print.it(1);
	if(keys.isShift) Environment.Exit(1);
	if(keys.isCtrl) throw new InvalidOperationException("Ctrl pressed");
	print.it(2);
}
```

The <span style='color:#00f;font-weight:bold'>goto</span> statement is used to jump to a label in current function.

```csharp
print.it(1);
if(keys.isShift) goto label1;
print.it(2);
label1:
print.it(3);

if (keys.isCtrl) {
	if(keys.isShift) goto g1;
	print.it(4);
	g1:; //need ; if the label is at the end of a { code block }
}
print.it(5);
```

