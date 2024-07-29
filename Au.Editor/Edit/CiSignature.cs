using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.SignatureHelp;
using Microsoft.CodeAnalysis.CSharp.SignatureHelp;

//FUTURE: show for lambda parameters. Currently VS does not show too.

class CiSignature {
	CiPopupText _textPopup;
	_Data _data; //not null while the popup window is visible
	CancellationTokenSource _cancelTS;
	
	class _Data {
		public SignatureHelpItems r;
		public _Span span;
		public int iSelected, iUserSelected, iRoslynSelected;
		public SciCode sci;
		
		public bool IsSameSpan(_Span span2) {
			return span2.start == span.start && span2.fromEnd == span.fromEnd;
			//never mind: we don't check whether text before and after is still the same. Not that important.
		}
		
		public bool IsSameArglist(_Span span2, SignatureHelpItems r2) {
			if (!IsSameSpan(span2) || r2.Items.Count != r.Items.Count) return false;
			for (int i = 0; i < r.Items.Count; i++) {
				var hi1 = r.Items[i] as AbstractSignatureHelpProvider.SymbolKeySignatureHelpItem;
				var hi2 = r2.Items[i] as AbstractSignatureHelpProvider.SymbolKeySignatureHelpItem;
				Debug.Assert(!(hi1 == null || hi2 == null));
				if (hi1 == null || hi2 == null || hi2.Symbol != hi1.Symbol) return false;
			}
			return true;
		}
	}
	
	struct _Span {
		public int start, fromEnd;
		public _Span(int start, int fromEnd) { this.start = start; this.fromEnd = fromEnd; }
		public _Span(TextSpan span, string code) { this.start = span.Start; this.fromEnd = code.Length - span.End; }
	}
	
	public bool IsVisibleUI => _data != null;
	
	public void Cancel() {
		_cancelTS?.Cancel(); _cancelTS = null;
		_CancelUI();
	}
	
	void _CancelUI() {
		if (_data == null) return;
		foreach (var r in _data.sci.ETempRanges_Enum(this)) r.Remove();
		_data = null;
		_textPopup?.Hide();
	}
	
	public void SciPositionChanged(SciCode doc) {
		if (_afterCharAdded) { _afterCharAdded = false; return; }
		if (_data == null) return;
		_ShowSignature(doc, default);
	}
	bool _afterCharAdded;
	
	public void SciCharAdded(SciCode doc, char ch, bool methodCompletion = false) {
		switch (ch) { case '(' or '[' or '<' or ')' or ']' or '>' or ',': break; default: return; }
		_ShowSignature(doc, ch, methodCompletion);
		_afterCharAdded = true;
	}
	
	public void ShowSignature(SciCode doc) {
		_ShowSignature(doc, default);
	}
	
