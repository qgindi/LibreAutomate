/// <link https://playwright.dev/dotnet/docs/input>Playwright<> can be used to automate pages in web browser (click links, fill web forms, extract data, etc). It has more features than <see cref="elm"/> (see <+recipe>Web automation<>). For example can wait for web page loaded, extract text, execute JavaScript. It has a recorder.
/// 
/// Works with Chrome, Edge and other Chromium-based browsers. Partially Firefox. Does not work on Windows 7/8.
/// 
/// Install NuGet package <+nuget playwright\Microsoft.Playwright>Microsoft.Playwright<>.
/// 
/// Download <link https://www.libreautomate.com/forum/showthread.php?tid=7779>Playwrighter.zip<> and import in LibreAutomate. Not necessary, but with it you'll use Playwright in a more convenient way.
/// The examples below use helper classes from the imported files. The imported files contain more examples and info.
/// 
/// This script connects to an existing web browser instance, finds a page by URL, activates the tab, and clicks a link.
/// To enable connections, the browser must be started with certain command line. Next script shows how.
/// Also, for this example, the browser must contain a tab with the main page of the LA forum.

/*/ c Playwrighter.cs; /*/
using Microsoft.Playwright;

using var play = Playwrighter.Connect(out var page, "https://www.libreautomate.com/forum/*");
await page.GetByText("Shared C# code").ClickAsync();
print.it(page.Url);

/// This script launches a web browser so that <.x>Playwrighter.Connect<> can work with it.

/*/ c Playwrighter.cs; /*/

PlaywrightRunBrowser.Chrome();
//PlaywrightRunBrowser.Chrome("--profile-directory=\"Profile 1\"");
//PlaywrightRunBrowser.Edge();
//PlaywrightRunBrowser.Edge(port: 9224);

/// This script connects to an existing web browser instance, creates new tab, opens page, and clicks a link.
/// To enable connections, the browser must be started with certain command line. Previous script shows how.

/*/ c Playwrighter.cs; /*/
using Microsoft.Playwright;

using var play = Playwrighter.Connect(out var page);
await page.GotoAsync("https://www.libreautomate.com/");
await page.GetByRole(AriaRole.Link, new() { Name = "Cookbook" }).ClickAsync();

/// This script launches a web browser, opens a page, and clicks a link.

/*/ c Playwrighter.cs; /*/
using Microsoft.Playwright;

using var play = Playwrighter.Launch(out var page); //default browser
//using var play = Playwrighter.Launch(out var page, PWBrowser.BundledChrome); //bundled browser (installs if need)
await page.GotoAsync("https://www.libreautomate.com/forum/");
await page.GetByText("Shared C# code").ClickAsync();

dialog.show("Close the browser");

/// This script launches a web browser and opens a page. Saves browser session data like cookies and local storage. Next time uses the saved data.

/*/ c Playwrighter.cs; /*/
using Microsoft.Playwright;

var userDataDir = @"C:\Test\Playwright\UserData"; //edit this
//var userDataDir = folders.LocalAppData + @"Google\Chrome\User Data"; //your Chrome user data. Fails if Chrome is already running and using this directory.

using var play = Playwrighter.LaunchPersistentContext(out var page, userDataDir);
//using var play = Playwrighter.LaunchPersistentContext(out var page, userDataDir, options: new() { IgnoreDefaultArgs = ["--disable-extensions"] }); //with Chrome extensions

await page.GotoAsync("https://www.google.com/");

dialog.show("Close browser");

/// This script launches Chrome with Playwright Inspector and starts recording.
/// With <.c>--channel chrome<> it uses your normal Chrome. Without - bundled.
/// You can change or remove the URL.

_Run(@"codegen --channel chrome https://www.libreautomate.com/forum/");

//console: 0 hidden, 1 normal, 2 don't close
void _Run(string command, int console = 0) {
	var ps1 = folders.Workspace + @".nuget\playwright\playwright.ps1";
	run.it("pwsh.exe", $"-NoLogo {(console == 2 ? "-NoExit" : "")} {(console == 0 ? "-w Hidden" : "")} \"{ps1}\" {command}");
}

/// Playwright also can be used with apps that use the WebView2 control. See <link>https://www.libreautomate.com/forum/showthread.php?tid=7826&pid=38514#pid38514<>
