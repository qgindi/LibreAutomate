/// To create code to OCR-extract window text and find/click a word, use this tool: menu <b>Code > Find OCR text<>.
/// Usually OCR is the slowest way to find and click an UI element. And OCR results often are not accurate. Use it only if other ways don't work (keyboard, mouse, window controls, UI elements, UI image).

/// Find and click word <i>Downloads<> in the left side of a folder window.

var w = wnd.find(1, null, "CabinetWClass");
var c = w.Child(1, cn: "SysTreeView32"); // "Navigation Pane"
var t = ocr.find(1, c, "Downloads");
t.MouseClick();

/// Extract window text using OCR. Print text and each word text + rectangle.

print.clear();
//ocr.engine = new OcrGoogleCloud(Environment.GetEnvironmentVariable("API_KEY_GOOGLE")); //better results, but slower
var w2 = wnd.find(1, null, "CabinetWClass");
var t2 = ocr.recognize(w2);
print.it("-- Text --");
print.it(t2.Text);
print.it("-- Words --");
foreach (var word in t2.Words) {
	print.it($"{word.Text,-20}  {word.Rect}");
}
print.scrollToTop();

/// Extract text from user-selected rectangle.

if (!CaptureScreen.ImageColorRectUI(out var captured, CIUFlags.Image)) return;
using var b = captured.image;
var t3 = ocr.recognize(b);
print.it(t3.Text);