	async void _ShowSignature(SciCode doc, char ch, bool methodCompletion = false) {
		//using var p1 = perf.local();
		if (!CodeInfo.GetContextAndDocument(out var cd, -2) || cd.pos < 2) return; //returns false if position is in meta comments
		
		_cancelTS?.Cancel();
		var cancelTS = _cancelTS = new CancellationTokenSource();
		var cancelToken = cancelTS.Token;
#if DEBUG
		if (Debugger.IsAttached) { cancelToken = default; _cancelTS = null; }
#endif
		
		SyntaxNode root = null;
		//ISignatureHelpProvider provider = null;
		SignatureHelpItems r = null;
		try {
			//could be sync, quite fast, but then sometimes reenters (GetItemsAsync waits/dispatches) and sometimes hangs
			r = await Task.Run(async () => {
				//p1.Next();
				root = cd.syntaxRoot;
				//p1.Next('r');
				var providers = _SignatureHelpProviders;
				//print.it(providers);
				SignatureHelpItems r = null;
				var trigger = new SignatureHelpTriggerInfo(ch == default ? SignatureHelpTriggerReason.InvokeSignatureHelpCommand : SignatureHelpTriggerReason.TypeCharCommand, ch);
				foreach (var p in providers) {
					//print.it(p);
					var r2 = await p.GetItemsAsync(cd.document, cd.pos, trigger, MemberDisplayOptions.Default, cancelToken).ConfigureAwait(false);
					//never mind: GetItemsAsync may throw exception. Rare.
					
					if (cancelToken.IsCancellationRequested) { /*print.it("IsCancellationRequested");*/ return null; } //often
					if (r2 == null) continue;
					if (r == null || r2.ApplicableSpan.Start > r.ApplicableSpan.Start) {
						r = r2;
						//provider = p;
					}
					//Example: 'print.it(new Something())'.
					//	The first provider probably is for Write (invocation).
					//	Then the second is for Something (object creation).
					//	We need the innermost, in this case Something.
				}
				return r;
			});
		}
		catch (OperationCanceledException) { /*Debug_.Print("canceled");*/ return; } //never noticed
		catch (Exception e1) { Debug_.Print(e1); return; } //Roslyn bug: in certain code throws
		finally {
			cancelTS.Dispose();
			if (cancelTS == _cancelTS) _cancelTS = null;
		}
		//print.it(r, cancelToken.IsCancellationRequested);
		
		if (cancelToken.IsCancellationRequested) return;
		if (r == null) {
			_CancelUI();
			return;
		}
		Debug.Assert(doc == Panels.Editor.ActiveDoc); //when active doc changed, cancellation must be requested
		if (cd.pos != doc.aaaCurrentPos16 || (object)cd.code != doc.aaaText) return; //changed while awaiting
		//p1.Next('s');
		
		//print.it($"<><c orange>pos={cd.pos}, span={r.ApplicableSpan},    nItems={r.Items.Count},  argCount={r.ArgumentCount}, argIndex={r.ArgumentIndex}, argName={r.ArgumentName}, sel={r.SelectedItemIndex},    provider={provider}<>");
		
		//get span of the arglist. r.ApplicableSpan.Start is of the statement, not of the arglist. In chained methods it is the chain start.
		var fullSpan = r.ApplicableSpan;
		//CiUtil.HiliteRange(fullSpan); wait.doEvents(500);
		var start = fullSpan.Start;
		var tok = root.FindToken(cd.pos);
		if (tok.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken or SyntaxKind.LessThanToken) tok = tok.GetPreviousToken();
		var argNode = tok.Parent;
		while (argNode != null) {
			int i = argNode.SpanStart; if (i <= start) break;
			if (argNode is BaseArgumentListSyntax or AttributeArgumentListSyntax or TypeArgumentListSyntax) {
				start = i + 1;
				break;
			}
			//CiUtil.PrintNode(argNode);
			argNode = argNode.Parent;
		}
		var argSpan = new TextSpan(start, fullSpan.End - start);
		//CiUtil.PrintNode(argNode); CiUtil.HiliteRange(argSpan); //print.it(argSpan);
		
		var span = new _Span(argSpan, cd.code);
		int iSel = 0, iSel2 = 0;
		if (r.Items.Count > 1) {
			iSel2 = r.SelectedItemIndex ?? -1;
			if (_data?.IsSameArglist(span, r) ?? false) {
				iSel = _data.iUserSelected; //preserve user selection in same session
				if (iSel2 < 0) iSel2 = _data.iRoslynSelected; //on error use last good Roslyn selection in same session, like in VS
			} else iSel = -1;
		}
		
		_data = new _Data {
			r = r,
			span = span,
			iUserSelected = iSel,
			iRoslynSelected = iSel2,
			sci = doc,
		};
		
		if (iSel < 0) iSel = iSel2;
		if (iSel < 0) {
			//r.SelectedItemIndex is null when cannot resolve overloads, eg when arglist is partially typed. Example: wnd.find(1, );
			iSel = r.SelectedItemIndex ?? (r.ArgumentCount == 0 ? 0 : -1);
			if (iSel < 0) {
				for (int i = 0; i < r.Items.Count; i++) if (r.Items[i].Parameters.Length >= r.ArgumentCount) { iSel = i; break; }
				if (iSel < 0) {
					for (int i = 0; i < r.Items.Count; i++) if (r.Items[i].IsVariadic) { iSel = i; break; }
					if (iSel < 0) iSel = 0;
				}
			}
		}
		
		doc.ETempRanges_Add(this, argSpan.Start, argSpan.End, onLeave: () => {
			if (doc.ETempRanges_Enum(doc.aaaCurrentPos8, this, utf8: true).Any()) return;
			_CancelUI();
		}, SciCode.TempRangeFlags.NoDuplicate);
		
		var rect = RECT.Union(doc.EGetCaretRectFromPos(fullSpan.Start), doc.EGetCaretRectFromPos(cd.pos));
		var rclient = doc.AaWnd.ClientRect;
		rect.left = Math.Clamp(rect.left, 0, rect.right = Math.Clamp(rect.right, 0, rclient.right));
		doc.AaWnd.MapClientToScreen(ref rect);
		rect.Width += Dpi.Scale(200, doc.AaWnd);
		rect.left -= 6;
		
		_textPopup ??= new CiPopupText(CiPopupText.UsedBy.Signature, onHiddenOrDestroyed: (_, _) => _data = null) {
			OnLinkClick = (ph, e) => ph.Text = _FormatText(e.ToInt(1), userSelected: true)
		};
		_textPopup.Text = _FormatText(iSel, userSelected: false);
		
		if (!_textPopup.IsVisible) {
			CodeInfo.HideTextPopupAndTempWindows();
			if (CodeInfo._compl.IsVisibleUI) //without this does not show completions with selected enum when typed Function( when first parameter is enum
				CodeInfo._compl.Cancel();
			if (methodCompletion) CodeInfo._compl.ShowList(ch); //when autocompletion added (); may need to show enum list 
		}
		
		_textPopup.Show(Panels.Editor.ActiveDoc, rect, System.Windows.Controls.Dock.Bottom);
		
		//also show Keys/Regex tool?
		//CiUtil.PrintNode(node);
		if (argNode is ArgumentListSyntax or BracketedArgumentListSyntax && cd.code.Eq(cd.pos - 1, "\"\"")) {
			//print.it("string");
			var semo = cd.semanticModel;
			var token = root.FindToken(cd.pos);
			if (true == token.IsInString(cd.pos, cd.code, out var stringInfo)) {
				var stringFormat = CiUtil.GetParameterStringFormat(stringInfo.stringNode, semo, true);
				if (stringFormat != 0)
					CodeInfo._tools.ShowForStringParameter(stringFormat, cd, stringInfo, _textPopup.PopupWindow.Hwnd);
			}
		}
	}
	
