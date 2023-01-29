# Windows OS version, computer and user name
Use class <a href='/api/Au.osVersion.html'>osVersion</a>.

```csharp
if (osVersion.minWin10) print.it("Windows 10 or later");
else if (osVersion.minWin8) print.it("Windows 8");
else print.it("Windows 7");
```

Get computer name and current user name.

```csharp
print.it(Environment.MachineName, Environment.UserName);
```

