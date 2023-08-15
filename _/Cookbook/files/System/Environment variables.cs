/// Environment variables is a feature of the operating system and not of the C# language. They are similar to static string variables. Use functions like <see cref="Environment.GetEnvironmentVariable(string)"/>. Some other functions support environment variables in file path string, like "%variable%...". Each process has a copy of environment variables of the caller process or of the system, and can modify the copy (add, remove, change values), but usually does not modify the system environment variables.

var ev = Environment.GetEnvironmentVariable("TEMP");
print.it(ev);

Environment.SetEnvironmentVariable("name", "VALUE"); //adds of changes an environment variable in this process only

run.it(@"%SystemRoot%\Media");
