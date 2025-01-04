/// If key pressed or toggled.

if (keys.isCtrl) print.it("Ctrl");
if (keys.isPressed(KKey.Escape)) print.it("Esc");
if (keys.isCapsLock) print.it("CapsLock toggled");

var mod = keys.getMod(); //get states of all four modifier keys
if (mod.Has(KMod.Alt)) print.it("Alt");
