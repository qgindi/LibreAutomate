using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using ToolLand;

namespace LA;

/// <summary>
/// Dialog panel for <c>PathFilter</c> code.
/// </summary>
class DPPathFilter : UserControl {
	KTextBox _tInclude, _tExclude, _tMaxDepth;
	KCheckBox _cShouldInclude, _cShouldRecurse;
	FileAttributes _attributesToSkip;
	
	public DPPathFilter() {
		var b = new wpfBuilder(this).Columns(-1, 0, 0);
		Background = SystemColors.ControlBrush; //no tooltip etc without a brush
		
		//. left
		b.R.StartGrid();
		b.R.Add("Include", out _tInclude).Tooltip("(optional) Glob pattern for files/folders to include.\nCan be multiple, like pattern1|pattern2|pattern3.");
		b.R.Add("Exclude", out _tExclude).Tooltip("(optional) Glob pattern for files/folders to exclude.\nCan be multiple, like pattern1|pattern2|pattern3.");
		b.End();
		//..
		
		//. middle
		b.StartStack<Expander>(out var exp1, "", vertical: true);
		exp1.ExpandDirection = ExpandDirection.Left;
		
		b.StartStack();
		b.Add(out _cShouldInclude, "IncludeFunc").Tooltip("Callback function that decides what to include");
		b.Add(out _cShouldRecurse, "RecurseFunc").Tooltip("Callback function that decides which folders to enumerate when enumerating all descendants");
		b.End();
		
		b.StartGrid().Columns(0, 50, -1, 30);
		b.R.Add("Max depth", out _tMaxDepth).Skip(1);
		b.AddButton("@", o => {
			var m = new popupMenu();
			var e = new EnumUI<FileAttributes>(o => !(o.Value is FileAttributes.Directory or FileAttributes.Device));
			e.AddTo(m, _attributesToSkip);
			var r = o.Button.RectInScreen();
			m.Show(PMFlags.AlignRectBottomTop, excludeRect: r, owner: o.Window);
			_attributesToSkip = e.Result;
		}).Tooltip("Attributes to skip");
		b.End();
		
		b.End();
		//..
		
		//. right
		b.xAddControlHelpButton(typeof(PathFilter).ToString()).Align(y: VerticalAlignment.Bottom).Tooltip("Documentation for include/exclude and other filters");
		//..
		
		b.End();
		
		//bool loadedOnce = false;
		//this.Loaded += (_, e) => {
		//	if (!loadedOnce) {
		//		loadedOnce = true;
		//	}
		//};
	}
	
	//static DPPathFilter() {
	//	TUtil.OnAnyCheckTextBoxValueChanged<DPPathFilter>((d, o) => d._AnyCheckTextBoxValueChanged(o));
	//}
	
	
	#region Result
	
	/// <summary>
	/// <i>filter</i> argument code string. Or <c>null</c> if not specified.
	/// </summary>
	public string AaResultCode {
		get {
			StringBuilder s = new();
			string incl = _tInclude.Text, excl = _tExclude.Text;
			int endOfFirstArg = 0;
			if (!(incl.NE() && excl.NE())) {
				s.AppendStringArg(_tInclude.Text);
				endOfFirstArg = s.Length;
				s.AppendStringArg(_tExclude.Text);
			}
			
			bool hasMore = _cShouldInclude.IsChecked == true || _cShouldRecurse.IsChecked == true || _attributesToSkip != 0 || !_tMaxDepth.Text.NE();
			if (hasMore || (s.Length > 0 && (s[0] != '\"' || s[endOfFirstArg + 2] != '\"' || incl.Starts("@@") || excl.Starts("@@")))) {
				s.Insert(0, "new(").Append(')');
				if (hasMore) {
					s.Append(" { ");
					if (_cShouldInclude.IsChecked == true) s.Append("IncludeFunc = (ref o) => true, ");
					if (_cShouldRecurse.IsChecked == true) s.Append("RecurseFunc = (ref o) => true, ");
					if (_attributesToSkip != 0) s.AppendFormat("AttributesToSkip = {0}, ", TUtil.FormatFlags(_attributesToSkip));
					if (!_tMaxDepth.Text.NE()) s.AppendFormat("MaxRecursionDepth = {0}, ", _tMaxDepth);
					s[s.Length - 2] = ' '; s[s.Length - 1] = '}';
				}
			} else if (s.Length > 0) {
				if (incl.NE()) s.Remove(0, 4).Insert(1, "||");
				else if (excl.NE()) s.Remove(s.Length - 4, 4);
				else s.Remove(--endOfFirstArg, 4).Insert(endOfFirstArg, "||");
			} else {
				return null;
			}
			
			return s.ToString();
		}
	}
	
	#endregion
	
}
