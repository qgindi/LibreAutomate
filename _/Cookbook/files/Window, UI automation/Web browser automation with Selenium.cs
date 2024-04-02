/// There are two ways to automate pages in web browser (click links, fill web forms, extract data, etc):
/// - UI element functions (<see cref="elm"/>). See recipe <+recipe>Web automation<>.
/// - <google>Selenium WebDriver<>. It has more features, for example wait for web page loaded, extract text, execute JavaScript. It can be faster and more reliable. But not so easy to use.
/// 
/// At first install NuGet packages <+nuget>Selenium.Support<> and <+nuget>Selenium.WebDriver.ChromeDriver<>. Update them for each new Chrome version.

/// This script starts new Chrome instance and then calls various automation functions.

/*/ nuget -\Selenium.Support; nuget -\Selenium.WebDriver.ChromeDriver; /*/
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;

//Set Chrome options.
ChromeOptions options = new();
//Enable and maybe edit this if want to use an existing profile. To get profile path, in Chrome open URL "chrome://version/".
//	Then before starting this script also may need to close existing Chrome instances that use this profile.
//options.AddArguments($"user-data-dir={folders.LocalAppData + @"Google\Chrome\User Data"}", "profile-directory=Profile 1");

//Start new Chrome instance.
var service = ChromeDriverService.CreateDefaultService(pathname.getDirectory(typeof(ChromeDriver).Assembly.Location));
service.HideCommandPromptWindow = true;
using var driver = new ChromeDriver(service, options);
driver.Manage().Window.Maximize();

//Open web page.
driver.Navigate().GoToUrl("https://www.libreautomate.com/forum/");

//Get page title, URL, HTML source. Extract all text.
print.it(driver.Title);
print.it(driver.Url);
//print.it(driver.PageSource);
var body = driver.FindElement(By.TagName("body"));
print.it(body.Text);

//Find and click a link. It opens new page and waits util it is loaded.
var e = driver.FindElement(By.LinkText("Resources"));
e.Click();

//Find a text input field and enter text. To find it use XPath copied from Chrome Developer Tools.
var e2 = driver.FindElement(By.XPath("//*[@id='search']/input[1]"));
e2.SendKeys("find");

//How to invoke mouse or keyboard actions.
Actions action = new Actions(driver);
action.ContextClick(e2).Perform();

//Wait. Then the script will close the web browser.
3.s();
dialog.show("Close web browser", x: ^1);

/// This script connects to an existing Chrome instance and opens a web page.

/*/ nuget -\Selenium.Support; nuget -\Selenium.WebDriver.ChromeDriver; /*/
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.Extensions;
using OpenQA.Selenium.Support.UI;

var w = wnd.find(0, "*- Google Chrome", "Chrome_WidgetWin_1");
using var driver = Selenium.ConnectToChrome(w);
driver.SwitchTo().NewWindow(WindowType.Tab);
driver.Navigate().GoToUrl("https://www.libreautomate.com");

/// <summary>
/// Misc helper functions for web browser automation using Selenium.
/// </summary>
static class Selenium {
	/// <summary>
	/// Connects to a Chrome process, which must be started with command line like <c>--remote-debugging-port=9222</c>.
	/// </summary>
	/// <exception cref="Exception">Failed.</exception>
	/// <example>
	/// <code><![CDATA[
	/// var w = wnd.find(0, "*- Google Chrome", "Chrome_WidgetWin_1");
	/// using var driver = Selenium.ConnectToChrome(w);
	/// driver.Navigate().GoToUrl("https://www.libreautomate.com");
	/// ]]></code>
	/// Starting Chrome.
	/// <code><![CDATA[
	/// run.it("chrome.exe", "--remote-debugging-port=9222");
	/// ]]></code>
	/// </example>
	public static ChromeDriver ConnectToChrome(wnd w) {
		var s = process.getCommandLine(w.ProcessId, true) ?? throw new AuException("Failed to get Chrome command line");
		var da = StringUtil.CommandLineToArray(s).FirstOrDefault(o => o.Starts("--remote-debugging-port=")) ?? throw new AuException("Chrome must be started with command line like --remote-debugging-port=9222");
		ChromeOptions options = new() { DebuggerAddress = "127.0.0.1:" + da[24..] };
		var service = ChromeDriverService.CreateDefaultService(pathname.getDirectory(typeof(ChromeDriver).Assembly.Location));
		service.HideCommandPromptWindow = true;
		return new ChromeDriver(service, options);
	}
}

/// This code can be used to run Chrome so that the above script can connect to it.

run.it("chrome.exe", "--remote-debugging-port=9222");

/// More info on the internet.
