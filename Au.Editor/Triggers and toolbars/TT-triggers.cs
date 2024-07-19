extern alias CAW;

using System.Windows;
using System.Windows.Controls;
using Au.Controls;
using Au.Tools;

using Microsoft.CodeAnalysis;
using CAW::Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Shared.Extensions;
using CAW::Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using CAW::Microsoft.CodeAnalysis.FindSymbols;

#if SCRIPT
namespace Script;
#endif

partial class TriggersAndToolbars {
	public static void NewTrigger() {
		static int _GetTriggerType() {
			var fn = App.Model.CurrentFile;
			if (fn != null && fn.IsClass) {
				int i = fn.Name.Eq(true, "Hotkey triggers.cs", "Autotext triggers.cs", "Mouse triggers.cs", "Window triggers.cs");
				if (i > 0 && fn.ItemPath.Eqi(@"\@Triggers and toolbars\Triggers\" + fn.Name)) return i;
			}
			return 0;
		}

		int iType = _GetTriggerType();
		var fn = App.Model.CurrentFile;
		bool canRunThisScript = fn != null && fn.IsScript && !fn.ItemPath.Eqi(@"\@Triggers and toolbars\Triggers and toolbars.cs");

		var w = new KDialogWindow { Title = "New trigger", ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize };
		var b = new wpfBuilder(w).WinSize(360);

		b.Add("Trigger", out ToolBar tb).Margin("LRT").Brush(SystemColors.ControlBrush);
		tb.HideGripAndOverflow();
		string[] ats = { "Hotkey", "Autotext", "Mouse", "Window" };
		var abc = new RadioButton[ats.Length];
		for (int i = 0; i < abc.Length; i++) {
			tb.Items.Add(abc[i] = new RadioButton { Content = ats[i], Width = 60, Margin = new(0, 0, 3, 0), BorderBrush = SystemColors.ActiveBorderBrush });
		}

		var bMore = new Button { Content = "...", Width = 24, BorderBrush = SystemColors.ActiveBorderBrush };
		bMore.Click += (_, _) => {
			var m = new popupMenu();
			m["Command line, shortcut, scheduler"] = o => { b.Window.Close(); Menus.TT.Script_triggers(); };
			m["Open file \"Other triggers\""] = o => { b.Window.Close(); Menus.TT.Other_triggers(); };
			m.Show(owner: b.Window);
		};
		tb.Items.Add(bMore);

		string[] aa = {
			"o => o.Replace(\"replacement\")",
			"o => o.Replace(\"\"\"multiline replacement\"\"\")",
			"\"replacement\"",
			"\"\"\"multiline replacement\"\"\"",
			"o => o.Menu(\"one\", \"two\", new(\"Label3\", \"three\"))",
			"o => { print.it(o); }",
			$"o => script.run(@\"{(canRunThisScript ? fn.ItemPath : "script.cs")}\")",
		};
		b.Row((-1, ..400)).Add("Action", out ListBox lbAction);
		ScrollViewer.SetHorizontalScrollBarVisibility(lbAction, ScrollBarVisibility.Disabled);
		b.R.xAddInfoBlockT(null); var tInfo = b.Last as TextBlock;
		b.R.AddOkCancel(out var bOK, out _, out _);
		b.End();
		if (iType > 0) abc[iType - 1].IsChecked = true;
		_SetTriggerType(iType, true);

		for (int i = 0; i < abc.Length; i++) {
			int tt = i + 1;
			abc[i].Checked += (o, _) => _SetTriggerType(tt, false);
		}

		void _SetTriggerType(int tt, bool startup) {
			if (!startup) {
				global::TriggersAndToolbars.Edit($@"Triggers\{ats[tt - 1]} triggers.cs");
				iType = tt;
			}
			bool enable = iType > 0;
			if (enable) {
				tInfo.Text = iType == 4
					? "Now in code click where to insert the new trigger.\nThen click OK, select window, OK, edit code if need.\nFinally click Run to [re]start the triggers script."
					: "Now in code click where to insert the new trigger.\nThen click OK, and edit the new code.\nTo set window scope can be used " + App.Settings.hotkeys.tool_quick + ".\nFinally click Run to [re]start the triggers script.";
				lbAction.ItemsSource = iType == 2 ? aa : aa.Skip(5);
				lbAction.SelectedIndex = 0;
			}
			tInfo.Visibility = enable ? Visibility.Visible : Visibility.Hidden;
			bOK.IsEnabled = enable;
		}

		if (!w.ShowAndWait(App.Wmain)) return;

		int ia = lbAction.SelectedIndex; if (iType != 2) ia += 5;
		var sAction = aa[ia];
		if (iType == 2) sAction = sAction.Replace("multiline replacement", "\r\n\r\n");
		string s = null;
		if (iType == 1) {
			s = """hk["%"]""";
		} else if (iType == 2) {
			s = sAction.Starts("\"") ? """tr["%"]""" : """tt["%"]""";
		} else if (iType == 3) {
			s = """Triggers.Mouse[TM%]""";
		} else if (iType == 4) {
			var d = new Dwnd(default, DwndFlags.ForTrigger, "Window trigger");
			if (!d.ShowAndWait()) return;
			s = $"Triggers.Window[TWEvent.%ActiveNew, {d.AaResultCode}]";
		}
		s = $"{s} = {sAction};";
		_CorrectTriggerPlace();
		InsertCode.Statements(s, ICSFlags.GoToPercent);
		CodeInfo.ShowSignature();
		if (iType == 3) CodeInfo.ShowCompletionList();
	}

	//rejected. It's in the quick capturing menu. The 'New trigger' dialog informs about it.
	//public static void TriggerScope() {
	//}

	static string _WndFindArgs(wnd w) {
		var f = new TUtil.WindowFindCodeFormatter();
		f.RecordWindowFields(w, 0, false);
		return TUtil.ArgsFromWndFindCode(f.Format());
	}

	/// <summary>
	/// Inserts code statement for a window trigger or scope.
	/// </summary>
	/// <param name="w"></param>
	/// <param name="action">0 trigger, 1 window scopw, 2 program scope.</param>
	public static void QuickWindowTrigger(wnd w, int action) {
		string s;
		if (action == 2) {
			s = $"Triggers.Of.Window(of: \"{w.ProgramName.Escape()}\");";
		} else {
			s = _WndFindArgs(w);
			if (action == 1) s = $"Triggers.Of.Window({s});";
			else s = $"Triggers.Window[TWEvent.ActiveNew, {s}] = o => {{ print.it(o); }};";
		}
		_CorrectTriggerPlace();
		InsertCode.Statements(s);
	}

	//Users may not know where to add triggers, or may forget to move the caret there. In such case this func moves the caret to a safe place if possible.
	static void _CorrectTriggerPlace() {
		if (!CodeInfo.GetContextAndDocument(out var cd, metaToo: true)) return;
		var semo = cd.semanticModel;

		var programSym = semo.Compilation.GlobalNamespace.GetTypeMembers("Program").FirstOrDefault(); if (programSym == null) return;
		var attrSym = programSym.GetTypeMembers("TriggersAttribute").FirstOrDefault(); if (attrSym == null) return;

		var m = semo.GetEnclosingSymbol<IMethodSymbol>(cd.pos, default);
		while (m != null && !m.IsOrdinaryMethod()) m = m.ContainingSymbol as IMethodSymbol;
		if (m != null && m.GetAttributes().Any(o => o.AttributeClass == attrSym)) return; //don't need to correct

		m = programSym.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(o => o.Locations[0].SourceTree == semo.SyntaxTree && o.GetAttributes().Any(o => o.AttributeClass == attrSym));
		if (m == null) return;

		if (m.Locations[0].FindNode(default) is not MethodDeclarationSyntax md) return;
		int pos;
		var last = md.Body.Statements.OfType<ExpressionStatementSyntax>().LastOrDefault(o => o.Expression is AssignmentExpressionSyntax aes && aes.Left is ElementAccessExpressionSyntax eae);
		if (last != null) pos = last.FullSpan.End;
		else if (md.Body.Statements.OfType<IfStatementSyntax>().FirstOrDefault() is IfStatementSyntax firstIf) pos = firstIf.FullSpan.Start;
		else pos = md.Body.CloseBraceToken.SpanStart;
		cd.sci.aaaGoToPos(true, pos);
	}
}
