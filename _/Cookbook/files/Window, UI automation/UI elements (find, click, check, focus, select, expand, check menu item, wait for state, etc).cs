/// To create code to find a UI element, use this tool: menu <b>Code > Find UI element<> (hotkey <mono>Ctrl+Shift+E<>). It also can create action code, for example to click the element.

//click button "Properties" in a folder window; then wait 1 s
var w1 = wnd.find(1, cn: "CabinetWClass").Activate();
var e1 = w1.Elm["SPLITBUTTON", "Properties", "class=NetUIHWND"].Find(1);
e1.Invoke();
//e1.MouseClick(); //use this when Invoke does not work
//e1.MouseClickD(); //or this (double click)
//e1.MouseClickR(); //or this (right click)
//e1.PostClick(); //or this
//e1.WebInvoke(); //use this with web page links when need to wait for new page
//e1.JavaInvoke(); //use this with Java windows when Invoke does not work well
1.s();

//check checkbox "Read-only"; then wait 1 s
var w2 = wnd.find(1, "* Properties", "#32770");
var e2 = w2.Elm["CHECKBOX", "Read-only"].Find(1);
e2.Check(true);
1.s();

//select tab "Details"
var w3 = wnd.find(1, "* Properties", "#32770").Activate();
var e3 = w3.Elm["PAGETAB", "Details"].Find(1);
e3.Focus(true);

//expand folder "System32" in a folder window. At first expands its ancestors.
var w4 = wnd.find(1, cn: "CabinetWClass").Activate();
var e4 = w4.Elm["TREEITEM", "This PC", "id=100"].Find(1);
e4.Expand("*C:*|Windows|System32");
//wait 2 s and collapse 3 levels
2.s();
keys.send("Left*6");

//select combo box item "Baltic"
var w5 = wnd.find(1, "Font", "#32770").Activate();
var e5 = w5.Elm["COMBOBOX", "Script:"].Find(1);
e5.ComboSelect("Baltic");

/// To select a menu item, need to find and click each intermediate menu item. However usually it's better to use hotkeys and <mono>Alt<>+keys.

//hotkey and Alt+keys
wnd.find(0, "*- Notepad", "Notepad").Activate();
keys.send("Ctrl+V");
keys.send("Alt+E P");

//the same with elm functions
var wNotepad = wnd.find(0, "*- Notepad", "Notepad").Activate();
var eEdit = wNotepad.Elm["MENUITEM", "Edit"].Find(0);
eEdit.Invoke();
var wMenu = wnd.find(1, "", "#32768", wNotepad);
var ePaste = wMenu.Elm["MENUITEM", "Paste\tCtrl+V"].Find(1);
ePaste.Invoke();

/// Use <b>elm<> functions to get menu item state (checked, disabled). This code checks menu <b>Format > Word Wrap<>. 

var wNotepad2 = wnd.find(0, "*- Notepad", "Notepad").Activate();
keys.send("Alt+O"); //Format
var wMenu2 = wnd.find(3, "", "#32768", wNotepad2);
var eWW = wMenu2.Elm["MENUITEM", "Word Wrap"].Find(1);
keys.send(eWW.IsChecked ? "Esc*2" : "W");

/// Wait until button <b>Apply<> isn't disabled.

var wProp = wnd.find(1, "* Properties", "#32770");
var eApply = wProp.Elm["BUTTON", "Apply"].Find(1);
eApply.WaitFor(0, e => !e.IsDisabled);
print.it("enabled");

/// To find child/descendant elements, use <see cref="elm.Elm"/>.

var wFolder = wnd.find(1, cn: "CabinetWClass").Activate();
var eList = wFolder.Elm["LIST", "Items View", "class=DirectUIHWND"].Find(1);
var eLi = eList.Elm["LISTITEM", "c"].Find();
print.it(eLi);
print.it("---");
var aLi = eList.Elm["LISTITEM"].FindAll();
print.it(aLi);
print.it("---");
var aAll = eList.Elm.FindAll();
print.it(aAll);

/// You can find more <b>elm<> functions in the popup list that appears when you type <.c>.<> (dot) after a variable name or <.c>elm<>.

var em = elm.fromMouse(); //popup list when typed "elm."
string role = em.Role; //popup list when typed "em."

/// In some cases to find UI elements it's better to use <see cref="elmFinder"/>. A single instance can be used to find elements in multiple windows etc.

/// Find window that contains button <b>Apply<> (UI element), and get the UI element too.

var f1 = new elmFinder("BUTTON", "Apply"); //or var f1 = elm.path["BUTTON", "Apply"];
var w10 = wnd.find(cn: "#32770", also: t => f1.In(t).Exists()); //or t => t.HasElm(f1)
print.it(w10);
print.it(f1.Result);

/// Print all UI elements in a window.

var w11 = wnd.find("LibreAutomate");
//print all
print.it(w11.Elm.FindAll());
//print only buttons
print.it(w11.Elm["BUTTON"].FindAll());
