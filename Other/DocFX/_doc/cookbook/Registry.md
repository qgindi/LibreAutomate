# Registry
Use class <a href='https://www.google.com/search?q=C%23+class+Registry'>Registry</a>.
See also <a href='Saving variables, settings.md'>saving variables</a>.

Set a string value.

```csharp
Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Au\Test", "A", "text");
```

Get a string value if exists.

```csharp
if (Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Au\Test", "A", null) is string s1) {
	print.it(s1);
}
```

Set an int (DWORD) value.

```csharp
int i1 = 100;
Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\Au\Test", "B", i1);
```

Get an int (DWORD) value if exists.

```csharp
if (Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Au\Test", "B", null) is int i2) {
	print.it(i2);
}
```

Delete a value if exists.

```csharp
using (var k1 = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Au\Test", writable: true)) k1.DeleteValue("A", throwOnMissingValue: false);
```

Enumerate subkeys.

```csharp
using (var k1 = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft")) {
	foreach (var s2 in k1.GetSubKeyNames()) {
		print.it(s2);
	}
}
```

