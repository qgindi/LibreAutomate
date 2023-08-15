/// Get mouse cursor position.

var p = mouse.xy;
print.it(p);

/// Is mouse left button pressed?

if (mouse.isPressed(MButtons.Left)) print.it("yes");

/// Wait for mouse left button down. <help mouse.waitForClick>More info<>.

mouse.waitForClick(0, MButtons.Left);

/// Wait for cursor (mouse pointer). <help mouse.waitForCursor>More info<>.

mouse.waitForCursor(0, MCursor.Hand); //standard
mouse.waitForCursor(0, -3191259760238497114); //custom

/// Get cursor hash for <b>waitForCursor</b>.

if(MouseCursor.GetCurrentVisibleCursor(out var c)) print.it(MouseCursor.Hash(c));

/// Also there are more <see cref="mouse"/> class functions.