	System.Windows.Documents.Section _FormatText(int iSel, bool userSelected) {
		_data.iSelected = iSel;
		if (userSelected) _data.iUserSelected = iSel;
		
		var r = _data.r;
		ISymbol currentItem = null;
		SignatureHelpParameter currentParameter = null;
		var x = new CiText();
		
		//print.clear();
		for (int i = 0; i < r.Items.Count; i++) {
			var sh = r.Items[i];
			if (sh is AbstractSignatureHelpProvider.SymbolKeySignatureHelpItem kk) {
				var sym = kk.Symbol;
				if (i == iSel) currentItem = sym;
				x.StartOverload(i == iSel, i);
#if false
				x.AppendTaggedParts(sh.PrefixDisplayParts); //works, but formats not as I like (too much garbage). Has bugs with tuples.
#else
				//if(nt != null) {
				//	print.it(1, nt.IsGenericType, nt.IsTupleType, nt.IsUnboundGenericType, nt.Arity, nt.CanBeReferencedByName);
				//	print.it(2, nt.IsAnonymousType, nt.IsDefinition, nt.IsImplicitlyDeclared, nt.Kind, nt.TypeKind);
				//	print.it(3, nt.MemberNames);
				//	print.it(4, nt.Name, nt.MetadataName, nt.OriginalDefinition, nt.TupleUnderlyingType);
				//	print.it("TypeParameters:");
				//	print.it(nt.TypeParameters);
				//	print.it("TypeArguments:");
				//	print.it(nt.TypeArguments);
				//	print.it("TupleElements:");
				//	try { var te = nt.TupleElements; if(!te.IsDefault) print.it(te); } catch(Exception e1) { print.it(e1.ToStringWithoutStack()); }
				//	print.it("---");
				//}
				
				int isTuple = 0; //1 ValueTuple<...>, 2 (...)
				var nt = sym as INamedTypeSymbol;
				if (nt != null && nt.IsTupleType) isTuple = nt.IsDefinition ? 1 : 2;
				
				if (isTuple == 1) x.Append("ValueTuple"); //AppendSymbolWithoutParameters formats incorrectly
				else if (isTuple == 0) x.AppendSymbolWithoutParameters(sym);
				string b1 = "(", b2 = ")";
				if (nt != null) {
					if (nt.IsGenericType && isTuple != 2) { b1 = "<"; b2 = ">"; }
				} else if (sym is IPropertySymbol) {
					b1 = "["; b2 = "]";
				}
				x.Append(b1);
#endif
				int iArg = r.ArgumentIndex, lastParam = sh.Parameters.Length - 1;
				int selParam = iArg <= lastParam ? iArg : (sh.IsVariadic ? lastParam : -1);
				if (!r.ArgumentName.NE()) {
					var pa = sh.Parameters;
					for (int pi = 0; pi < pa.Length; pi++) if (pa[pi].Name == r.ArgumentName) { selParam = pi; break; }
				}
				x.AppendParameters(sym, selParam, sh);
				//x.AppendParameters(sh, selParam); //works, but formats not as I like (too much garbage)
#if false
				x.AppendTaggedParts(sh.SuffixDisplayParts);
#else
				x.Append(b2);
#endif
				if (i == iSel && selParam >= 0) currentParameter = sh.Parameters[selParam];
				x.EndOverload(i == iSel);
			} else {
				Debug_.Print(sh);
			}
		}
		
		if (currentItem != null) {
			var tt = r.Items[iSel].DocumentationFactory?.Invoke(default);
			bool haveDoc = tt?.Any() ?? false;
			string helpUrl = CiUtil.GetSymbolHelpUrl(currentItem);
			string sourceUrl = CiGoTo.GetLinkData(currentItem);
			bool haveLinks = helpUrl != null || sourceUrl != null;
			if (haveDoc || haveLinks) {
				x.StartParagraph();
				if (haveDoc) x.AppendTaggedParts(tt, false);
				if (haveLinks) {
					if (haveDoc) x.Append(" ");
					x.AppendSymbolLinks(helpUrl, sourceUrl);
					x.Append(".");
				}
				x.EndParagraph();
			}
		}
		
		if (currentParameter != null && !currentParameter.Name.NE()) { //if tuple, Name is "" and then would be exception
			x.StartParagraph("parameter");
			x.Bold(currentParameter.Name); x.Append(":  ");
			var tt = currentParameter.DocumentationFactory?.Invoke(default);
			//rejected. Use inheritdoc. This would cover only part of cases.
			//if (!tt.Any()) { //if this parameter of this overload is undocumented, look for parameter with same name in other overloads
			//	for (int i = 0; i < r.Items.Count; i++) {
			//		if (i == iSel) continue;
			//		var p = r.Items[i].Parameters.FirstOrDefault(o => o.Name == currentParameter.Name);
			//		if (p != null) {
			//			tt = p.DocumentationFactory?.Invoke(default);
			//			if (tt.Any()) break;
			//		}
			//	}
			//}
			x.AppendTaggedParts(tt, false);
			x.EndParagraph();
		}
		
		return x.Result;
	}
	
