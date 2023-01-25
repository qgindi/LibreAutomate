/// This code in file "Hotkey triggers" contains several triggers and function <b>_ShowTriggers</b> that shows them in a dialog box when you press Alt+?.

#region Alt
		hk["Alt+<"] = o => { keys.sendt("&lt;"); };
		hk["Alt+>"] = o => { keys.sendt("&gt;"); };
		hk["Alt+7"] = o => { keys.sendt("&amp;"); };
		hk["Alt+B"] = o => _MakeTag("b");
		hk["Alt+I"] = o => _MakeTag("i");
		hk["Alt+T"] = o => _HtmlTable();
#endregion
		hk["Alt+?"] = o => _ShowTriggers("Alt");

		static void _ShowTriggers(string region) {
			var s = _SourceCode();
			if (s.RxMatch($@"(?ms)^#region {region}\R(.+?)^#endregion\b", 1, out s)) dialog.show(null, s, "Close");
	
			static string _SourceCode([CallerFilePath] string f_ = null) => filesystem.loadText(f_);
		}
