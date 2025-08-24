using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

/// <summary>
/// <c>PasswordBox</c> box with a toggle button "Environment variable" that turns it into <c>TextBox</c>.
/// </summary>
class KPasswordBox : UserControl {
	DockPanel _dp;
	TextBox _tb;
	PasswordBox _pb;
	ToggleButton _toggle;
	
	public KPasswordBox() {
		Thickness pad = new(2, 1, 2, 2);
		_tb = new() { Padding = pad };
		_pb = new() { Padding = pad };
		_toggle = new() { Content = ImageUtil.LoadWpfImageElement("*Material.Variable #404040 @12"), ToolTip = "Environment variable" };
		//DockPanel.SetDock(_toggle, Dock.Left);
		_dp = new();
		_dp.Children.Add(_toggle);
		_dp.Children.Add(_pb);
		base.Content = _dp;
		
		_toggle.Checked += (_, _) => {
			_dp.Children.Remove(_pb);
			_dp.Children.Add(_tb);
			_tb.Text = _pb.Password;
			_tb.SelectAll();
			if (_toggle.IsFocused) _tb.Focus();
		};
		_toggle.Unchecked += (_, _) => {
			_dp.Children.Remove(_tb);
			_dp.Children.Add(_pb);
			_pb.Password = _tb.Text;
			_pb.SelectAll();
			if (_toggle.IsFocused) _pb.Focus();
		};
	}
	
	/// <summary>
	/// Gets or sets the state of the "Environment variable" toggle button and the type of the text field (<c>TextBox</c> if <c>true</c>, else <c>PasswordBox</c>). Default <c>false</c>.
	/// </summary>
	public bool IsEnvVar {
		get => _toggle.IsChecked == true;
		set { _toggle.IsChecked = value; }
	}
	
	/// <summary>
	/// Gets or sets the password or environment variable (if <see cref="IsEnvVar"/> <c>true</c>).
	/// </summary>
	public string Text {
		get => IsEnvVar ? _tb.Text : _pb.Password;
		set { if (IsEnvVar) _tb.Text = value; else _pb.Password = value; }
	}
	
	public TextBox TheTextBox => _tb;
	
	public PasswordBox ThePasswordBox => _pb;
}
