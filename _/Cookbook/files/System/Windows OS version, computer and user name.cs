/// Use class <see cref="osVersion"/>.

if (osVersion.minWin10) print.it("Windows 10 or later");
else if (osVersion.minWin8) print.it("Windows 8");
else print.it("Windows 7");

/// Get computer name and current user name.

print.it(Environment.MachineName, Environment.UserName);
