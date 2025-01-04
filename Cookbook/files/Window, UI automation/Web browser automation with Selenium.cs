/// <google>Selenium WebDriver<> can be used to automate pages in web browser. It has more features than <see cref="elm"/> (see <+recipe>Web automation<>). For example can wait for web page loaded, extract text, execute JavaScript. But not so easy to use. Weaker than <+recipe>Playwright<>.
/// 
/// Install NuGet packages <+nuget selenium\Selenium.Support>Selenium.Support<> and <+nuget selenium\Selenium.WebDriver.ChromeDriver>Selenium.WebDriver.ChromeDriver<>. Update them for each new Chrome version.

/// This script starts new Chrome instance and then calls various automation functions.

/*/ nuget selenium\Selenium.Support; nuget selenium\Selenium.WebDriver.ChromeDriver; /*/
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
var e = driver.FindElement(By.LinkText("Shared C# code"));
e.Click();

//Find a text input field and enter text. To find it use XPath copied from Chrome Developer Tools.
var e2 = driver.FindElement(By.XPath("//*[@id='search']/input[1]"));
e2.SendKeys("find");

//How to invoke mouse or keyboard actions.
Actions action = new Actions(driver);
action.ContextClick(e2).Perform();

//Wait. Then the script will close the web browser.
2.s();
dialog.show("Close web browser", x: ^1);

/// Also it's possible to connect to an existing Chrome instance, but with Selenium it rarely works well. Use Playwright instead.

/// More info on the internet.