	static List<ISignatureHelpProvider> _GetSignatureHelpProviders() {
		var a = new List<ISignatureHelpProvider>();
		var types = Assembly.GetAssembly(typeof(InvocationExpressionSignatureHelpProvider)).DefinedTypes;
		foreach (var t in types.Where(t =>
			t.Namespace == "Microsoft.CodeAnalysis.CSharp.SignatureHelp"
			&& t.IsDefined(typeof(ExportSignatureHelpProviderAttribute))
			//&& t.ImplementedInterfaces.Contains(typeof(ISignatureHelpProvider)) && !t.IsAbstract
			)) {
			//print.it(t);
			var c = t.GetConstructor(Type.EmptyTypes);
			Debug_.PrintIf(c == null, t.ToString());
			if (c == null) continue;
			var o = c.Invoke(null) as ISignatureHelpProvider; Debug.Assert(o != null); if (o == null) continue;
			a.Add(o);
		}
		return a;
	}
	
	List<ISignatureHelpProvider> _SignatureHelpProviders => _shp ??= _GetSignatureHelpProviders();
	List<ISignatureHelpProvider> _shp;
	
	public bool OnCmdKey(KKey key) {
		if (_data != null) {
			switch (key) {
			case KKey.Escape:
				Cancel();
				return true;
			case KKey.Down:
			case KKey.Up:
				int i = _data.iSelected, n = _data.r.Items.Count;
				if (key == KKey.Down) {
					if (++i >= n) i = 0;
				} else {
					if (--i < 0) i = n - 1;
				}
				if (i != _data.iSelected) _textPopup.Text = _FormatText(i, userSelected: true);
				return true;
			}
		}
		return false;
	}
}
