# Shutdown, restart, logoff, sleep, hibernate, lock computer, battery power
Use class <a href='/api/Au.computer.html'>computer</a>.

```csharp
computer.shutdown(); //shutdown
computer.shutdown(restart: true); //reboot
computer.logoff(); //logoff (sign out)
computer.sleep(); //sleep
computer.sleep(hibernate: true); //hibernate
computer.lockOrSwitchUser(); //lock
```

Or run <a href='https://www.google.com/search?q=shutdown.exe'>shutdown.exe</a>.

```csharp
run.console("shutdown.exe", "/s /t 0"); //shutdown
run.console("shutdown.exe", "/s /f /t 0"); //shutdown, force
run.console("shutdown.exe", "/r /t 0"); //reboot
run.console("shutdown.exe", "/r /f /t 0"); //reboot, force
run.console("shutdown.exe", "/l"); //log off
run.console("shutdown.exe", "/h"); //hibernate
```

On Windows 10 can use the Win+X menu.

```csharp
keys.send("Win+X ^u"); //show popup menu with shutdown options
```

Is the computer running on battery?

```csharp
if (computer.isOnBattery) print.it("on battery");
```

