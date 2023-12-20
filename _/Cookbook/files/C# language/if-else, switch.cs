/// Examples of the <.k>if<> statement. <+lang selection statements, "if">More info<>.

int i = 1; //variable i

if (i == 1) {
	print.it("i is 1");
	//here can be more statements
}

if (i != 0) print.it("i is not 0"); //don't need { } for single statement

/// Inside ( ) can be any expression of type <.k>bool<> (<.k>true<> or <.k>false<>).

bool ctrl = keys.isCtrl;
if (ctrl) print.it("Ctrl pressed");

if (!dialog.showOkCancel("Continue?")) return; //the function returns a bool value. The ! is operator NOT.

if (i > 0) {
	print.it("i > 0");
} else {
	print.it("i <= 0");
}

if (i > 0) print.it("i > 0"); else print.it("i <= 0");

if (i > 0) print.it("i > 0");
else if (i < 0) print.it("i < 0");
else print.it("i == 0");

if (i is 8 or 9) print.it("i is 8 or 9");

/// The <.k>switch<> statement can be used instead of multiple <.k>if<>.

switch (i) {
case 1:
	print.it("i is 1");
	break;
case 2 or 3:
	print.it("i is 2 or 3");
	break;
case >= 10 and < 20:
	print.it("i is >= 10 and < 20");
	break;
case 100 when keys.isShift:
	print.it("i is 100, and Shift is pressed");
	break;
case 1000: goto case 1;
case 7:
case 8:
	print.it("same as case 7 or 8");
	break;
//case 9: print.it("error, contains statements and no break");
//case variable: break; //error, must be a constant value
default:
	print.it("none");
	break;
}

string s = "green"; //variable s
switch (s) {
case "black":
	print.it("black");
	break;
case "green":
	print.it("green");
	break;
}

/// This is a <.k>switch<> expression.

i = s switch { "blue" => 1, "green" => 2, _ => 0 }; //select 1 if s is "blue", 2 if "green", else 0

int r = i switch { 1 => 10, 2 or 3 => 20, >= 4 and < 7 => 30, _ => 0 };

/// The conditional operator <.k>?:<>.

i = s == "blue" ? 1 : 0; //if s is "blue", select 1, else 0
i = s == "blue" ? 1 : (s == "green" ? 2 : 0); //same as the first switch expression example
