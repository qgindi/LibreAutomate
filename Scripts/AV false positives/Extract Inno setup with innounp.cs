string innounp = folders.Downloads + "innounp.exe";
//string setup = @"C:\Temp\Au\AV\LibreAutomateSetup.exe";
string setup = folders.Editor + "LibreAutomateSetup.exe";
string dir = @"C:\Temp\Au\AV\unpack";
int r1 = run.console(innounp, $@"-x -m -d{dir} {setup}");
print.it(r1);
