using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;

namespace Au.More;

/// <summary>
/// This OCR engine uses <c>tesseract.exe</c> from Tesseract installed on this computer.
/// </summary>
/// <remarks>
/// Slower than <see cref="OcrWin10"/> (the default engine). The accuracy is poor.
/// Supports more languages. You choose what languages to install when you install Tesseract.
/// 
/// <see href="https://github.com/UB-Mannheim/tesseract/wiki">Download Tesseract</see>
/// </remarks>
public class OcrTesseract : IOcrEngine {
	/// <param name="tesseractPath">Full path of Tesseract folder. If <c>null</c>, uses path written in the registry by the installer.</param>
	/// <exception cref="FileNotFoundException"></exception>
	public OcrTesseract(string tesseractPath = null) {
		_tesseractPath = tesseractPath ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Tesseract-OCR", "Path", null) as string;
		var exe = _tesseractPath + @"\tesseract.exe";
		if (!filesystem.exists(exe).File) throw new FileNotFoundException("tesseract.exe not found. Download/install Tesseract, or set correct tesseractPath argument.");
		_tesseractExe = exe;
	}
	readonly string _tesseractPath, _tesseractExe;

	/// <summary>
	/// One or more of installed languages, like <c>"deu"</c> or <c>"eng+deu"</c>. If <c>null</c> (default), uses <c>"eng"</c>.
	/// </summary>
	public string Language { get; set; }

	/// <summary>
	/// Gets OCR languages that are installed on this computer and can be used for <see cref="Language"/>.
	/// </summary>
	public string[] AvailableLanguages {
		get {
			var a = new List<string>();
			run.console(s => { if (!s.NE() && !s.Ends(':') && !s.Eqi("osd")) a.Add(s); }, _tesseractExe, "--list-langs");
			return a.ToArray();

			//note: can be in subfolders, like "test/deu".
		}
	}

	///// <summary>
	///// Adds command line <c>--psm</c> (page segmentation mode).
	///// Valid values are 0-13, but documented only 3 (default) and 6 (no segmentation).
	///// </summary>
	//public int Psm { get; set; } = 3;

	/// <summary>
	/// Additional command line arguments.
	/// </summary>
	public string CommandLine { get; set; }

	/// <inheritdoc cref="IOcrEngine.DpiScale"/>
	public bool DpiScale { get; set; } = true;

	/// <inheritdoc cref="IOcrEngine.Recognize"/>
	public OcrWord[] Recognize(Bitmap b, bool dispose, double scale) {
		var b0 = b;
		b = IOcrEngine.PrepareBitmap(b, dispose, scale);

		using var temp = new TempFile(".png");
		b.Save(temp, ImageFormat.Png);
		if (dispose || b != b0) b.Dispose();

		var cl = $"\"{temp}\" stdout";
		if (!Language.NE()) cl += $" -l {Language}";
		//if (Psm != 3) cl += $" --psm {Psm}";
		if (!CommandLine.NE()) cl += $" {CommandLine}";
		cl += " quiet tsv"; //must be at the end, else all -x parameters ignored

		if (0 != run.console(out string result, _tesseractExe, cl, encoding: Encoding.UTF8)) throw new AuException(result);

		//print.it(result);

		regexp rx = new(@"^(?:\d+\t){6}(\d+)\t(\d+)\t(\d+)\t(\d+)\t\S+\t(\S.*)$");
		List<OcrWord> a = new();
		string sep = null;
		RECT pr = default;
		foreach (var s in result.Lines()) {
			switch (s[0]) {
			case '4': //line
				if (sep != null) sep = "\r\n";
				break;
			case '5': //word
				if (rx.Match(s, out var m)) { //else text is space
					RECT r = new(m[1].Value.ToInt(), m[2].Value.ToInt(), m[3].Value.ToInt(), m[4].Value.ToInt());

					//break line if big space between words
					if (sep == " " && r.left - pr.right > pr.Height + r.Height) sep = "\r\n";
					pr = r;

					a.Add(new(sep, m[5].Value, r, scale));
					sep = " ";
				}
				break;
			}
		}

		return a.ToArray();
	}
}
