/// Run a PowerShell script and print the output. See <google>PowerShell.exe command line</google>.

string code1 = """
[console]::OutputEncoding = [System.Text.Encoding]::Unicode
Write-Host 'PowerShell'
""";
using var file1 = new TempFile(".ps1");
filesystem.saveText(file1, code1, encoding: Encoding.Unicode);
run.console("PowerShell.exe", $"-ExecutionPolicy Bypass -File \"{file1}\"", encoding: Encoding.Unicode);

/// Run a VBScript script and print the output. See <google>cscript.exe command line</google>. No Unicode output.

string code2 = """
Wscript.Echo "VBScript"
""";
using var file2 = new TempFile(".vbs");
filesystem.saveText(file2, code2, encoding: Encoding.Unicode);
run.console("Cscript.exe", $"/e:VBScript /nologo \"{file2}\"");

/// The same should work for JScript. Replace /e:VBScript with /e:JScript.
