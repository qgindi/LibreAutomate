/// To automate web page elements in Chrome, Firefox and other web browser windows, can be used UI element functions and tools. See recipe <+recipe>UI elements<>.

var w = wnd.find(1, "*- Google Chrome", "Chrome_WidgetWin_1").Activate();

//find and click link "Search"
var e = w.Elm["web:LINK", "Search"].Find(1);
//e.Invoke(); //clicks the link
e.WebInvoke(); //clicks and waits until the window name changes

//find and click radiobutton with label "search titles only"; it does not have a name etc, therefore let's find an adjacent element (STATICTEXT) and navigate
e = w.Elm["web:STATICTEXT", "search titles only", navig: "pr"].Find(1);
e.Invoke();

//assume then need to enter text in a text input field near the radiobutton.
//	Could find it like in the above examples, but let's try alternative ways.
if (true) { //use Navigate
	e = e.Navigate("pa2 pr fi2"); //to create the string, in the "Find UI element" tool select the first element, then right-click the final element and click "Navigate". Then copy-paste.
	e.SendKeys("Ctrl+A", "!web automation");
} else { //use Tab key. In this case it is the best way.
	e.SendKeys("Shift+Tab Ctrl+A", "!web automation"); //SendKeys makes the radiobutton focused and send Shift+Tab which makes the text field focused
}

//select a combo box item
e = w.Elm["web:COMBOBOX", prop: "@name=postdate"].Find(1);
e.ComboSelect("*3 months*");

/// Get element HTML and attributes.

var ec = w.Elm["web:CHECKBOX", prop: "@name=matchusername"].Find(1);
var html = ec.Html(outer: true);
print.it(html);
var a = ec.HtmlAttributes();
foreach (var v in a) {
	print.it(v.Key, v.Value);
}
string attrValue = ec.HtmlAttribute("checked");
print.it(attrValue);

/// Get web page name, address (URL) and body HTML.

var ep = w.Elm["web:DOCUMENT"].Find(1);
var name = ep.Name;
var url = ep.Value;
print.it(name, url);
var body = ep.Html(outer: true);
print.it(body);

/// Open web page in default web browser.

run.it("https://www.example.com/");

/// There is no function "wait until the web page is fully loaded". Instead let the script search for an element that should be in the new page; make the timeout longer if need (by default the tool creates code with 1 s timeout). Also use function <b>WebInvoke</b> when appropriate.

var w1 = wnd.find(10, "*- Google Chrome", "Chrome_WidgetWin_1"); //find web browser window; wait max 10 s
var e1 = w1.Elm["web:LINK", "Search"].Find(10); //find a link; wait max 10 s
e1.WebInvoke(); //click the link and wait until window name changes
var e2 = w.Elm["web:TEXT", prop: "@name=keywords"].Find(10); //wait max 10 s for an element in the new web page

/// See also <+recipe>Selenium<>.
