using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using ToolLand;

namespace LA;

/// <summary>
/// Dialog panel for <c>PathFilter</c> code.
/// </summary>
class DPPathFilter : UserControl {
	KTextBox _tInclude, _tExclude;
	KCheckBox _cFileFunc, _cDirFunc;
	
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
		
		b.StartStack(vertical: true);
		b.Add(out _cFileFunc, "FileFilter").Tooltip("Callback function  for files");
		b.Add(out _cDirFunc, "DirectoryFilter").Tooltip("Callback function for directories");
		b.End();
		
		b.End();
		//..
		
		//. right
		b.xAddControlHelpButton(typeof(PathFilter).ToString()).Align(y: VerticalAlignment.Bottom).Tooltip("Documentation for include/exclude and other filters");
		//..
		
		b.End();
	}
	
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
			
			bool hasMore = _cFileFunc.IsChecked == true || _cDirFunc.IsChecked == true;
			if (hasMore || (s.Length > 0 && (s[0] != '\"' || s[endOfFirstArg + 2] != '\"' || incl.Starts("@@") || excl.Starts("@@")))) {
				s.Insert(0, "new(").Append(')');
				if (hasMore) {
					s.Append(" { ");
					if (_cFileFunc.IsChecked == true) s.Append("FileFilter = e => true, ");
					if (_cDirFunc.IsChecked == true) s.Append("DirectoryFilter = e => true, ");
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
}
