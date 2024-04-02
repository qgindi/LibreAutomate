using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Linq;

//SHOULDDO: a workaround for WPF bug: sometimes window or part of window is white, until invalidating.
//	Noticed with all kinds of WPF windows and popups, not only created with wpfBuilder. Noticed in other WPF apps too, usually black parts.
//	Maybe set timer that invalidates window. Tested InvalidateRect, works.
//	Can reproduce in ~50% times: hide main window and press Ctrl+Shift+W. The wnd tool first time often white.
//	It seems OK when using Nvidia graphic card for all apps. Select in Nvidia control panel -> Manage 3D settings.
//	Tested on old computer. Never noticed on new.
//	Maybe never mind, it's just a low-quality graphic card. Had other problems with it too, eg video rendering in web browser.

//never mind: on Win7 text of all WPF checkboxes too low by 1 pixel. Not only of wpfBuilder.

namespace Au;

/// <summary>
/// With this class you can create windows with controls, for example for data input.
/// </summary>
/// <remarks>
/// This class uses WPF (Windows Presentation Foundation). Creates window at run time. No designer. No WPF and XAML knowledge required, unless you want something advanced.
/// 
/// To start, use snippet <b>wpfSnippet</b> or menu <b>File > New > Dialogs</b>. Also look in Cookbook.
/// 
/// Most functions return <c>this</c>, to enable method chaining, aka fluent interface, like with <b>StringBuilder</b>. See example.
/// 
/// A <b>wpfBuilder</b> object can be used to create whole window or some window part, for example a tab page.
/// 
/// The size/position unit in WPF is about 1/96 inch, regardless of screen DPI. For example, if DPI is 96 (100%), 1 unit = 1 physical pixel; if 150% - 1.5 pixel; if 200% - 2 pixels. WPF windows are DPI-scaled automatically when need. Your program's manifest should contain <c>dpiAware=true/PM</c> and <c>dpiAwareness=PerMonitorV2</c>; it is default for scripts/programs created with the script editor of this library.
/// 
/// Note: WPF starts slowly and uses much memory. It is normal if to show the first window in process takes 500-1000 ms and the process uses 30 MB of memory, whereas WinForms takes 250 ms / 10 MB and native takes 50 ms / 2 MB. However WinForms becomes slower than WPF if there are more than 100 controls in window. This library uses WPF because it is the most powerful and works well with high DPI screens.
/// 
/// WPF has many control types, for example <see cref="Button"/>, <see cref="CheckBox"/>, <see cref="TextBox"/>, <see cref="ComboBox"/>, <see cref="Label"/>. Most are in namespaces <b>System.Windows.Controls</b> and <b>System.Windows.Controls.Primitives</b>. Also on the internet you can find many libraries containing WPF controls and themes. For example, search for <i>github awesome dotnet C#</i>. Many libraries are open-source, and most can be found in GitHub (source, info and sometimes compiled files). Compiled files usually can be found in <see href="https://www.nuget.org/"/> as packages. Use menu <b>Tools > NuGet</b>.
/// 
/// By default don't need XAML. When need, you can load XAML strings and files with <see cref="System.Windows.Markup.XamlReader"/>.
/// </remarks>
/// <example>
/// Dialog window with several controls for data input.
/// <code><![CDATA[
/// var b = new wpfBuilder("Example").WinSize(400); //create Window object with Grid control; set window width 400
/// b.R.Add("Text", out TextBox text1).Focus(); //add label and text box control in first row
/// b.R.Add("Combo", out ComboBox combo1).Items("One|Two|Three"); //in second row add label and combo box control with items
/// b.R.Add(out CheckBox c1, "Check"); //in third row add check box control
/// b.R.AddOkCancel(); //finally add standard OK and Cancel buttons
/// b.End();
/// if (!b.ShowDialog()) return; //show the dialog and wait until closed; return if closed not with OK button
/// print.it(text1.Text, combo1.SelectedIndex, c1.IsChecked == true); //get user input from control variables
/// ]]></code>
/// </example>
public class wpfBuilder {
	//readonly FrameworkElement _container; //now used only in ctor
	readonly Window _window; //= _container or null
	_PanelBase _p; //current grid/stack/dock/canvas panel, either root or nested
	
	abstract class _PanelBase {
		protected readonly wpfBuilder _b;
		public readonly _PanelBase parent;
		public readonly Panel panel;
		FrameworkElement _lastAdded, _lastAdded2;
		public bool ended;
		
		protected _PanelBase(wpfBuilder b, Panel p) {
			_b = b;
			parent = b._p;
			_lastAdded = panel = p;
		}
		
		public virtual void BeforeAdd(WBAdd flags = 0) {
			if (ended) throw new InvalidOperationException("Cannot add after End()");
			if (flags.Has(WBAdd.ChildOfLast) && _lastAdded == panel) throw new ArgumentException("Last element is panel.", "flag ChildOfLast");
		}
		
		public virtual void Add(FrameworkElement c) {
			SetLastAdded(c);
			panel.Children.Add(c);
		}
		
		public virtual void End() { ended = true; }
		
		public FrameworkElement LastAdded => _lastAdded;
		
		public FrameworkElement LastAdded2 => _lastAdded2;
		
		public void SetLastAdded(FrameworkElement e) {
			if (_lastAdded != panel) _lastAdded2 = _lastAdded;
			_lastAdded = e;
		}
		
		public FrameworkElement LastDirect {
			get {
				if (_lastAdded == panel) {
					Debug_.Print("lastAdded == panel");
					return null;
				}
				for (var c = _lastAdded; ;) {
					var pa = c.Parent as FrameworkElement;
					if (pa == panel) return c;
					c = pa;
				}
			}
		}
	}
	
	class _Canvas : _PanelBase {
		public _Canvas(wpfBuilder b) : base(b, new Canvas()) {
			panel.HorizontalAlignment = HorizontalAlignment.Left;
			panel.VerticalAlignment = VerticalAlignment.Top;
		}
	}
	
	class _DockPanel : _PanelBase {
		public _DockPanel(wpfBuilder b) : base(b, new DockPanel()) {
		}
	}
	
	class _StackPanel : _PanelBase {
		public _StackPanel(wpfBuilder b, bool vertical) : base(b, new StackPanel { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal }) {
		}
	}
	
	class _Grid : _PanelBase {
		readonly Grid _grid; //same as panel, just to avoid casting everywhere
		int _row = -1, _col;
		bool _isSpan;
		double? _andWidth;
		
		public _Grid(wpfBuilder b) : base(b, new Grid()) {
			_grid = panel as Grid;
			if (gridLines) _grid.ShowGridLines = true;
		}
		
		public void Row(WBGridLength height) {
			if (_andWidth != null) throw new InvalidOperationException("And().Row()");
			if (_row >= 0) {
				_SetLastSpan();
				_col = 0;
			} else if (_grid.ColumnDefinitions.Count == 0) {
				_grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
				_grid.ColumnDefinitions.Add(new ColumnDefinition());
			}
			_row++;
			_grid.RowDefinitions.Add(height.Row);
		}
		
		public override void BeforeAdd(WBAdd flags = 0) {
			base.BeforeAdd(flags);
			if (flags.Has(WBAdd.ChildOfLast)) return;
			if (_row < 0 || _col >= _grid.ColumnDefinitions.Count) Row(0);
			_isSpan = false;
		}
		
		public override void Add(FrameworkElement c) {
			if (_andWidth != null) {
				var width = _andWidth.Value; _andWidth = null;
				if (width < 0) {
					var m = c.Margin;
					m.Left += -width + 3;
					c.Margin = m;
				} else if (width > 0) {
					c.Width = width;
					c.HorizontalAlignment = HorizontalAlignment.Right;
				}
				var last = LastDirect;
				Grid.SetColumn(c, Grid.GetColumn(last));
				Grid.SetColumnSpan(c, Grid.GetColumnSpan(last));
				_isSpan = true;
			} else {
				Grid.SetColumn(c, _col);
			}
			_col++;
			Grid.SetRow(c, _row);
			base.Add(c);
		}
		
		public void And(double width) {
			if (_col == 0 || _andWidth != null || LastAdded == panel) throw new InvalidOperationException("And()");
			var c = LastDirect;
			if (width < 0) {
				c.Width = -width;
				c.HorizontalAlignment = HorizontalAlignment.Left;
			} else if (width > 0) {
				var m = c.Margin;
				m.Right += width + 3;
				c.Margin = m;
			}
			_andWidth = width;
			_col--;
		}
		
		public void Span(int span) {
			if (_col == 0) throw new InvalidOperationException("Span() at row start");
			int cc = _grid.ColumnDefinitions.Count;
			_col--;
			if (span != 0) { //if 0, will add 2 controls in 1 cell
				if (span < 0 || _col + span > cc) span = cc - _col;
				Grid.SetColumnSpan(LastDirect, span);
				_col += span;
			}
			_isSpan = true;
		}
		
		//If not all row cells filled, let the last control span all remaining cells, unless its span specified explicitly.
		void _SetLastSpan() {
			if (!_isSpan && _row >= 0 && _col > 0) {
				int n = _grid.ColumnDefinitions.Count - _col;
				if (n > 0) Grid.SetColumnSpan(LastDirect, n + 1);
			}
			_isSpan = false;
		}
		
		public void Skip(int span = 1) {
			BeforeAdd();
			_col += span;
			_isSpan = true;
		}
		
		public override void End() {
			base.End();
			_SetLastSpan();
		}
		
		public (int column, int row) NextCell => (_col, _row);
	}
	
	#region current panel
	
	/// <summary>
	/// Ends adding controls etc to the window or nested panel (<see cref="StartGrid"/> etc).
	/// </summary>
	/// <remarks>
	/// Always call this method to end a nested panel. For root panel it is optional if using <see cref="ShowDialog"/>.
	/// </remarks>
	public wpfBuilder End() {
		if (!_p.ended) {
			_p.End();
			if (_p.parent != null) {
				_p = _p.parent;
			} else {
				
			}
		}
		return this;
	}
	
	/// <summary>
	/// Sets column count and widths of current grid.
	/// </summary>
	/// <param name="widths">
	/// Column widths.
	/// An argument can be:
	/// <br/>• <b>int</b> or <b>double</b> - <see cref="ColumnDefinition.Width"/>. Value 0 means auto-size. Negative value is star-width, ie fraction of total width of star-sized columns. Examples: <c>50</c>, <c>-0.5</c>.
	/// <br/>• <b>Range</b> - <see cref="ColumnDefinition.MinWidth"/> and/or <see cref="ColumnDefinition.MaxWidth"/>. Sets width value = -1 (star-sized). Examples: <c>50..150</c>, <c>50..</c> or <c>..150</c>.
	/// <br/>• tuple <b>(double value, Range minMax)</b> - width and min/max widths. Example: <c>(-2, 50..)</c>.
	/// <br/>• <see cref="ColumnDefinition"/>.
	/// </param>
	/// <exception cref="InvalidOperationException">Columns() in non-grid panel or after an <b>Add</b> function.</exception>
	/// <remarks>
	/// If this function not called, the table has 2 columns like <c>.Columns(0, -1)</c>.
	/// 
	/// If there are star-sized columns, should be set width of the grid or of its container. Call <see cref="Width"/> or <see cref="Size"/> or <see cref="WinSize"/>. But if the grid is in a cell of another grid, usually it's better to set column width of that grid to a non-zero value, ie let it be not auto-sized.
	/// </remarks>
	public wpfBuilder Columns(params WBGridLength[] widths) {
		var g = Last as Grid ?? throw new InvalidOperationException("Columns() in wrong place");
		g.ColumnDefinitions.Clear();
		foreach (var v in widths) g.ColumnDefinitions.Add(v.Column);
		return this;
	}
	
	/// <summary>
	/// Starts new row in current grid.
	/// </summary>
	/// <param name="height">
	/// Row height. Can be:
	/// <br/>• <b>int</b> or <b>double</b> - <see cref="RowDefinition.Height"/>. Value 0 means auto-size. Negative value is star-width, ie fraction of total height of star-sized rows. Examples: <c>50</c>, <c>-0.5</c>.
	/// <br/>• <b>Range</b> - <see cref="RowDefinition.MinHeight"/> and/or <see cref="RowDefinition.MaxHeight"/>. Sets height value = -1 (star-sized). Examples: <c>50..150</c>, <c>50..</c> or <c>..150</c>.
	/// <br/>• tuple <b>(double value, Range minMax)</b> - height and min/max heights. Example: <c>(-2, 50..200)</c>.
	/// <br/>• <see cref="RowDefinition"/>.
	/// </param>
	/// <exception cref="InvalidOperationException">In non-grid panel.</exception>
	/// <remarks>
	/// Calling this function is optional, except when not all cells of previous row are explicitly filled.
	/// 
	/// If there are star-sized rows, grid height should be defined. Call <see cref="Height"/> or <see cref="Size"/>. But if the grid is in a cell of another grid, usually it's better to set row height of that grid to a non-zero value, ie let it be not auto-sized.
	/// </remarks>
	public wpfBuilder Row(WBGridLength height) {
		if (_p.ended) throw new InvalidOperationException("Row() after End()");
		var g = _p as _Grid ?? throw new InvalidOperationException("Row() in non-grid panel");
		g.Row(height);
		return this;
	}
	
	/// <summary>
	/// Starts new auto-sized row in current grid. The same as <c>Row(0)</c>. See <see cref="Row"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">In non-grid panel.</exception>
	public wpfBuilder R => Row(0);
	
	#endregion
	
	#region ctors, window
	
	//	static readonly DependencyProperty _wpfBuilderProperty = DependencyProperty.RegisterAttached("_wpfBuilder", typeof(wpfBuilder), typeof(Panel));
	static ConditionalWeakTable<Panel, wpfBuilder> s_cwt = new();
	//which is better? Both fast.
	
	/// <summary>
	/// This constructor creates <see cref="System.Windows.Window"/> object with panel of specified type (default is <see cref="Grid"/>).
	/// </summary>
	/// <param name="windowTitle">Window title bar text.</param>
	/// <param name="panelType">Panel type. Default is <see cref="Grid"/>. Later you also can add nested panels of various types with <b>StartX</b> functions.</param>
	public wpfBuilder(string windowTitle, WBPanelType panelType = WBPanelType.Grid) {
		/*_container=*/
		_window = new Window() { Title = windowTitle };
		_AddRootPanel(_window, false, panelType, true);
	}
	
	/// <summary>
	/// This constructor creates panel of specified type (default is <see cref="Grid"/>) and optionally adds to a container.
	/// </summary>
	/// <param name="container">
	/// Window or some other element that will contain the panel. Should be empty, unless the type supports multiple direct child elements. Can be <c>null</c>.
	/// If the type (or base type) is <see cref="ContentControl"/> (<see cref="System.Windows.Window"/>, <see cref="TabItem"/>, <see cref="ToolTip"/>, etc), <see cref="Popup"/> or <see cref="Decorator"/> (eg <b>Border</b>), this function adds the panel to it. If <i>container</i> is <c>null</c> or an element of some other type, need to explicitly add the panel to it, like <c>container.Child = b.Panel;</c> or <c>container.Children.Add(b.Panel);</c> or <c>b.Tooltip(btt.Panel);</c> or <c>hwndSource.RootVisual = btt.Panel;</c> (the code depends on <i>container</i> type).
	/// </param>
	/// <param name="panelType">Panel type. Default is <see cref="Grid"/>. Later you also can add nested panels of various types with <b>StartX</b> functions.</param>
	/// <param name="setProperties">
	/// Set some container's properties like other overload does. Default <c>true</c>. Currently sets these properties, and only if <i>container</i> is of type <b>Window</b>:
	/// <br/>• <see cref="Window.SizeToContent"/>, except when <i>container</i> is <b>Canvas</b> or has properties <b>Width</b> and/or <b>Height</b> set.
	/// <br/>• <b>SnapsToDevicePixels</b> = <c>true</c>.
	/// <br/>• <b>WindowStartupLocation</b> = <b>Center</b>.
	/// <br/>• <b>Topmost</b> and <b>Background</b> depending on static properties <see cref="winTopmost"/> and <see cref="winWhite"/>.
	/// </param>
	public wpfBuilder(FrameworkElement container = null, WBPanelType panelType = WBPanelType.Grid, bool setProperties = true) {
		//_container=container; // ?? throw new ArgumentNullException("container"); //can be null
		_window = container as Window;
		_AddRootPanel(container, true, panelType, setProperties);
	}
	
	void _AddRootPanel(FrameworkElement container, bool externalContainer, WBPanelType panelType, bool setProperties) {
		switch (panelType) {
		case WBPanelType.Grid:
			_p = new _Grid(this);
			break;
		case WBPanelType.Canvas:
			_p = new _Canvas(this);
			break;
		case WBPanelType.Dock:
			_p = new _DockPanel(this);
			break;
		default:
			_p = new _StackPanel(this, panelType == WBPanelType.VerticalStack);
			break;
		}
		if (_window != null) _p.panel.Margin = new Thickness(3);
		switch (container) {
		case ContentControl c: c.Content = _p.panel; break;
		case Popup c: c.Child = _p.panel; break;
		case Decorator c: c.Child = _p.panel; break;
			//rejected. Rare. Let users add explicitly, like container.Child = b.Panel.
			//		case Panel c: c.Children.Add(_p.panel); break;
			//		case ItemsControl c: c.Items.Add(_p.panel); break;
			//		case TextBlock c: c.Inlines.Add(_p.panel); break;
			//		default: throw new NotSupportedException("Unsupported container type");
		}
		if (setProperties) {
			if (_window != null) {
				if (panelType != WBPanelType.Canvas) {
					if (externalContainer) {
						_window.SizeToContent = (double.IsNaN(_window.Width) ? SizeToContent.Width : 0) | (double.IsNaN(_window.Height) ? SizeToContent.Height : 0);
					} else {
						_window.SizeToContent = SizeToContent.WidthAndHeight;
					}
				}
				_window.SnapsToDevicePixels = true; //workaround for black line at bottom, for example when there is single CheckBox in Grid.
				//_window.UseLayoutRounding=true; //not here. Makes many controls bigger by 1 pixel when resizing window with grid, etc. Maybe OK if in _Add (for each non-panel element).
				if (_window.WindowStartupLocation == default) _window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				if (winTopmost) _window.Topmost = true;
				if (!winWhite) _window.Background = SystemColors.ControlBrush;
			}
		}
		s_cwt.Add(_p.panel, this);
		
		if (script.role == SRole.MiniProgram && _window != null) Loaded += () => { }; //set custom icon if need
	}
	
	/// <summary>
	/// Shows the window and waits until closed.
	/// </summary>
	/// <param name="owner">Owner window or element. Sets <see cref="Window.Owner"/>.</param>
	/// <exception cref="InvalidOperationException">
	/// - Container is not of type <b>Window</b>.
	/// - Missing <b>End</b> for a panel added with a <b>StartX</b> function.
	/// </exception>
	/// <remarks>
	/// Calls <see cref="End"/>, sets <see cref="Window.Owner"/> and calls <see cref="Window.ShowDialog"/>.
	/// You can instead call these functions directly. Or call <see cref="Window.Show"/> to show as non-modal window, ie don't wait. Or add <see cref="Panel"/> to some container window or other element, etc.
	/// </remarks>
	public bool ShowDialog(DependencyObject owner = null) {
		_ThrowIfNotWindow();
		if (_IsNested) throw new InvalidOperationException("Missing End() for a StartX() panel");
		End();
		//if (script.isWpfPreview) _window.Preview(); //no
		
		_window.Owner = owner == null ? null : owner as Window ?? Window.GetWindow(owner);
		//SHOULDDO: try to support AnyWnd. Why WPF here supports only Window and not Popup or HwndSource?
		
		return true == _window.ShowDialog();
	}
	
	/// <summary>
	/// Sets window width and/or height or/and min/max width/height.
	/// </summary>
	/// <param name="width">Width or/and min/max width.</param>
	/// <param name="height">Height or/and min/max height.</param>
	/// <exception cref="InvalidOperationException">
	/// - Container is not of type <b>Window</b>.
	/// - Cannot be after the last <b>End</b>.
	/// - Cannot be after <b>WinRect</b> or <b>WinSaved</b>.
	/// </exception>
	/// <remarks>
	/// Use WPF logical device-independent units, not physical pixels.
	/// </remarks>
	/// <seealso cref="WinRect"/>
	/// <seealso cref="WinSaved"/>
	public wpfBuilder WinSize(WBLength? width = null, WBLength? height = null) {
		_ThrowIfNotWindow();
		_ThrowIfWasWinRect();
		if (_IsWindowEnded) throw new InvalidOperationException("WinSize() cannot be after last End()"); //although currently could be anywhere
		var u = _window.SizeToContent;
		if (width != null) { var v = width.Value; v.ApplyTo(_window, false); u &= ~SizeToContent.Width; }
		if (height != null) { var v = height.Value; v.ApplyTo(_window, true); u &= ~SizeToContent.Height; }
		_window.SizeToContent = u;
		return this;
	}
	
	void _ThrowIfWasWinRectXY([CallerMemberName] string m_ = null) {
		if (_wasWinXY != 0) throw new InvalidOperationException(m_ + " cannot be after WinXY, WinRect or WinSaved.");
	}
	void _ThrowIfWasWinRect([CallerMemberName] string m_ = null) {
		if (_wasWinXY == 2) throw new InvalidOperationException(m_ + " cannot be after WinRect or WinSaved.");
	}
	byte _wasWinXY; //1 xy, 2 rect
	
	/// <summary>
	/// Sets window location.
	/// </summary>
	/// <param name="x">X coordinate in screen. Physical pixels.</param>
	/// <param name="y">Y coordinate in screen. Physical pixels.</param>
	/// <exception cref="InvalidOperationException">
	/// - Container is not of type <b>Window</b>.
	/// - Cannot be after <b>WinXY</b>, <b>WinRect</b> or <b>WinSaved</b>.
	/// </exception>
	/// <remarks>
	/// With this function use physical pixels, not WPF logical device-independent units.
	/// Call this function before showing the window. Don't change location/size-related window properties after that.
	/// Calls <see cref="ExtWpf.SetXY"/>.
	/// </remarks>
	/// <seealso cref="WinSaved"/>
	public wpfBuilder WinXY(int x, int y) {
		_ThrowIfNotWindow();
		_ThrowIfWasWinRectXY(); _wasWinXY = 1;
		_window.SetXY(x, y);
		return this;
	}
	
	/// <summary>
	/// Sets window rectangle (location and size).
	/// </summary>
	/// <param name="r">Rectangle in screen. Physical pixels.</param>
	/// <exception cref="InvalidOperationException">
	/// - Container is not of type <b>Window</b>.
	/// - Cannot be after <b>WinXY</b>, <b>WinRect</b> or <b>WinSaved</b>.
	/// </exception>
	/// <remarks>
	/// With this function use physical pixels, not WPF logical device-independent units.
	/// Call this function before showing the window. Don't change location/size-related window properties after that.
	/// Calls <see cref="ExtWpf.SetRect"/>.
	/// </remarks>
	/// <seealso cref="WinSaved"/>
	public wpfBuilder WinRect(RECT r) {
		_ThrowIfNotWindow();
		_ThrowIfWasWinRectXY(); _wasWinXY = 2;
		_window.SetRect(r);
		return this;
	}
	
	/// <summary>
	/// Saves window xy/size/state when closing and restores when opening.
	/// </summary>
	/// <param name="saved">String that the <i>save</i> action received previously. Can be <c>null</c> or <c>""</c>, usually first time (still not saved).</param>
	/// <param name="save">Called when closing the window. Receives string containing window xy/size/state. Can save it in registry, file, anywhere.</param>
	/// <exception cref="InvalidOperationException">
	/// - Container is not of type <b>Window</b>.
	/// - Cannot be after <b>WinXY</b>, <b>WinRect</b> or <b>WinSaved</b>.
	/// - Window is loaded.
	/// </exception>
	/// <remarks>
	/// Calls <see cref="WndSavedRect.Restore"/>.
	/// Call this function before showing the window. Don't change location/size-related window properties after that.
	/// If you use <see cref="WinSize"/>, call it before. It is used if size is still not saved. The same if you set window position or state.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// string rk = @"HKEY_CURRENT_USER\Software\Au\Test", rv = "winSR";
	/// var b = new wpfBuilder("Window").WinSize(300);
	/// b.Row(0).Add("Text", out TextBox _);
	/// b.R.AddOkCancel();
	/// b.WinSaved(Microsoft.Win32.Registry.GetValue(rk, rv, null) as string, o => Microsoft.Win32.Registry.SetValue(rk, rv, o));
	/// b.End();
	/// ]]></code>
	/// </example>
	public wpfBuilder WinSaved(string saved, Action<string> save) {
		_ThrowIfNotWindow();
		_ThrowIfWasWinRectXY(); _wasWinXY = 2;
		WndSavedRect.Restore(_window, saved, save);
		return this;
	}
	
	/// <summary>
	/// Changes various window properties.
	/// </summary>
	/// <param name="startLocation">Sets <see cref="WindowStartupLocation"/>.</param>
	/// <param name="resizeMode">Sets <see cref="Window.ResizeMode"/>.</param>
	/// <param name="showActivated">Sets <see cref="Window.ShowActivated"/>.</param>
	/// <param name="showInTaskbar">Sets <see cref="Window.ShowInTaskbar"/>.</param>
	/// <param name="topmost">Sets <see cref="Window.Topmost"/>.</param>
	/// <param name="state">Sets <see cref="Window.WindowState"/>.</param>
	/// <param name="style">Sets <see cref="Window.WindowStyle"/>.</param>
	/// <param name="icon">Sets <see cref="Window.Icon"/>. Example: <c>.WinProperties(icon: BitmapFrame.Create(new Uri(@"d:\icons\file.ico")))</c>.</param>
	/// <param name="whiteBackground">Set background color = <b>SystemColors.WindowBrush</b> (normally white) if <c>true</c> or <b>SystemColors.ControlBrush</b> (dialog color) if <c>false</c>. See also <see cref="winWhite"/>, <see cref="Brush"/>.</param>
	/// <exception cref="InvalidOperationException">
	/// - Container is not of type <b>Window</b>.
	/// - <i>startLocation</i> or <i>state</i> used after <b>WinXY</b>, <b>WinRect</b> or <b>WinSaved</b>.
	/// </exception>
	/// <remarks>
	/// The function uses only non-<c>null</c> parameters.
	/// Or you can change <see cref="Window"/> properties directly, for example <c>b.Window.Topmost = true;</c>.
	/// </remarks>
	public wpfBuilder WinProperties(WindowStartupLocation? startLocation = null, ResizeMode? resizeMode = null, bool? showActivated = null, bool? showInTaskbar = null, bool? topmost = null, WindowState? state = null, WindowStyle? style = null, ImageSource icon = null, bool? whiteBackground = null) {
		_ThrowIfNotWindow();
		if (startLocation.HasValue) { _ThrowIfWasWinRectXY("WinProperties(startLocation)"); _window.WindowStartupLocation = startLocation.Value; }
		if (resizeMode.HasValue) _window.ResizeMode = resizeMode.Value;
		if (showActivated.HasValue) _window.ShowActivated = showActivated.Value;
		if (showInTaskbar.HasValue) _window.ShowInTaskbar = showInTaskbar.Value;
		if (topmost.HasValue) _window.Topmost = topmost.Value;
		if (state.HasValue) { _ThrowIfWasWinRectXY("WinProperties(state)"); _window.WindowState = state.Value; }
		if (style.HasValue) _window.WindowStyle = style.Value;
		if (whiteBackground.HasValue) _window.Background = whiteBackground.Value ? SystemColors.WindowBrush : SystemColors.ControlBrush;
		if (icon != null) _window.Icon = icon;
		return this;
	}
	
	#endregion
	
	#region properties, events
	
	/// <summary>
	/// Gets the top-level window.
	/// </summary>
	/// <returns><c>null</c> if container is not of type <b>Window</b>.</returns>
	public Window Window => _window;
	
	/// <summary>
	/// Gets current <see cref="Grid"/> or <see cref="StackPanel"/> or etc.
	/// </summary>
	public Panel Panel => _p.panel;
	
	/// <summary>
	/// Gets the last child or descendant element added in current panel. Before that returns current panel.
	/// </summary>
	/// <remarks>
	/// The "set properties of last element" functions set properties of this element.
	/// </remarks>
	public FrameworkElement Last => _p.LastAdded;
	
	/// <summary>
	/// Gets the child or descendant element added in current panel before adding <see cref="Last"/>. Can be <c>null</c>.
	/// </summary>
	/// <remarks>
	/// For example, after calling the <b>Add</b> overload that adds 2 elements (the first is <b>Label</b>), this property returns the <b>Label</b>.
	/// </remarks>
	public FrameworkElement Last2 => _p.LastAdded2;
	
	//	not useful
	//	/// <summary>
	//	/// Gets the last direct child element added in current panel. Before that returns current panel or its parent <b>GroupBox</b>.
	//	/// </summary>
	//	public FrameworkElement LastDirect => _p.LastDirect;
	
	/// <summary>
	/// When root panel loaded and visible. Once.
	/// </summary>
	/// <remarks>
	/// If the panel is in a <b>TabControl</b>, this event is fired when the tab page is selected/loaded first time.
	/// When this event is fired, handles of visible <b>HwndHost</b>-based controls are already created.
	/// </remarks>
	public event Action Loaded {
		add {
			if (!_loadedEvent2) { _loadedEvent2 = true; Panel.Loaded += _Panel_Loaded; }
			_loadedEvent += value;
		}
		remove {
			_loadedEvent -= value;
		}
	}
	Action _loadedEvent;
	bool _loadedEvent2;
	
	private void _Panel_Loaded(object sender, RoutedEventArgs e) {
		var p = sender as Panel;
		if (!p.IsVisible) return;
		p.Loaded -= _Panel_Loaded;
		
		//if role miniProgram, use assembly icon instead of apphost icon
		if (script.role == SRole.MiniProgram && _window != null && _window.Icon == null) {
			var hm = Api.GetModuleHandle(Assembly.GetEntryAssembly().Location);
			if (default != Api.FindResource(hm, Api.IDI_APPLICATION, Api.RT_GROUP_ICON)) {
				var w = _window.Hwnd();
				icon.FromModuleHandle_(hm, Dpi.Scale(16, w))?.SetWindowIcon(w, false);
				icon.FromModuleHandle_(hm, Dpi.Scale(32, w))?.SetWindowIcon(w, true);
			}
		}
		
		_loadedEvent?.Invoke();
	}
	
	/// <summary>
	/// When clicked <b>OK</b> or <b>Apply</b> button.
	/// </summary>
	/// <remarks>
	/// <see cref="Button.IsDefault"/> is <c>true</c> if it is <b>OK</b> button.
	/// The parameter's property <b>Cancel</b> can be used to prevent closing the window.
	/// </remarks>
	public event Action<WBButtonClickArgs> OkApply;
	
	#endregion
	
	#region static
	
	/// <summary>
	/// <see cref="Grid.ShowGridLines"/> of grid panels created afterwards.
	/// To be used at design time only.
	/// </summary>
	public static bool gridLines { get; set; }
	
	/// <summary>
	/// <see cref="Window.Topmost"/> of windows created afterwards.
	/// Usually used at design time only, to make always on top of editor window.
	/// </summary>
	public static bool winTopmost { get; set; }
	
	/// <summary>
	/// If <c>true</c>, constructor does not change color of windows created afterwards; then color normally is white.
	/// If <c>false</c> constructor sets standard color of dialogs, usually light gray.
	/// Default value depends on application's theme and usually is <c>true</c> if using custom theme.
	/// </summary>
	//	public static bool winWhite { get; set; } = _IsCustomTheme(); //no, called too early
	public static bool winWhite { get => s_winWhite ??= _IsCustomTheme(); set { s_winWhite = value; } }
	static bool? s_winWhite;
	
	//	/// <summary>
	//	/// Default modifyPadding option value. See <see cref="Options"/>.
	//	/// </summary>
	//	public static bool modifyPadding { get; set; }
	
	#endregion
	
	#region add
	
	/// <summary>
	/// Changes some options for elements added afterwards.
	/// </summary>
	/// <param name="modifyPadding">Let <b>Add</b> adjust the <b>Padding</b> property of some controls to align content better when using default theme. Default value of this option depends on application's theme.</param>
	/// <param name="rightAlignLabels">Right-align <b>Label</b> controls in grid cells.</param>
	/// <param name="margin">Default margin of elements. If not set, default margin is 3 in all sides. Default margin of nested panels is 0; this option is not used.</param>
	/// <param name="showToolTipOnKeyboardFocus">Show tooltips when the tooltip owner element receives the keyboard focus when using keys to focus controls or open the window. If <c>true</c>, it can be set separately for each tooltip or owner element with <see cref="ToolTip.ShowsToolTipOnKeyboardFocus"/> or <see cref="ToolTipService.SetShowsToolTipOnKeyboardFocus(DependencyObject, bool?)"/>.</param>
	/// <param name="bindLabelVisibility">Let <see cref="LabeledBy"/> and the <b>Add</b> overload that adds 2 elements (the first is <b>Label</b>) bind the <b>Visibility</b> property of the label to that of the last added element, to automatically hide/show the label together with the element.</param>
	public wpfBuilder Options(bool? modifyPadding = null, bool? rightAlignLabels = null, Thickness? margin = null, bool? showToolTipOnKeyboardFocus = null, bool? bindLabelVisibility = null) {
		if (modifyPadding != null) _opt_modifyPadding = modifyPadding.Value;
		if (rightAlignLabels != null) _opt_rightAlignLabels = rightAlignLabels.Value;
		if (margin != null) _opt_margin = margin.Value;
		if (showToolTipOnKeyboardFocus != null) _opt_showToolTipOnKeyboardFocus = showToolTipOnKeyboardFocus.Value;
		if (bindLabelVisibility != null) _opt_bindLabelVisibility = bindLabelVisibility.Value;
		return this;
	}
	bool _opt_modifyPadding = !_IsCustomTheme();
	bool _opt_rightAlignLabels;
	Thickness _opt_margin = new(3);
	//string _opt_radioGroup; //rejected. Radio buttons have problems with high DPI and should not be used. Or can put groups in panels.
	//	double _opt_checkMargin=3; //rejected
	bool _opt_showToolTipOnKeyboardFocus;
	bool _opt_bindLabelVisibility;
	
	void _Add(FrameworkElement e, object text, WBAdd flags, bool add) {
		bool childOfLast = flags.Has(WBAdd.ChildOfLast);
		if (!flags.Has(WBAdd.DontSetProperties)) {
			if (e is Control c) {
				//rejected: modify padding etc through XAML. Not better than this.
				//rejected: use _opt_modifyPadding only if font Segoe UI. Tested with several fonts.
				switch (c) {
				case Label:
					if (_opt_modifyPadding) c.Padding = new Thickness(1, 2, 1, 1); //default 5
					if (_opt_rightAlignLabels) c.HorizontalAlignment = HorizontalAlignment.Right;
					break;
				case TextBox:
				case PasswordBox:
					if (_opt_modifyPadding) c.Padding = new Thickness(2, 1, 1, 2); //default padding 0, height 18
					break;
				case Button:
					if (_opt_modifyPadding && text is string) c.Padding = new Thickness(5, 1, 5, 2); //default 1
					break;
				case ToggleButton:
					c.HorizontalAlignment = HorizontalAlignment.Left; //default stretch
					c.VerticalAlignment = VerticalAlignment.Center; //default top. Note: VerticalContentAlignment bad on Win7.
					
					//partial workaround for squint CheckBox/RadioButton when High DPI.
					//	Without it, check mark size/alignment is different depending on control's xy.
					//	With it at least all controls are equal, either bad (eg DPI 125%) or good (DPI 150%).
					//	When bad, normal CheckBox check mark now still looks good. Only third state and RadioButtons look bad, but it is better than when controls look differently.
					//	But now at 150% DPI draws thick border.
					//c.UseLayoutRounding=true;
					//c.SnapsToDevicePixels=true; //does not help
					break;
				case ComboBox cb:
					//Change padding because default Windows font Segoe UI is badly centered vertically. Too big space above text, and too big control height.
					//Tested: changed padding isn't the reason of different control heights or/and arrows when high DPI.
					if (cb.IsEditable) {
						if (_opt_modifyPadding) c.Padding = new Thickness(2, 1, 2, 2); //default (2)
					} else {
						if (_opt_modifyPadding) c.Padding = new Thickness(5, 2, 4, 3); //default (6,3,5,3)
					}
					break;
				}
			} else if (e is Image) {
				e.UseLayoutRounding = true; //workaround for blurred images
			}
			
			//workaround for:
			//	1. Blurred images in some cases.
			//	2. High DPI: Different height of controls of same class, eg TextBox, ComboBox.
			//	3. High DPI: Different height/shape/alignment of control parts, eg CheckBox/RadioButton check mark and ComboBox arrow.
			//	Bad: on DPI 150% makes control borders 2-pixel.
			//	Rejected. Thick border is very noticeable, especially TabControl. Different control sizes, v check mark and v arrow aren't so noticeable. Radio buttons and null checkboxes rarely used. Most my tested WPF programs don't use this.
			//			e.UseLayoutRounding=true;
			//			e.SnapsToDevicePixels=true; //does not help
			
			if (text != null) {
				switch (e) {
				case HeaderedContentControl u: u.Header = text; break; //GroupBox, Expander
				case HeaderedItemsControl u: u.Header = text; break;
				case ContentControl u: u.Content = text; break; //Label, buttons, etc
				case TextBox u: u.Text = text.ToString(); break;
				case PasswordBox u: u.Password = text.ToString(); break;
				case ComboBox u: u.Text = text.ToString(); break;
				case TextBlock u: u.Text = text.ToString(); break;
				case RichTextBox u: u.AppendText(text.ToString()); break;
				//default: throw new NotSupportedException($"Add() cannot set text/content of {e.GetType().Name}.");
				default:
					if (!_PropGetSet<string>.TryCreate(e, "Text", out var pgs)) throw new NotSupportedException($"Add() cannot set text/content of {e.GetType().Name}.");
					pgs.Set(text.ToString());
					break;
				}
			}
		}
		if (!(childOfLast || e is GridSplitter)) e.Margin = _opt_margin;
		
		if (add) {
			_AddToParent(e, childOfLast);
			if (_alsoAll != null) {
				_alsoAllArgs ??= new WBAlsoAllArgs();
				if (_p is _Grid g) {
					var v = g.NextCell;
					_alsoAllArgs.Column = v.column - 1;
					_alsoAllArgs.Row = v.row;
				} else {
					_alsoAllArgs.Column = _alsoAllArgs.Row = -1;
				}
				_alsoAll(this, _alsoAllArgs);
			}
		}
	}
	
	void _AddToParent(FrameworkElement e, bool childOfLast) {
		if (childOfLast) { //info: BeforeAdd throws exception if Last is panel
			switch (Last) {
			case ContentControl d: d.Content = e; break;
			case Decorator d: d.Child = e; break;
			//case Panel d: d.Children.Add(e); break; //no, cannot add multiple items because Last becomes the added child
			default: throw new NotSupportedException($"Cannot add child to {Last.GetType().Name}.");
			}
			_p.SetLastAdded(e);
		} else {
			_p.Add(e);
		}
	}
	
	/// <summary>
	/// Adds an existing element (control etc of any type).
	/// </summary>
	/// <param name="element"></param>
	/// <param name="flags"></param>
	/// <exception cref="NotSupportedException">The function does not support flag <i>childOfLast</i> for this element type.</exception>
	public wpfBuilder Add(FrameworkElement element, WBAdd flags = 0) {
		_p.BeforeAdd(flags);
		_Add(element, null, flags, true);
		return this;
	}
	
	/// <summary>
	/// Creates and adds element of type <i>T</i> (control etc of any type).
	/// </summary>
	/// <param name="variable">
	/// Receives element's variable. The function creates element of variable's type. You can use the variable to set element's properties before showing window or/and to get value after.
	/// Examples: <c>.Add(out CheckBox c1, "Text")</c>, <c>.Add(out _textBox1)</c>. If don't need a variable: <c>.Add(out Label _, "Text")</c> or <c>.Add&lt;Label>("Text")</c>.
	/// </param>
	/// <param name="text">
	/// Text, header or other content. Supported element types (or base types):
	/// <br/>• <see cref="TextBox"/> - sets <b>Text</b> property.
	/// <br/>• <see cref="ComboBox"/> - sets <b>Text</b> property (see also <see cref="Items"/>).
	/// <br/>• <see cref="PasswordBox"/> - sets <b>Password</b> property.
	/// <br/>• <see cref="TextBlock"/> - sets <b>Text</b> property (see also <see cref="FormatText"/> and <see cref="formattedText"/>).
	/// <br/>• <see cref="HeaderedContentControl"/>, <see cref="HeaderedItemsControl"/> - sets <b>Header</b> property (see also <see cref="FormatText"/> and <see cref="formattedText"/>).
	/// <br/>• <see cref="ContentControl"/> except above two - sets <b>Content</b> property (can be string, other element, etc) (see also <see cref="FormatText"/> and <see cref="formattedText"/>).
	/// <br/>• <see cref="RichTextBox"/> - calls <b>AppendText</b> (see also <see cref="LoadFile"/>).
	/// <br/>• Other element types that have <b>Text</b> property.
	/// </param>
	/// <param name="flags"></param>
	/// <exception cref="NotSupportedException">The function does not support non-<c>null</c> <i>text</i> or flag <i>childOfLast</i> for this element type.</exception>
	public wpfBuilder Add<T>(out T variable, object text = null, WBAdd flags = 0) where T : FrameworkElement, new() {
		if (text is WBAdd f1 && flags == 0) { flags = f1; text = null; } //it's easy to make a mistake - use WBAdd flags as the second argument. Roslyn shows WBAdd completions for the second parameter.
		_p.BeforeAdd(flags);
		variable = new T();
		_Add(variable, text, flags, true);
		return this;
	}
	
	/// <summary>
	/// Creates and adds element of type <i>T</i> (any type). This overload can be used when don't need element's variable.
	/// </summary>
	/// <param name="text">Text, header or other content. More info - see other overload.</param>
	/// <param name="flags"></param>
	/// <exception cref="NotSupportedException">The function does not support non-<c>null</c> <i>text</i> or flag <i>childOfLast</i> for this element type.</exception>
	public wpfBuilder Add<T>(object text = null, WBAdd flags = 0) where T : FrameworkElement, new() => Add(out T _, text, flags);
	
	/// <summary>
	/// Adds 2 elements: <see cref="Label"/> and element of type <i>T</i> (control etc of any type).
	/// </summary>
	/// <param name="label">Label text. Usually string or <see cref="TextBlock"/>. Example: <c>new TextBlock() { TextWrapping = TextWrapping.Wrap, Text = "long text" }</c>.</param>
	/// <param name="variable">Variable of second element. More info - see other overload.</param>
	/// <param name="text">Text, header or other content of second element. More info - see other overload.</param>
	/// <param name="row2">If not <c>null</c>, after adding first element calls <see cref="Row"/> with this argument.</param>
	/// <exception cref="NotSupportedException">If the function does not support non-<c>null</c> <i>text</i> for this element type.</exception>
	/// <remarks>
	/// Sets <see cref="Label.Target"/> if the first element is <b>Label</b>, calls <see cref="System.Windows.Automation.AutomationProperties.SetLabeledBy"/> and applies the <i>bindLabelVisibility</i> option (see <see cref="Options"/>).
	/// </remarks>
	public wpfBuilder Add<T>(object label, out T variable, object text = null, WBGridLength? row2 = null) where T : FrameworkElement, new()
		=> _Add2(out Label _, label, out variable, text, row2);
	
	wpfBuilder _Add2<T1, T2>(out T1 var1, object text1, out T2 var2, object text2 = null, WBGridLength? row2 = null) where T1 : FrameworkElement, new() where T2 : FrameworkElement, new() {
		Add(out var1, text1);
		if (row2 != null) Row(row2.Value);
		Add(out var2, text2); //note: no flags
		return this.LabeledBy(var1);
	}
	
#if !DEBUG
	/// <summary>
	/// Adds 2 elements. One of type <b>T1</b>, other of type <b>T2</b>.
	/// </summary>
	/// <param name="var1">Variable of first element. More info - see other overload.</param>
	/// <param name="text1">Text, header or other content of first element. More info - see other overload.</param>
	/// <param name="var2">Variable of second element. More info - see other overload.</param>
	/// <param name="text2">Text, header or other content of second element. More info - see other overload.</param>
	/// <param name="row2">If not <c>null</c>, after adding first element calls <see cref="Row"/> with this argument.</param>
	/// <exception cref="NotSupportedException">If the function does not support non-<c>null</c> <i>text</i> for element type <b>T1</b> or <b>T2</b>.</exception>
	/// <remarks>
	/// If <b>T1</b> is <b>Label</b>, sets <see cref="Label.Target"/>. If <b>T1</b> is <b>Label</b> or <b>TextBlock</b>, calls <see cref="System.Windows.Automation.AutomationProperties.SetLabeledBy"/>.
	/// </remarks>
	[EditorBrowsable(EditorBrowsableState.Never)] //obsolete. Too many overloads, confusing. Instead users can add label element separately and use <b>LabeledBy</b>.
	[Obsolete]
	public wpfBuilder Add<T1, T2>(out T1 var1, object text1, out T2 var2, object text2 = null, WBGridLength? row2 = null) where T1 : FrameworkElement, new() where T2 : FrameworkElement, new() {
		return _Add2(out var1, text1, out var2, text2, row2);
	}
#endif
	
	/// <summary>
	/// Adds button with <see cref="ButtonBase.Click"/> event handler.
	/// </summary>
	/// <param name="variable">Receives button's variable.</param>
	/// <param name="text">Text/content (<see cref="ContentControl.Content"/>).</param>
	/// <param name="click">Action to call when the button clicked. Its parameter's property <b>Cancel</b> can be used to prevent closing the window when clicked this <b>OK</b> button. Not called if validation fails.</param>
	/// <param name="flags"></param>
	/// <remarks>
	/// If <i>flags</i> contains <b>OK</b> or <b>Apply</b> or <b>Validate</b> and this window contains elements for which was called <see cref="Validation"/>, on click performs validation; if fails, does not call the <i>click</i> action and does not close the window.
	/// </remarks>
	public wpfBuilder AddButton(out Button variable, object text, Action<WBButtonClickArgs> click, WBBFlags flags = 0/*, Action<WBButtonClickArgs> clickSplit = null*/) {
		Add(out variable, text);
		var c = variable;
		if (flags.Has(WBBFlags.OK)) c.IsDefault = true;
		if (flags.Has(WBBFlags.Cancel)) c.IsCancel = true;
		if (flags.HasAny(WBBFlags.OK | WBBFlags.Cancel | WBBFlags.Apply)) { c.MinWidth = 70; c.MinHeight = 21; }
		if (click != null || flags.HasAny(WBBFlags.OK | WBBFlags.Cancel | WBBFlags.Apply | WBBFlags.Validate)) {
			c.Click += (_, _) => {
				var w = _FindWindow(c);
				if (flags.HasAny(WBBFlags.OK | WBBFlags.Apply | WBBFlags.Validate) && !_Validate(w, c)) return;
				bool needEvent = flags.HasAny(WBBFlags.OK | WBBFlags.Apply) && OkApply != null;
				var e = (needEvent || click != null) ? new WBButtonClickArgs { Button = c, Window = w } : null;
				if (needEvent) {
					OkApply(e);
					if (e.Cancel) return;
				}
				if (click != null) {
					click(e);
					if (e.Cancel) return;
				}
				if (flags.Has(WBBFlags.OK)) {
					bool modal = w.IsModal_() != false;
					if (modal) {
						try { w.DialogResult = true; }
						catch (InvalidOperationException) { modal = false; } //failed to detect modal?
					}
					if (!modal) w.Close();
				} else if (flags.Has(WBBFlags.Cancel)) {
					w.Close(); //info: IsCancel ignored if nonmodal
				}
			};
		}
		//if(clickSplit!=null) c.ClickSplit+=clickSplit;
		//FUTURE: split-button.
		return this;
	}
	//	/// <param name="clickSplit">
	//	/// If not null, creates split-button. Action to call when the arrow part clicked. Example:
	//	/// <br/><c>b => { int mi = popupMenu.showSimple("1 One|2 Two", b, (0, b.Height)); }</c>
	//	/// </param>
	
	/// <inheritdoc cref="AddButton(out Button, object, Action{WBButtonClickArgs}, WBBFlags)"/>
	public wpfBuilder AddButton(object text, Action<WBButtonClickArgs> click, WBBFlags flags = 0/*, Action<WBButtonClickArgs> clickSplit = null*/) {
		return AddButton(out _, text, click, flags);
	}
	
	/// <summary>
	/// Adds button that closes the window and sets <see cref="ResultButton"/>.
	/// </summary>
	/// <param name="text">Text/content (<see cref="ContentControl.Content"/>).</param>
	/// <param name="result"><see cref="ResultButton"/> value when clicked this button.</param>
	/// <remarks>
	/// When clicked, sets <see cref="ResultButton"/> = <i>result</i>, closes the window, and <see cref="ShowDialog"/> returns <c>true</c>.
	/// </remarks>
	public wpfBuilder AddButton(object text, int result/*, Action<WBButtonClickArgs> clickSplit = null*/) {
		Add(out Button c, text);
		c.Click += (_, _) => { _resultButton = result; _FindWindow(c).DialogResult = true; };
		//if(clickSplit!=null) c.ClickSplit+=clickSplit;
		return this;
	}
	//	/// <param name="clickSplit">
	//	/// If not null, creates split-button. Action to call when the arrow part clicked. Example:
	//	/// <br/><c>b => { int mi = popupMenu.showSimple("1 One|2 Two", b, (0, b.Height)); }</c>
	//	/// </param>
	
	/// <summary>
	/// If the window closed with an <see cref="AddButton(object, int)"/> button, returns its <i>result</i>. Else returns 0.
	/// Note: if the button is in a tab page, use the <b>wpfBuilder</b> variable of that page.
	/// </summary>
	public int ResultButton => _resultButton;
	int _resultButton;
	
	/// <summary>
	/// Adds <b>OK</b> and/or <b>Cancel</b> and/or <b>Apply</b> buttons.
	/// </summary>
	/// <param name="ok">Text of <b>OK</b> button. If <c>null</c>, does not add the button.</param>
	/// <param name="cancel">Text of <b>Cancel</b> button. If <c>null</c>, does not add the button.</param>
	/// <param name="apply">Text of <b>Apply</b> button. If <c>null</c>, does not add the button.</param>
	/// <param name="stackPanel">Add a right-bottom aligned <see cref="StackPanel"/> that contains the buttons. See <see cref="StartOkCancel"/>. If <c>null</c> (default), adds if not already in a stack panel, except when there is 1 button.</param>
	/// <remarks>
	/// Sets properties of <b>OK</b>/<b>Cancel</b> buttons so that click and <c>Enter</c>/<c>Esc</c> close the window; then <see cref="ShowDialog"/> returns <c>true</c> on <b>OK</b>, <c>false</c> on <b>Cancel</b>.
	/// See also event <see cref="OkApply"/>.
	/// </remarks>
	public wpfBuilder AddOkCancel(string ok = "OK", string cancel = "Cancel", string apply = null, bool? stackPanel = null)
		=> AddOkCancel(out _, out _, out _, ok, cancel, apply, stackPanel);
	
	/// <param name="bOK">Variable of <b>OK</b> button.</param>
	/// <param name="bCancel">Variable of <b>Cancel</b> button.</param>
	/// <param name="bApply">Variable of <b>Apply</b> button.</param>
	/// <inheritdoc cref="AddOkCancel(string, string, string, bool?)"/>
	public wpfBuilder AddOkCancel(out Button bOK, out Button bCancel, out Button bApply, string ok = "OK", string cancel = "Cancel", string apply = null, bool? stackPanel = null) {
		int n = 0; if (ok != null) n++; if (cancel != null) n++;
		if (n == 0) throw new ArgumentNullException();
		bool stack = stackPanel ?? (n > 1 && !(_p is _StackPanel));
		if (stack) StartOkCancel();
		if (ok != null) AddButton(out bOK, ok, null, WBBFlags.OK); else bOK = null;
		if (cancel != null) AddButton(out bCancel, cancel, null, WBBFlags.Cancel); else bCancel = null;
		if (apply != null) AddButton(out bApply, apply, null, WBBFlags.Apply); else bApply = null;
		if (stack) End();
		return this;
	}
	
	/// <summary>
	/// Adds <see cref="Separator"/> control.
	/// </summary>
	/// <param name="vertical">If <c>true</c>, adds vertical separator. If <c>false</c>, horizontal. If <c>null</c> (default), adds vertical if in horizontal stack panel, else adds horizontal.</param>
	/// <remarks>
	/// In <b>Canvas</b> panel separator's default size is 1x1. Need to set size, like <c>.AddSeparator().XY(0, 50, 100, 1)</c>.
	/// </remarks>
	public wpfBuilder AddSeparator(bool? vertical = null) {
		Add(out Separator c);
		if (vertical ?? (_p.panel is StackPanel p && p.Orientation == Orientation.Horizontal)) {
			c.Style = _style_VertSep ??= c.FindResource(ToolBar.SeparatorStyleKey) as Style;
		}
		c.UseLayoutRounding = true; //workaround: separators of different thickness when high DPI
		return this;
	}
	Style _style_VertSep;
	
	/// <summary>
	/// Adds enum members as <b>StackPanel</b> with checkboxes (if it's a <c>[Flags]</c> enum) or <b>ComboBox</b> control.
	/// </summary>
	/// <param name="e">Variable for getting result later. See <see cref="EnumUI{TEnum}.Result"/>.</param>
	/// <param name="init">Initial value.</param>
	/// <param name="items">Enum members and their text/tooltip. Optional. Text can be: <c>null</c>, <c>"text"</c>, <c>"text|tooltip"</c>, <c>"|tooltip"</c>.</param>
	/// <param name="label">If not <c>null</c>, adds a <b>GroupBox</b> or <b>Label</b> control with this label. If it's a <c>[Flags]</c> enum, adds <b>GroupBox</b> as parent of checkboxes, else adds <b>Label</b> before the <b>ComboBox</b> (uses 2 grid cells).</param>
	/// <param name="vertical">Vertical stack. Default <c>true</c>.</param>
	/// <example>
	/// <code><![CDATA[
	/// var b = new wpfBuilder("Window").WinSize(250);
	/// b.R.AddEnum<KMod>(out var e1, KMod.Ctrl | KMod.Alt, label: "Modifiers", vertical: false);
	/// b.R.AddEnum<DayOfWeek>(out var e2, DateTime.Today.DayOfWeek, label: "Day");
	/// b.R.AddOkCancel();
	/// if (!b.ShowDialog()) return;
	/// print.it(e1.Result);
	/// print.it(e2.Result);
	/// ]]></code>
	/// </example>
	public wpfBuilder AddEnum<TEnum>(out EnumUI<TEnum> e, TEnum init = default, (TEnum value, string text)[] items = null, string label = null, bool vertical = true) where TEnum : unmanaged, Enum {
		if (typeof(TEnum).IsDefined(typeof(FlagsAttribute), false)) {
			if (label != null) StartStack<GroupBox>(label, vertical: vertical);
			else StartStack(vertical: vertical);
			e = new EnumUI<TEnum>(Panel as StackPanel, init, items);
			End();
		} else {
			ComboBox cb;
			if (label != null) Add(label, out cb);
			else Add(out cb);
			e = new EnumUI<TEnum>(cb, init, items);
		}
		return this;
	}
	
	/// <summary>
	/// Adds one or more empty cells in current row of current grid.
	/// </summary>
	/// <param name="span">Column count.</param>
	/// <exception cref="InvalidOperationException">In non-grid panel.</exception>
	/// <remarks>
	/// Actually just changes column index where next element will be added.
	/// </remarks>
	public wpfBuilder Skip(int span = 1) {
		if (span < 0) throw new ArgumentException();
		var g = _p as _Grid ?? throw new InvalidOperationException("Skip() in non-grid panel");
		g.Skip(span);
		return this;
	}
	
	/// <summary>
	/// Sets to add next element in the same grid cell as previous element.
	/// </summary>
	/// <param name="width">Width of next element. If negative - width of previous element. Also it adds to the corresponding margin of other element. If 0, simply adds in the same place as previous element.</param>
	/// <exception cref="InvalidOperationException">In non-grid panel or in a wrong place.</exception>
	/// <remarks>
	/// Can be used to add 2 elements in 1 cell as a cheaper and more concise way than with a <b>StartX</b> function.
	/// Next element will inherit column index and span of previous element but won't inherit row span.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// b.Add("File", out TextBox _).And(70).AddButton("Browse...", null);
	/// ]]></code>
	/// </example>
	public wpfBuilder And(double width) {
		var g = _p as _Grid ?? throw new InvalidOperationException("And() in non-grid panel");
		g.And(width);
		return this;
	}
	
	#endregion
	
	#region set common properties of last added element
	
	/// <summary>
	/// Sets column span of the last added element.
	/// </summary>
	/// <param name="columns">Column count. If -1 or too many, will span all remaining columns in current row. If 0, will share 1 column with next element added in current row; to set element positions use <see cref="Margin"/>, <see cref="Width"/> and <see cref="Align"/>; see also <see cref="And"/>.</param>
	/// <exception cref="InvalidOperationException">In non-grid panel.</exception>
	public wpfBuilder Span(int columns) {
		_ParentOfLastAsOrThrow<_Grid>().Span(columns);
		return this;
	}
	
	/// <summary>
	/// Sets row span of the last added element.
	/// </summary>
	/// <param name="rows">Row count.</param>
	/// <exception cref="InvalidOperationException">In non-grid panel.</exception>
	/// <remarks>
	/// In next row(s) use <see cref="Skip"/> to skip cells occupied by this element.
	/// Often it's better to add a nested panel instead. See <see cref="StartGrid"/>.
	/// </remarks>
	public wpfBuilder SpanRows(int rows) {
		var c = _ParentOfLastAsOrThrow<_Grid>().LastDirect;
		Grid.SetRowSpan(c, rows);
		return this;
	}
	
	//rejected
	///// <summary>
	///// Calls your callback function.
	///// </summary>
	///// <param name="action"></param>
	//public wpfBuilder Also(Action<wpfBuilder> action) {
	//	action(this);
	//	return this;
	//}
	
	/// <summary>
	/// Sets callback function to be called by <b>AddX</b> functions for each element added afterwards. Not called by <b>StartX</b> functions for panels.
	/// </summary>
	/// <param name="action">Callback function or <c>null</c>.</param>
	/// <example>
	/// <code><![CDATA[
	/// b.AlsoAll((b, e) => {
	/// 	if(b.Last is CheckBox c) { c.IsChecked = true; b.Margin("t1 b1"); }
	/// });
	/// ]]></code>
	/// </example>
	public wpfBuilder AlsoAll(Action<wpfBuilder, WBAlsoAllArgs> action) {
		_alsoAll = action;
		return this;
	}
	Action<wpfBuilder, WBAlsoAllArgs> _alsoAll;
	WBAlsoAllArgs _alsoAllArgs;
	
	/// <summary>
	/// Sets width and height of the last added element. Optionally sets alignment.
	/// </summary>
	/// <param name="width">Width or/and min/max width.</param>
	/// <param name="height">Height or/and min/max height.</param>
	/// <param name="alignX">Horizontal alignment. If not <c>null</c>, calls <see cref="Align(string, string)"/>.</param>
	/// <param name="alignY">Vertical alignment.</param>
	/// <exception cref="ArgumentException">Invalid alignment string.</exception>
	public wpfBuilder Size(WBLength width, WBLength height, string alignX = null, string alignY = null) {
		var c = Last;
		width.ApplyTo(c, false);
		height.ApplyTo(c, true);
		if (alignX != null || alignY != null) Align(alignX, alignY);
		return this;
	}
	
	/// <summary>
	/// Sets width of the last added element. Optionally sets alignment.
	/// </summary>
	/// <param name="width">Width or/and min/max width.</param>
	/// <param name="alignX">Horizontal alignment. If not <c>null</c>, calls <see cref="Align(string, string)"/>.</param>
	/// <exception cref="ArgumentException">Invalid alignment string.</exception>
	public wpfBuilder Width(WBLength width, string alignX = null) {
		width.ApplyTo(Last, false);
		if (alignX != null) Align(alignX);
		return this;
	}
	
	/// <summary>
	/// Sets height of the last added element. Optionally sets alignment.
	/// </summary>
	/// <param name="height">Height or/and min/max height.</param>
	/// <param name="alignY">Vertical alignment. If not <c>null</c>, calls <see cref="Align(string, string)"/>.</param>
	/// <exception cref="ArgumentException">Invalid alignment string.</exception>
	public wpfBuilder Height(WBLength height, string alignY = null) {
		height.ApplyTo(Last, true);
		if (alignY != null) Align(null, alignY);
		return this;
	}
	
	/// <summary>
	/// Sets position of the last added element in <b>Canvas</b> panel. Optionally sets size.
	/// </summary>
	/// <param name="x"></param>
	/// <param name="y"></param>
	/// <param name="width">Width or/and min/max width.</param>
	/// <param name="height">Height or/and min/max height.</param>
	/// <exception cref="InvalidOperationException">Current panel is not <b>Canvas</b>.</exception>
	/// <remarks>
	/// Only in <see cref="Canvas"/> panel you can set position explicitly. In other panel types it is set automatically and can be adjusted with <see cref="Margin"/>, <see cref="Align"/>, container's <see cref="AlignContent"/>, etc.
	/// </remarks>
	public wpfBuilder XY(double x, double y, WBLength? width = null, WBLength? height = null) {
		var c = _ParentOfLastAsOrThrow<_Canvas>().LastDirect;
		Canvas.SetLeft(c, x);
		Canvas.SetTop(c, y);
		width?.ApplyTo(c, false);
		height?.ApplyTo(c, true);
		return this;
	}
	
	/// <summary>
	/// Docks the last added element in <see cref="DockPanel"/>.
	/// </summary>
	/// <param name="dock"></param>
	/// <exception cref="InvalidOperationException">Current panel is not <b>DockPanel</b>.</exception>
	public wpfBuilder Dock(Dock dock) {
		var c = _ParentOfLastAsOrThrow<_DockPanel>().LastDirect;
		DockPanel.SetDock(c, dock);
		return this;
	}
	
	/// <summary>
	/// Sets horizontal and/or vertical alignment of the last added element.
	/// </summary>
	/// <param name="x">Horizontal alignment.</param>
	/// <param name="y">Vertical alignment.</param>
	/// <exception cref="InvalidOperationException">Current panel is <b>Canvas</b>.</exception>
	public wpfBuilder Align(HorizontalAlignment? x = null, VerticalAlignment? y = null) {
		var c = Last;
		if (c.Parent is Canvas) throw new InvalidOperationException("Align() in Canvas panel.");
		if (x != null) c.HorizontalAlignment = x.Value;
		if (y != null) c.VerticalAlignment = y.Value;
		return this;
	}
	
	/// <summary>
	/// Sets horizontal and/or vertical alignment of the last added element.
	/// </summary>
	/// <param name="x">Horizontal alignment. String that starts with one of these letters, uppercase or lowercase: <c>L</c> (left), <c>R</c> (right), <c>C</c> (center), <c>S</c> (stretch).</param>
	/// <param name="y">Vertical alignment. String that starts with one of these letters, uppercase or lowercase: <c>T</c> (top), <c>B</c> (bottom), <c>C</c> (center), <c>S</c> (stretch).</param>
	/// <exception cref="InvalidOperationException">Current panel is <b>Canvas</b>.</exception>
	/// <exception cref="ArgumentException">Invalid alignment string.</exception>
	public wpfBuilder Align(string x = null, string y = null) => Align(_AlignmentFromStringX(x), _AlignmentFromStringY(y));
	
	HorizontalAlignment? _AlignmentFromStringX(string s, [CallerMemberName] string m_ = null)
		=> s.NE() ? default(HorizontalAlignment?) : (char.ToUpperInvariant(s[0]) switch { 'L' => HorizontalAlignment.Left, 'C' => HorizontalAlignment.Center, 'R' => HorizontalAlignment.Right, 'S' => HorizontalAlignment.Stretch, _ => throw new ArgumentException(m_ + "(x)") });
	
	VerticalAlignment? _AlignmentFromStringY(string s, [CallerMemberName] string m_ = null)
		=> s.NE() ? default(VerticalAlignment?) : (char.ToUpperInvariant(s[0]) switch { 'T' => VerticalAlignment.Top, 'C' => VerticalAlignment.Center, 'B' => VerticalAlignment.Bottom, 'S' => VerticalAlignment.Stretch, _ => throw new ArgumentException(m_ + "(y)") });
	
	/// <summary>
	/// Sets content alignment of the last added element.
	/// </summary>
	/// <param name="x">Horizontal alignment.</param>
	/// <param name="y">Vertical alignment.</param>
	/// <exception cref="InvalidOperationException">The last added element is not <b>Control</b>.</exception>
	public wpfBuilder AlignContent(HorizontalAlignment? x = null, VerticalAlignment? y = null) {
		var c = _LastAsControlOrThrow();
		if (x != null) c.HorizontalContentAlignment = x.Value;
		if (y != null) c.VerticalContentAlignment = y.Value;
		return this;
	}
	
	/// <summary>
	/// Sets content alignment of the last added element.
	/// </summary>
	/// <param name="x">Horizontal alignment. String like with <see cref="Align(string, string)"/>.</param>
	/// <param name="y">Vertical alignment.</param>
	/// <exception cref="InvalidOperationException">The last added element is not <b>Control</b>.</exception>
	/// <exception cref="ArgumentException">Invalid alignment string.</exception>
	public wpfBuilder AlignContent(string x = null, string y = null) => AlignContent(_AlignmentFromStringX(x), _AlignmentFromStringY(y));
	
	/// <summary>
	/// Sets margin of the last added element.
	/// </summary>
	public wpfBuilder Margin(Thickness margin) {
		Last.Margin = margin;
		return this;
	}
	
	/// <summary>
	/// Sets margin of the last added element.
	/// </summary>
	public wpfBuilder Margin(double? left = null, double? top = null, double? right = null, double? bottom = null) {
		var c = Last;
		var p = c.Margin;
		left ??= p.Left;
		top ??= p.Top;
		right ??= p.Right;
		bottom ??= p.Bottom;
		c.Margin = new Thickness(left.Value, top.Value, right.Value, bottom.Value);
		return this;
	}
	
	/// <summary>
	/// Sets margin of the last added element.
	/// </summary>
	/// <param name="margin">
	/// String containing uppercase or lowercase letters for margin sides (<c>L</c>, <c>T</c>, <c>R</c>, <c>B</c>) optionally followed by a number (default 0) and optionally separated by spaces. Or just single number, to set all sides equal.
	/// Examples: <c>"tb"</c> (top 0, bottom 0), <c>"L5 R15"</c> (left 5, right 15), <c>"2"</c> (all sides 2).
	/// </param>
	/// <exception cref="ArgumentException">Invalid string.</exception>
	public wpfBuilder Margin(string margin) {
		var c = Last;
		var m = c.Margin;
		_ThicknessFromString(ref m, margin);
		c.Margin = m;
		return this;
	}
	
	static void _ThicknessFromString(ref Thickness t, string s, [CallerMemberName] string m_ = null) {
		if (s.NE()) return;
		if (s.ToInt(out int v1, 0, out int e1) && e1 == s.Length) {
			t = new Thickness(v1);
			return;
		}
		
		for (int i = 0; i < s.Length; i++) {
			var c = s[i]; if (c == ' ') continue;
			int v = s.ToInt(i + 1, out int end); if (end > 0) i = end - 1; //never mind: should be double. Currently we don't have a function that can recognize and convert part of string to double.
			switch (c) {
			case 't': case 'T': t.Top = v; break;
			case 'b': case 'B': t.Bottom = v; break;
			case 'l': case 'L': t.Left = v; break;
			case 'r': case 'R': t.Right = v; break;
			default: throw new ArgumentException(m_ + "()");
			}
		}
	}
	
	/// <summary>
	/// Sets padding of the last added control.
	/// </summary>
	/// <exception cref="InvalidOperationException">The last added element does not have <b>Padding</b> property.</exception>
	public wpfBuilder Padding(Thickness thickness) {
		new _PropGetSet<Thickness>(Last, "Padding").Set(thickness);
		return this;
	}
	
	/// <summary>
	/// Sets padding of the last added control.
	/// </summary>
	/// <exception cref="InvalidOperationException">The last added element does not have <b>Padding</b> property.</exception>
	public wpfBuilder Padding(double? left = null, double? top = null, double? right = null, double? bottom = null) {
		var c = new _PropGetSet<Thickness>(Last, "Padding");
		var p = c.Get;
		left ??= p.Left;
		top ??= p.Top;
		right ??= p.Right;
		bottom ??= p.Bottom;
		c.Set(new Thickness(left.Value, top.Value, right.Value, bottom.Value));
		return this;
	}
	
	/// <summary>
	/// Sets padding of the last added control.
	/// </summary>
	/// <param name="padding">
	/// String containing uppercase or lowercase letters for padding sides (<c>L</c>, <c>T</c>, <c>R</c>, <c>B</c>) optionally followed by a number (default 0) and optionally separated by spaces. Or just single number, to set all sides equal.
	/// Examples: <c>"tb"</c> (top 0, bottom 0), <c>"L5 R15"</c> (left 5, right 15), <c>"2"</c> (all sides 2).
	/// </param>
	/// <exception cref="InvalidOperationException">The last added element does not have <b>Padding</b> property.</exception>
	/// <exception cref="ArgumentException">Invalid string.</exception>
	public wpfBuilder Padding(string padding) {
		var c = new _PropGetSet<Thickness>(Last, "Padding");
		var p = c.Get;
		_ThicknessFromString(ref p, padding);
		c.Set(p);
		return this;
	}
	
	/// <summary>
	/// Sets <see cref="UIElement.IsEnabled"/> of the last added element.
	/// </summary>
	/// <param name="disabled">If <c>true</c> (default), sets <b>IsEnabled</b> = <c>false</c>, else sets <b>IsEnabled</b> = true.</param>
	public wpfBuilder Disabled(bool disabled = true) {
		Last.IsEnabled = !disabled;
		return this;
	}
	
	/// <summary>
	/// Sets <see cref="UIElement.Visibility"/> of the last added element.
	/// </summary>
	/// <param name="hidden">If <c>true</c> (default), sets <see cref="Visibility"/> <b>Hiden</b>; if <c>false</c> - <b>Visible</b>; if <c>null</c> - <b>Collapsed</b>.</param>
	public wpfBuilder Hidden(bool? hidden = true) {
		Last.Visibility = hidden switch { true => Visibility.Hidden, false => Visibility.Visible, _ => Visibility.Collapsed };
		return this;
	}
	
	/// <summary>
	/// Sets tooltip text/content/object of the last added element. See <see cref="FrameworkElement.ToolTip"/>.
	/// </summary>
	/// <param name="tooltip">Tooltip text (string), or tooltip content element, or <b>ToolTip</b> object.</param>
	/// <example>
	/// Text box with simple tooltip.
	/// <code><![CDATA[
	/// b.R.Add("Example", out TextBox _).Tooltip("Tooltip text");
	/// ]]></code>
	/// Tooltip with content created by another <b>wpfBuilder</b>.
	/// <code><![CDATA[
	/// //tooltip content
	/// var btt = new wpfBuilder()
	/// 	.R.Add<Image>().Image(icon.stock(StockIcon.INFO).ToWpfImage())
	/// 	.R.Add<TextBlock>().Text("Some ", "<b>text", ".")
	/// 	.End();
	/// //dialog
	/// var b = new wpfBuilder("Window").WinSize(300);
	/// b.R.AddButton("Example", null).Tooltip(btt.Panel);
	/// b.R.AddOkCancel();
	/// b.End();
	/// if (!b.ShowDialog()) return;
	/// ]]></code>
	/// </example>
	public wpfBuilder Tooltip(object tooltip) {
		Last.ToolTip = tooltip;
		if (!_opt_showToolTipOnKeyboardFocus) ToolTipService.SetShowsToolTipOnKeyboardFocus(Last, false);
		return this;
	}
	//FUTURE: make easier to create tooltip content, eg Inlines of TextBlock. Would be good to create on demand.
	//FUTURE: hyperlinks in tooltip. Now does not work because tooltip closes when mouse leaves the element.
	
	/// <summary>
	/// Sets UI Automation name of the last added element.
	/// </summary>
	public wpfBuilder UiaName(string name) {
		Last.UiaSetName(name);
		return this;
	}
	
	/// <summary>
	/// Sets background and/or foreground brush (color, gradient, etc) of the last added element.
	/// </summary>
	/// <param name="background">Background brush. See <see cref="Brushes"/>, <see cref="SystemColors"/>. Descendants usually inherit this property.</param>
	/// <param name="foreground">Foreground brush. Usually sets text color. Descendants usually override this property.</param>
	/// <exception cref="NotSupportedException">Last added element must be <b>Control</b>, <b>Panel</b>, <b>Border</b> or <b>TextBlock</b>. With <i>foreground</i> only <b>Control</b> or <b>TextBlock</b>.</exception>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add<Label>("Example1").Brush(Brushes.Cornsilk, Brushes.Green).Border(Brushes.BlueViolet, 1);
	/// b.R.Add<Label>("Example2").Brush(new LinearGradientBrush(Colors.Chocolate, Colors.White, 0));
	/// ]]></code>
	/// </example>
	public wpfBuilder Brush(Brush background = null, Brush foreground = null) { //named not Colors because: 1. Can set other brush than color, eg gradient. 2. Rarely used and in autocompletion lists is above Columns.
		var last = Last;
		if (foreground != null) {
			new _PropGetSet<Brush>(Last, "Foreground").Set(foreground);
		}
		if (background != null) {
			if (last == _p.panel && !_IsNested && _window != null) last = _window;
			new _PropGetSet<Brush>(Last, "Background").Set(background);
		}
		return this;
	}
	
	/// <summary>
	/// Sets background and/or foreground color of the last added element.
	/// </summary>
	/// <exception cref="NotSupportedException">Last added element must be <b>Control</b>, <b>Panel</b>, <b>Border</b> or <b>TextBlock</b>. With <i>foreground</i> only <b>Control</b> or <b>TextBlock</b>.</exception>
	public wpfBuilder Brush(ColorInt? background = null, ColorInt? foreground = null)
		=> Brush(background == null ? null : new SolidColorBrush((Color)background.Value), foreground == null ? null : new SolidColorBrush((Color)foreground.Value));
	
	/// <summary>
	/// Sets border properties of the last added element, which can be <b>Border</b> or a <b>Control</b>-derived class.
	/// </summary>
	/// <param name="color">Border color brush. If <c>null</c>, uses <b>SystemColors.ActiveBorderBrush</b>.</param>
	/// <param name="thickness">Border thickness. Ignored if <i>thickness2</i> not <c>null</c>.</param>
	/// <param name="padding">Sets the <b>Padding</b> property.</param>
	/// <param name="cornerRadius">Sets <see cref="Border.CornerRadius"/>. If used, the last added element must be <b>Border</b>.</param>
	/// <param name="thickness2">Border thickness to use instead of <i>thickness</i>. Allows to set non-uniform thickness.</param>
	/// <exception cref="NotSupportedException">Last added element must be <b>Control</b> or <b>Border</b>. With <i>cornerRadius</i> only <b>Border</b>.</exception>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add<Label>("Example1").Border(Brushes.BlueViolet, 1, new(5)).Brush(Brushes.Cornsilk, Brushes.Green);
	/// b.R.Add<Border>().Border(Brushes.Blue, 2, cornerRadius: 3).Add<Label>("Example2", WBAdd.ChildOfLast);
	/// ]]></code>
	/// </example>
	public wpfBuilder Border(Brush color = null, double thickness = 1d, Thickness? padding = null, double? cornerRadius = null, Thickness? thickness2 = null) {
		color ??= SystemColors.ActiveBorderBrush;
		switch (Last) {
		case Control c:
			if (cornerRadius != null) throw new NotSupportedException("Border(): Last added must be Border, or cornerRadius null");
			c.BorderBrush = color;
			c.BorderThickness = thickness2 ?? new Thickness(thickness);
			if (padding != null) c.Padding = padding.Value;
			break;
		case Border c:
			c.BorderBrush = color;
			c.BorderThickness = thickness2 ?? new Thickness(thickness);
			if (padding != null) c.Padding = padding.Value;
			if (cornerRadius != null) c.CornerRadius = new CornerRadius(cornerRadius.Value);
			break;
		default: throw new NotSupportedException("Border(): Last added must be Control or Border");
		}
		return this;
		//tested: there are no other useful types that have these properties.
	}
	
	/// <param name="color">Border color.</param>
	/// <inheritdoc cref="Border(Brush, double, Thickness?, double?, Thickness?)"/>
	public wpfBuilder Border(ColorInt color, double thickness = 1d, Thickness? padding = null, double? cornerRadius = null, Thickness? thickness2 = null)
		=> Border(new SolidColorBrush((Color)color), thickness, padding, cornerRadius, thickness2);
	
	/// <summary>
	/// Sets font properties of the last added element and its descendants.
	/// </summary>
	/// <param name="name">If not <c>null</c>, sets font name.</param>
	/// <param name="size">If not <c>null</c>, sets font size.</param>
	/// <param name="bold">If not <c>null</c>, sets font bold or not.</param>
	/// <param name="italic">If not <c>null</c>, sets font italic or not.</param>
	public wpfBuilder Font(string name = null, double? size = null, bool? bold = null, bool? italic = null) {
		var c = Last;
		if (name != null) TextElement.SetFontFamily(c, new FontFamily(name));
		if (size != null) TextElement.SetFontSize(c, size.Value);
		if (bold != null) TextElement.SetFontWeight(c, bold == true ? FontWeights.Bold : FontWeights.Normal);
		if (italic != null) TextElement.SetFontStyle(c, italic == true ? FontStyles.Italic : FontStyles.Normal);
		return this;
		//rejected: FontStretch? stretch=null. Rarely used. Most fonts don't support.
		
		//not sure is this is OK or should set font properties for each supporting class separately.
	}
	
	/// <summary>
	/// Sets <b>TextWrapping</b> property of the last added element.
	/// Supports <b>TextBlock</b>, <b>TextBox</b> and <b>AccessText</b>.
	/// </summary>
	public wpfBuilder Wrap(TextWrapping wrapping) {
		switch (Last) {
		case TextBlock t: t.TextWrapping = wrapping; break;
		case TextBox t: t.TextWrapping = wrapping; break;
		case AccessText t: t.TextWrapping = wrapping; break;
		default: throw new NotSupportedException("Wrap(): Last added must be TextBlock, TextBox or AccessText");
		}
		return this;
	}
	
	/// <summary>
	/// Sets <b>TextWrapping</b> property of the last added element = <b>TextWrapping.Wrap</b> if <c>true</c> (default), else or <b>TextWrapping.NoWrap</b>.
	/// Supports <b>TextBlock</b>, <b>TextBox</b> and <b>AccessText</b>.
	/// </summary>
	public wpfBuilder Wrap(bool wrap = true) => Wrap(wrap ? TextWrapping.Wrap : TextWrapping.NoWrap);
	
	/// <summary>
	/// Attempts to set focus to the last added element when it'll become visible.
	/// </summary>
	public wpfBuilder Focus() {
		Last.Focus();
		return this;
	}
	
	/// <summary>
	/// Sets <b>Tag</b> property of the last added element.
	/// </summary>
	public wpfBuilder Tag(object tag) {
		Last.Tag = tag;
		return this;
	}
	
	/// <summary>
	/// Sets <see cref="FrameworkElement.DataContext"/> property of the last added element.
	/// Then with <see cref="Bind"/> of this and descendant elements don't need to specify data source object because it is set by this function.
	/// </summary>
	/// <param name="source">Data source object.</param>
	public wpfBuilder BindingContext(object source) {
		Last.DataContext = source;
		return this;
	}
	
	/// <summary>
	/// Calls <see cref="FrameworkElement.SetBinding(DependencyProperty, string)"/> of the last added element.
	/// </summary>
	/// <param name="property">Element's dependency property, for example <c>TextBox.TextProperty</c>.</param>
	/// <param name="path">Source property name or path, for example <c>nameof(MyData.Property)</c>. Source object should be set with <see cref="BindingContext"/>.</param>
	public wpfBuilder Bind(DependencyProperty property, string path) {
		Last.SetBinding(property, path);
		return this;
	}
	
	/// <summary>
	/// Calls <see cref="FrameworkElement.SetBinding(DependencyProperty, BindingBase)"/> of the last added element.
	/// </summary>
	/// <param name="property">Element's dependency property, for example <c>TextBox.TextProperty</c>.</param>
	/// <param name="binding">A binding object, for example <c>new Binding(nameof(MyData.Property))</c> or <c>new Binding(nameof(MyData.Property)) { Source = dataObject }</c>. In the first case, source object should be set with <see cref="BindingContext"/>.</param>
	public wpfBuilder Bind(DependencyProperty property, BindingBase binding) {
		Last.SetBinding(property, binding);
		return this;
	}
	
	/// <summary>
	/// Calls <see cref="FrameworkElement.SetBinding(DependencyProperty, BindingBase)"/> of the last added element and gets its return value.
	/// </summary>
	/// <param name="property">Element's dependency property, for example <c>TextBox.TextProperty</c>.</param>
	/// <param name="binding">A binding object.</param>
	/// <param name="r">The return value of <b>SetBinding</b>.</param>
	public wpfBuilder Bind(DependencyProperty property, BindingBase binding, out BindingExpressionBase r) {
		r = Last.SetBinding(property, binding);
		return this;
	}
	
	/// <summary>
	/// Calls <see cref="FrameworkElement.SetBinding(DependencyProperty, BindingBase)"/> of the last added element. Creates <see cref="Binding"/> that uses <i>source</i> and <i>path</i>.
	/// </summary>
	/// <param name="property">Element's dependency property, for example <c>TextBox.TextProperty</c>.</param>
	/// <param name="source">Data source object.</param>
	/// <param name="path">Source property name or path, for example <c>nameof(MyData.Property)</c>.</param>
	public wpfBuilder Bind(DependencyProperty property, object source, string path) {
		var binding = new Binding(path) { Source = source };
		Last.SetBinding(property, binding);
		return this;
	}
	
	/// <summary>
	/// Sets a validation callback function for the last added element.
	/// </summary>
	/// <param name="func">Function that returns an error string if element's value is invalid, else returns <c>null</c>.</param>
	/// <remarks>
	/// The callback function will be called when clicked button <b>OK</b> or <b>Apply</b> or a button added with flag <see cref="WBBFlags.Validate"/>.
	/// If it returns a non-<c>null</c> string, the window stays open and button's <i>click</i> callback not called. The string is displayed in a tooltip.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// var b = new wpfBuilder("Window").WinSize(300);
	/// b.R.Add("Name", out TextBox tName)
	/// 	.Validation(o => string.IsNullOrWhiteSpace(tName.Text) ? "Name cannot be empty" : null);
	/// b.R.Add("Count", out TextBox tCount)
	/// 	.Validation(o => int.TryParse(tCount.Text, out int i1) && i1 >= 0 && i1 <= 100 ? null : "Count must be 0-100");
	/// b.R.AddOkCancel();
	/// b.End();
	/// if (!b.ShowDialog()) return;
	/// print.it(tName.Text, tCount.Text.ToInt());
	/// ]]></code>
	/// </example>
	public wpfBuilder Validation(Func<FrameworkElement, string> func/*, DependencyProperty property=null*/) {
		var c = Last;
		//validate on click of OK or some other button. Often eg text fields initially are empty and must be filled.
		(_validations ??= new List<_Validation>()).Add(new _Validation { e = c, func = func });
		
		//rejected: also validate on lost focus or changed property value. Maybe in the future.
		//		if(property==null) {
		//			c.LostFocus+=(o,e)=> { print.it(func(o as FrameworkElement)); };
		//		} else {
		//			var pd = DependencyPropertyDescriptor.FromProperty(property, c.GetType());
		//			pd.AddValueChanged(c, (o,e)=> { print.it(func(o as FrameworkElement)); });
		//		}
		return this;
	}
	
	class _Validation {
		public FrameworkElement e;
		public Func<FrameworkElement, string> func;
	}
	
	List<_Validation> _validations;
	
	bool _Validate(Window w, Button b) {
		TextBlock tb = null;
		foreach (var gb in _GetAllWpfBuilders(w)) { //find all wpfBuilder used to build this window
			if (gb._validations == null) continue;
			foreach (var v in gb._validations) {
				var e = v.e;
				var s = v.func(e); if (s == null) continue;
				if (tb == null) tb = new TextBlock(); else tb.Inlines.Add(new LineBreak());
				var h = new Hyperlink(new Run(s));
				h.Click += (o, y) => {
					if (_FindAncestorTabItem(e, out var ti)) ti.IsSelected = true; //SHOULDDO: support other cases too, eg other tabcontrol-like control class or tabcontrol in tabcontrol.
					timer.after(1, _ => { //else does not focus etc if was in hidden tab page
						try {
							e.BringIntoView();
							e.Focus();
						}
						catch { }
						//catch(Exception e1) { print.it(e1); }
					});
				};
				tb.Inlines.Add(h);
			}
		}
		if (tb == null) return true;
		var tt = new ToolTip { Content = tb, StaysOpen = false, PlacementTarget = b, Placement = PlacementMode.Bottom };
		//var tt=new Popup { Child=tb, StaysOpen=false, PlacementTarget=b, Placement= PlacementMode.Bottom }; //works, but black etc
		tt.IsOpen = true;
		//never mind: could add eg red rectangle, like WPF does on binding validation error. Not easy.
		return false;
	}
	
	static List<wpfBuilder> _GetAllWpfBuilders(DependencyObject root) {
		var a = new List<wpfBuilder>();
		_Enum(root, 0);
		void _Enum(DependencyObject parent, int level) {
			foreach (var o in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>()) {
				//print.it(new string(' ', level) + o);
				if (o is Panel p && s_cwt.TryGetValue(p, out var gb)) a.Add(gb);
				_Enum(o, level + 1);
			}
		}
		return a;
	}
	
	static bool _FindAncestorTabItem(DependencyObject e, out TabItem ti) {
		ti = null;
		for (; ; )
		{
			switch (e = LogicalTreeHelper.GetParent(e)) {
			case null: return false;
			case TabItem t: ti = t; return true;
			}
		}
	}
	
	/// <summary>
	/// Sets <see cref="FrameworkElement.Name"/> of the last added element.
	/// </summary>
	/// <param name="name">Name. Must start with a letter or <c>_</c>, and contain only letters, digits and <c>_</c>.</param>
	/// <param name="andUia">Also set UI Automation name (<see cref="UiaName"/>).</param>
	/// <exception cref="ArgumentException">Invalid name.</exception>
	/// <remarks>
	/// The <b>Name</b> property can be used to identify the element in code. It also sets the UIA <b>AutomationId</b> (regardless of <i>andUia</i>). It isn't displayed in UI.
	/// </remarks>
	public wpfBuilder Name(string name, bool andUia = false) {
		Last.Name = name;
		if (andUia) UiaName(name);
		return this;
	}
	
	/// <summary>
	/// Makes an element behave as a label of the last added element (<see cref="Last"/>).
	/// </summary>
	/// <param name="label">The label element. Usually <b>Label</b> or <b>TextBlock</b>, but can be any element.</param>
	/// <remarks>
	/// Sets <i>label</i>'s <see cref="Label.Target"/> if it's <b>Label</b>. Calls <see cref="System.Windows.Automation.AutomationProperties.SetLabeledBy"/>. Applies the <i>bindLabelVisibility</i> option (see <see cref="Options"/>).
	/// </remarks>
	public wpfBuilder LabeledBy(FrameworkElement label) {
		var e = Last;
		if (label is Label la) la.Target = e;
		System.Windows.Automation.AutomationProperties.SetLabeledBy(e, label);
		if (_opt_bindLabelVisibility) label?.SetBinding(UIElement.VisibilityProperty, new Binding("Visibility") { Source = e, Mode = BindingMode.OneWay });
		return this;
	}
	
	/// <summary>
	/// Makes <see cref="Last2"/> behave as a label of <see cref="Last"/>.
	/// </summary>
	public wpfBuilder LabeledBy() => LabeledBy(Last2);
	
	/// <summary>
	/// Sets watermark/hint/cue text of the last added <b>TextBox</b> or editable <b>ComboBox</b> control.
	/// The text is visible only when the control text is empty.
	/// </summary>
	/// <param name="text">Watermark text.</param>
	/// <remarks>
	/// The control must be a child/descendant of an <b>AdornerDecorator</b>. See example.
	/// </remarks>
	/// <exception cref="NotSupportedException">The last added element isn't <b>TextBox</b> or editable <b>ComboBox</b> control.</exception>
	/// <exception cref="InvalidOperationException">The control isn't in an <b>AdornerDecorator</b>.</exception>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add<AdornerDecorator>().Add(out TextBox text1, flags: WBAdd.ChildOfLast).Watermark("Water");
	/// ]]></code>
	/// More examples in Cookbook.
	/// </example>
	public wpfBuilder Watermark(string text) => Watermark(out _, text);
	
	/// <param name="adorner">Receives the adorner. It can be used to change watermark text later.</param>
	/// <inheritdoc cref="Watermark(string)"/>
	public wpfBuilder Watermark(out WatermarkAdorner adorner, string text) {
		var c = Last as Control;
		if (c is not TextBox && !(c is ComboBox k && k.IsEditable)) throw new NotSupportedException("Watermark(): Last added must be TextBox or editable ComboBox");
		adorner = new WatermarkAdorner(c, text);
		adorner.SetAdornerVisibility();
		return this;
	}
	
	#endregion
	
	#region set type-specific properties of last added element
	
	/// <summary>
	/// Sets <see cref="ToggleButton.IsChecked"/> and <see cref="ToggleButton.IsThreeState"/> of the last added check box or radio button.
	/// </summary>
	/// <param name="check"></param>
	/// <param name="threeState"></param>
	/// <exception cref="NotSupportedException">The last added element is not <b>ToggleButton</b>.</exception>
	public wpfBuilder Checked(bool? check = true, bool threeState = false) {
		var c = Last as ToggleButton ?? throw new NotSupportedException("Checked(): Last added element must be CheckBox or RadioButton");
		c.IsThreeState = threeState;
		c.IsChecked = check;
		return this;
	}
	
	/// <summary>
	/// Sets <see cref="ToggleButton.IsChecked"/> of the specified <see cref="RadioButton"/>.
	/// </summary>
	/// <param name="check"></param>
	/// <param name="control"></param>
	/// <remarks>
	/// Unlike other similar functions, does not use <see cref="Last"/>.
	/// </remarks>
	public wpfBuilder Checked(bool check, RadioButton control) {
		control.IsChecked = check;
		return this;
	}
	
	/// <summary>
	/// Sets <see cref="TextBoxBase.IsReadOnly"/> or <see cref="ComboBox.IsReadOnly"/> of the last added text box or editable combo box.
	/// </summary>
	/// <param name="readOnly"></param>
	/// <exception cref="NotSupportedException">The last added element is not <b>TextBoxBase</b> or <b>ComboBox</b>.</exception>
	public wpfBuilder Readonly(bool readOnly = true) { //rejected: , bool caretVisible=false. Not useful.
		switch (Last) {
		case TextBoxBase c:
			c.IsReadOnly = readOnly;
			//c.IsReadOnlyCaretVisible=caretVisible;
			break;
		case ComboBox c:
			c.IsReadOnly = readOnly;
			break;
		default: throw new NotSupportedException("Readonly(): Last added must be TextBox, RichTextBox or ComboBox");
		}
		return this;
	}
	
	/// <summary>
	/// Makes the last added <see cref="TextBox"/> multiline.
	/// </summary>
	/// <param name="height">If not <c>null</c>, sets height or/and min/max height.</param>
	/// <param name="wrap">Sets <see cref="TextBox.TextWrapping"/>.</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>TextBox</b>.</exception>
	public wpfBuilder Multiline(WBLength? height = null, TextWrapping wrap = TextWrapping.WrapWithOverflow) {
		var c = Last as TextBox ?? throw new NotSupportedException("Multiline(): Last added must be TextBox");
		c.AcceptsReturn = true;
		c.TextWrapping = wrap;
		c.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		c.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
		height?.ApplyTo(c, true);
		return this;
	}
	
	/// <summary>
	/// Makes the last added <see cref="ComboBox"/> editable.
	/// </summary>
	/// <exception cref="NotSupportedException">The last added element is not <b>ComboBox</b>.</exception>
	public wpfBuilder Editable() {
		var c = Last as ComboBox ?? throw new NotSupportedException("Editable(): Last added must be ComboBox");
		c.IsEditable = true;
		if (_opt_modifyPadding) c.Padding = new Thickness(2, 1, 2, 2); //default (2) or set by _Add() for non-editable
		return this;
	}
	
	/// <summary>
	/// Splits string and ads substrings as items to the last added <see cref="ItemsControl"/> (<see cref="ComboBox"/>, etc).
	/// </summary>
	/// <param name="items">String like <c>"One|Two|Three"</c>.</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>ItemsControl</b>.</exception>
	/// <remarks>
	/// If it is a non-editable <b>ComboBox</b>, selects the first item. See also <see cref="Select"/>.
	/// </remarks>
	public wpfBuilder Items(string items) => _Items(items.Split('|'), null);
	
	/// <summary>
	/// Adds items of any type to the last added <see cref="ItemsControl"/> (<see cref="ComboBox"/>, etc).
	/// </summary>
	/// <param name="items">Items of any type (<b>string</b>, WPF element).</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>ItemsControl</b>.</exception>
	/// <remarks>
	/// If it is a non-editable <b>ComboBox</b>, selects the first item. See also <see cref="Select"/>.
	/// </remarks>
	public wpfBuilder Items(params object[] items) => _Items(items, null);
	
	wpfBuilder _Items(object[] a, IEnumerable e) {
		var ic = Last as ItemsControl ?? throw new NotSupportedException("Items(): Last added must be ItemsControl, for example ComboBox");
		ic.ItemsSource = null;
		if (a != null) {
			ic.Items.Clear();
			foreach (var v in a) ic.Items.Add(v);
		} else if (e != null) {
			ic.ItemsSource = e;
		}
		if (Last is ComboBox cb && !cb.IsEditable && cb.HasItems) cb.SelectedIndex = 0;
		return this;
	}
	
	/// <summary>
	/// Adds items as <b>IEnumerable</b> to the last added <see cref="ItemsControl"/> (<see cref="ComboBox"/>, etc), with "lazy" option.
	/// </summary>
	/// <param name="items">An <b>IEnumerable</b> that contains items (eg array, <b>List</b>) or generates items (eg returned from a yield-return function).</param>
	/// <param name="lazy">Retrieve items when (if) showing the dropdown part of the <b>ComboBox</b> first time.</param>
	/// <exception cref="NotSupportedException">
	/// - The last added element is not <b>ItemsControl</b>.
	/// - <i>lazy</i> is <c>true</c> and the last added element is not <b>ComboBox</b>.
	/// </exception>
	public wpfBuilder Items(IEnumerable items, bool lazy = false) => lazy ? Items(true, o => o.ItemsSource = items) : _Items(null, items);
	
	/// <summary>
	/// Sets callback function that should add items to the last added <see cref="ComboBox"/> later.
	/// </summary>
	/// <param name="once">Call the function once. If <c>false</c>, calls on each drop down.</param>
	/// <param name="onDropDown">Callback function that should add items. Called before showing the dropdown part of the <b>ComboBox</b>. Don't need to clear old items.</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>ComboBox</b>.</exception>
	public wpfBuilder Items(bool once, Action<ComboBox> onDropDown) {
		var c = Last as ComboBox ?? throw new NotSupportedException("Items(): Last added must be ComboBox");
		EventHandler d = null;
		d = (_, _) => {
			if (once) c.DropDownOpened -= d;
			if (c.ItemsSource != null) c.ItemsSource = null; else c.Items.Clear();
			onDropDown(c);
		};
		c.DropDownOpened += d;
		return this;
	}
	
	/// <summary>
	/// Selects an item of the last added <see cref="Selector"/> (<see cref="ComboBox"/>, etc).
	/// </summary>
	/// <param name="index">0-based item index</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>Selector</b>.</exception>
	/// <seealso cref="Items"/>
	public wpfBuilder Select(int index) {
		var c = Last as Selector ?? throw new NotSupportedException("Items(): Last added must be Selector, for example ComboBox or ListBox");
		c.SelectedIndex = index;
		return this;
	}
	
	/// <summary>
	/// Selects an item of the last added <see cref="Selector"/> (<see cref="ComboBox"/>, etc).
	/// </summary>
	/// <param name="item">An added item.</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>Selector</b>.</exception>
	/// <seealso cref="Items"/>
	public wpfBuilder Select(object item) {
		var c = Last as Selector ?? throw new NotSupportedException("Items(): Last added must be Selector, for example ComboBox or ListBox");
		c.SelectedItem = item;
		return this;
	}
	
	/// <summary>
	/// Obsolete. Use <b>FormatText</b>.
	/// Adds inlines to the last added <see cref="TextBlock"/>.
	/// </summary>
	/// <param name="inlines">
	/// Arguments of type:
	/// <br/>• string like <c>"&lt;b>text"</c>, <c>"&lt;i>text"</c> or <c>"&lt;u>text"</c> adds inline of type <b>Bold</b>, <b>Italic</b> or <b>Underline</b>.
	/// <br/>• string like <c>"&lt;a>text"</c> adds <see cref="Hyperlink"/>. Next argument of type <b>Action</b> or <b>Action&lt;Hyperlink&gt;</b> sets its action.
	/// <br/>• other string - plain text.
	/// <br/>• <see cref="WBLink"/> adds a hyperlink.
	/// <br/>• <see cref="Inline"/> of any type, eg <b>Run</b>, <b>Bold</b>, <b>Hyperlink</b>.
	/// <br/>• <see cref="UIElement"/>.
	/// </param>
	/// <exception cref="NotSupportedException">The last added element is not <b>TextBlock</b>.</exception>
	/// <exception cref="ArgumentException">Unsupported argument type.</exception>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add<TextBlock>().Text(
	/// 	"Text ", "<b>bold ", "\n",
	/// 	new WBLink("libreautomate", "https://www.libreautomate.com"), ", ",
	/// 	new WBLink("Action", _ => print.it("click"), bold: true), "\n",
	/// 	new Run("color") { Foreground = Brushes.Blue, Background = Brushes.Cornsilk, FontSize = 20 }, "\n",
	/// 	"controls", new TextBox() { MinWidth = 100, Height = 20, Margin = new(3) }, new CheckBox() { Content = "Check" }, "\n",
	/// 	"image", ImageUtil.LoadWpfImageElement("*PixelartIcons.Notes #0060F0")
	/// 	);
	/// ]]></code>
	/// </example>
	[EditorBrowsable(EditorBrowsableState.Never)] //obsolete
	public wpfBuilder Text(params object[] inlines) {
		var c = Last as TextBlock ?? throw new NotSupportedException("Text(): Last added must be TextBlock");
		var k = c.Inlines;
		k.Clear();
		Hyperlink link = null;
		foreach (var v in inlines) {
			Inline n = null; int i;
			switch (v) {
			case WBLink x:
				n = x.Hlink;
				break;
			case Hyperlink x:
				n = link = x;
				break;
			case Inline x:
				n = x;
				break;
			case Action x when link != null: //<a> fbc
				link.Click += (o, e) => x();
				continue;
			case Action<Hyperlink> x when link != null: //<a> fbc
				link.Click += (o, e) => x(o as Hyperlink);
				continue;
			case UIElement x:
				n = new InlineUIContainer(x) { BaselineAlignment = BaselineAlignment.Center };
				break;
			case string x:
				if (x.Starts('<') && (i = x.Starts(false, "<a>", "<b>", "<i>", "<u>")) > 0) {
					var run = new Run(x[3..]);
					switch (i) {
					case 1: n = link = new Hyperlink(run); break;
					case 2: n = new Bold(run); break;
					case 3: n = new Italic(run); break;
					case 4: n = new Underline(run); break;
					}
				} else {
					k.Add(x);
					continue;
				}
				break;
			default: throw new ArgumentException("Text(): unsupported argument type");
			}
			k.Add(n);
		}
		return this;
	}
	
	/// <summary>
	/// Adds inlines (text, formatted text, hyperlinks, images, etc) to the last added <see cref="TextBlock"/> etc.
	/// </summary>
	/// <param name="text">
	/// Interpolated string (like <c>$"string"</c>) with tags etc. The format is XML without root element.
	/// <para>
	/// These tags add inlines of these types:
	/// <br/>• <c>&lt;b>text&lt;/b></c> - <b>Bold</b>.
	/// <br/>• <c>&lt;i>text&lt;/i></c> - <b>Italic</b>.
	/// <br/>• <c>&lt;u>text&lt;/u></c> - <b>Underline</b>.
	/// <br/>• <c>&lt;s>text&lt;/s></c> - <b>Span</b>.
	/// <br/>• <c>&lt;s {Span}>text&lt;/s></c> - <b>Span</b> or a <b>Span</b>-based type. The function adds <c>text</c> to its <b>Inlines</b> collection.
	/// <br/>• <c>&lt;a {Action or Action&lt;Hyperlink>}>text&lt;/a></c> - <b>Hyperlink</b> that calls the action.
	/// <br/>• <c>&lt;a href='URL or path etc'>text&lt;/a></c> - <b>Hyperlink</b> that calls <see cref="run.itSafe"/>.
	/// </para>
	/// <para>
	/// Tags can have these attributes, like <c>&lt;s c='red' FontSize = '20'>text&lt;/s></c>:
	/// <br/>• <c>c</c> or <c>Foreground</c> - text color, like <c>'red'</c> or <c>'#AARRGGBB'</c> or <c>'#RRGGBB'</c> or <c>'#ARGB'</c> or <c>'#RGB'</c>.
	/// <br/>• <c>b</c> or <c>Background</c> - background color.
	/// <br/>• <c>FontFamily</c> - font name, like <c>'Consolas'</c>.
	/// <br/>• <c>FontSize</c> - font size, like <c>'20'</c>.
	/// </para>
	/// <para>
	/// WPF elements of these types can be inserted without tags:
	/// <br/>• <c>{Inline}</c> - any inline, eg <b>Run</b>. See also <c>&lt;s {Span}>text&lt;/s></c>.
	/// <br/>• <c>{UIElement}</c> - a WPF element, eg <b>CheckBox</b> or <b>Image</b>.
	/// <br/>• <c>{ImageSource}</c> - adds <b>Image</b>.
	/// <br/>• <c>{IEnumerable&lt;Inline>}</c> - adds multiple <b>Inline</b>.
	/// </para>
	/// XML special characters must be escaped:
	/// <br/>• <c>&lt;</c> - <c>&amp;lt;</c>.
	/// <br/>• <c>&amp;</c> - <c>&amp;amp;</c>.
	/// <br/>• <c>'</c>, <c>"</c> - <c>&amp;apos;</c> in <c>'attribute'</c> or <c>&amp;quot;</c> in <c>"attribute"</c>.
	/// <para>
	/// </para>
	/// The <c>text</c> in above examples can contain nested tags and elements.
	/// </param>
	/// <exception cref="NotSupportedException">Unsupported type of the last added element. Or supported type but non-empty <b>Content</b> and <b>Header</b> (read Remarks).</exception>
	/// <exception cref="ArgumentException">Unknown <c>&lt;tag></c> or unsupported <c>{object}</c> type.</exception>
	/// <exception cref="InvalidOperationException">The same <c>{Span}</c> or <c>{Inline}</c> object in multiple places.</exception>
	/// <exception cref="FormatException">Invalid color attribute.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="XElement.Parse"/>.</exception>
	/// <remarks>
	/// The last added element can be of type:
	/// <br/>• <see cref="TextBlock"/> - the function adds inlines to its <b>Inlines</b> collection.
	/// <br/>• <b>ContentControl</b> (eg <b>Label</b> or <b>Button</b>) - creates new <b>TextBlock</b> with inlines and sets its <b>Content</b> property if it is <c>null</c>. If <b>HeaderedContentControl</b> (eg <b>GroupBox</b>) and its <b>Header</b> property is <c>null</c>, sets <b>Header</b> instead.
	/// <br/>• <b>Panel</b> whose <b>Parent</b> is <b>HeaderedContentControl</b> (eg <c>b.StartGrid&lt;GroupBox>(null).FormatText($"...")</c>) - uses the <b>HeaderedContentControl</b> like the above.
	///
	/// For elements other than the last added use <see cref="formatTextOf(object, InterpolatedString)"/> or <see cref="formattedText(InterpolatedString)"/>.
	///
	/// To load images can be used <see cref="ImageUtil.LoadWpfImageElement"/> and <see cref="ImageUtil.LoadWpfImage"/>.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add<TextBlock>().FormatText($"""
	/// Text <b>bold</b> <i>italic <u>underline</u>.</i>
	/// <s c='GreenYellow' b='Black' FontFamily='Consolas' FontSize='20'>attributes</s>
	/// <s {new Span() { Foreground = Brushes.Red, Background = new LinearGradientBrush(Colors.GreenYellow, Colors.Transparent, 90) }}>Span object, <b>bold</b></s>
	/// <a href='https://www.example.com'>example.com</a> <b><a href='notepad.exe'>Notepad</a></b>
	/// <a {() => { print.it("click"); }}>click</a> <a {(Hyperlink h) => { print.it("click once"); h.IsEnabled = false; }}>click once</a>
	/// {new Run("Run object") { Foreground = Brushes.Blue, Background = Brushes.Goldenrod, FontSize = 20 }}
	/// Image {ImageUtil.LoadWpfImageElement("*PixelartIcons.Notes #0060F0")}<!-- or ImageUtil.LoadWpfImage(@"C:\Test\image.png") -->
	/// Controls {new TextBox() { MinWidth = 100, Height = 20, Margin = new(3) }} {new CheckBox() { Content = "Check" }}
	/// &lt; &gt; &amp; &apos; &quot;
	/// """);
	/// ]]></code>
	/// Build interpolated string at run time.
	/// <code><![CDATA[
	/// wpfBuilder.InterpolatedString s = new();
	/// s.AppendLiteral("Text <b>bold</b> <a ");
	/// s.AppendFormatted(() => { print.it("click"); });
	/// s.AppendLiteral(">link</a>.");
	/// b.R.Add<TextBlock>().FormatText(s);
	/// ]]></code>
	/// </example>
	public wpfBuilder FormatText(InterpolatedString text) {
		var s = text.GetFormattedText();
		_FormatText(Last, s, text.a);
		return this;
	}
	
	//rejected: overload `FormatText(string text)` that supports only tags where don't need {object}.
	
	/// <summary>
	/// Adds inlines (text, formatted text, hyperlinks, images, etc) to the specified <see cref="TextBlock"/> etc.
	/// </summary>
	/// <param name="obj">Object of type <see cref="TextBlock"/>, <b>ContentControl</b> or <b>InlineCollection</b>. More info in <see cref="FormatText(InterpolatedString)"/> remarks.</param>
	/// <exception cref="NotSupportedException">Unsupported <i>obj</i> type or non-empty <b>Content</b>/<b>Header</b>.</exception>
	/// <exception cref="ArgumentException">Unknown <c>&lt;tag></c> or unsupported <c>{object}</c> type.</exception>
	/// <exception cref="InvalidOperationException">The same <c>{Span}</c> or <c>{Inline}</c> object in multiple places.</exception>
	/// <exception cref="FormatException">Invalid color attribute.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="XElement.Parse"/>.</exception>
	/// <inheritdoc cref="FormatText(InterpolatedString)" path="/param"/>
	public static void formatTextOf(object obj, InterpolatedString text) {
		var s = text.GetFormattedText();
		_FormatText(obj, s, text.a);
	}
	
	/// <summary>
	/// Creates new <see cref="TextBlock"/> and adds inlines like <see cref="FormatText(InterpolatedString)"/>.
	/// </summary>
	/// <exception cref="ArgumentException">Unknown <c>&lt;tag></c> or unsupported <c>{object}</c> type.</exception>
	/// <exception cref="InvalidOperationException">The same <c>{Span}</c> or <c>{Inline}</c> object in multiple places.</exception>
	/// <exception cref="FormatException">Invalid color attribute.</exception>
	/// <exception cref="Exception">Exceptions of <see cref="XElement.Parse"/>.</exception>
	/// <inheritdoc cref="FormatText(InterpolatedString)" path="/param"/>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add(wpfBuilder.formattedText($"<b>Label</b>"), out TextBox _);
	/// b.R.AddButton(_TextWithIcon("Button", "*PixelartIcons.Notes #0060F0"), _ => { print.it("Button clicked"); });
	/// 
	/// static TextBlock _TextWithIcon(string text, string icon) {
	/// 	var e = ImageUtil.LoadWpfImageElement(icon);
	/// 	e.Margin = new(0, 0, 4, 0);
	/// 	return wpfBuilder.formattedText($"{e}{text}");
	/// }
	/// ]]></code>
	/// </example>
	public static TextBlock formattedText(InterpolatedString text) {
		var e = new TextBlock();
		var s = text.GetFormattedText();
		_FormatText(e, s, text.a);
		return e;
	}
	
	static void _FormatText(object obj, string text, List<object> a) {
		InlineCollection ic;
		g1:
		switch (obj) {
		case TextBlock k: ic = k.Inlines; break;
		case HeaderedContentControl k when k.Header == null: k.Header = obj = new TextBlock(); goto g1;
		case ContentControl k when k.Content == null: k.Content = obj = new TextBlock(); goto g1;
		case Panel k when k.Parent is HeaderedContentControl p1: obj = p1; goto g1; //eg b.StartGrid<GroupBox>(null).FormatText($"...")
		case InlineCollection k: ic = k; break;
		default: throw new NotSupportedException("Format(): unsupported element type");
		}
		ic.Clear();
		
		const string c_mark = InterpolatedString.c_mark;
		var xr = XElement.Parse("<x>" + text + "</x>", LoadOptions.PreserveWhitespace);
		_Enum(ic, xr);
		
		void _Enum(InlineCollection ic, XElement xp) {
			foreach (var n in xp.Nodes()) {
				if (n is XElement xe) _Element(xe);
				else if (n is XText xt) _Text(xt); //also XCData
			}
			
			void _Element(XElement x) {
				Span r = null;
				var tag = x.Name.LocalName;
				switch (tag) {
				case "b": r = new Bold(); break;
				case "i": r = new Italic(); break;
				case "u": r = new Underline(); break;
				case "s":
					if (_GetObj() is object o) { //<s {Span}>...</s>
						r = o as Span ?? throw new ArgumentException("Expected <s {Span}>");
						if (r.Parent != null) throw new InvalidOperationException("Reused {Span} object");
					} else { //<s attributes}>...</s>
						r = new();
					}
					break;
				case "a":
					var h = new Hyperlink();
					r = h;
					if (x.Attr("href") is string href) { //<a href='...'>
						h.Click += (_, _) => run.itSafe(href);
					} else { //<a {Action}>
						switch (_GetObj()) {
						case Action g: h.Click += (_, _) => g(); break;
						case Action<Hyperlink> g: h.Click += (_, _) => g(h); break;
						default: throw new ArgumentException("Expected <a {Action}> or <a href='...'>");
						}
					}
					break;
				default: throw new ArgumentException($"Unknown tag <{tag}>");
				}
				
				foreach (var at in x.Attributes()) {
					var v = at.Value;
					switch (at.Name.LocalName) {
					case "c" or "Foreground": r.Foreground = _Brush(v); break;
					case "b" or "Background": r.Background = _Brush(v); break;
					case "FontFamily": r.FontFamily = new(v); break;
					case "FontSize": if (v.ToNumber(out double fsize)) r.FontSize = fsize; break;
					}
				}
				static Brush _Brush(string v) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(v));
				
				ic.Add(r);
				if (x.HasElements) _Enum(r.Inlines, x);
				else r.Inlines.Add(x.Value);
				
				object _GetObj() => a != null && x.Attr("_a") is string s && s.Starts(c_mark) && s.ToInt(out int i1, c_mark.Length) ? a[i1] : null;
			}
			
			void _Text(XText x) {
				var s = x.Value;
				if (a != null) {
					int from = 0;
					for (; from < s.Length; from++) {
						int m = s.Find(c_mark, from);
						if (m < 0) break;
						if (m > from) ic.Add(s[from..m]);
						if (s.ToInt(out int i, m + c_mark.Length, out from)) {
							var k = a[i];
							g2:
							switch (k) {
							case Inline e:
								if (e.Parent != null) throw new InvalidOperationException("Reused {Inline} object");
								ic.Add(e);
								break;
							case UIElement e:
								ic.Add(new InlineUIContainer(e) { BaselineAlignment = BaselineAlignment.Center });
								break;
							case ImageSource e:
								k = new Image { Source = e, Stretch = Stretch.None };
								goto g2;
							case IEnumerable<Inline> e:
								ic.AddRange(e);
								break;
							default: throw new ArgumentException($"Unexpected element type {a[i]}");
							}
						}
					}
					if (from < s.Length) ic.Add(s[from..]);
				} else ic.Add(s);
			}
		}
	}
	
#pragma warning disable 1591 //no XML doc
	[InterpolatedStringHandler, NoDoc]
	public ref struct InterpolatedString {
		DefaultInterpolatedStringHandler _f;
		internal List<object> a;
		string _lit;
		internal const string c_mark = "≡∫∫≡";
		
		public InterpolatedString(int literalLength, int formattedCount) {
			_f = new(literalLength, formattedCount);
		}
		
		public InterpolatedString(int literalLength, int formattedCount, IFormatProvider provider) {
			_f = new(literalLength, formattedCount, provider);
		}
		
		public InterpolatedString(int literalLength, int formattedCount, IFormatProvider provider, Span<char> initialBuffer) {
			_f = new(literalLength, formattedCount, provider, initialBuffer);
		}
		
		public void AppendLiteral(string value)
			 => _f.AppendLiteral(_lit = value);
		
		public void AppendFormatted(string value)
			=> _f.AppendFormatted(value);
		
		public void AppendFormatted<T>(T value) {
			if (value is Delegate or DependencyObject or IEnumerable<Inline>) {
				bool q = _lit != null && _lit.AsSpan().TrimEnd() is [.., '<', 'a' or 's'];
				if (q) _f.AppendLiteral(" _a='");
				a ??= new();
				_f.AppendLiteral(c_mark + a.Count.ToS() + " ");
				a.Add(value);
				if (q) _f.AppendLiteral("'");
			} else _f.AppendFormatted(value);
			_lit = null;
		}
		
		public void AppendFormatted<T>(T value, int alignment)
			 => _f.AppendFormatted(value, alignment);
		
		public void AppendFormatted<T>(T value, string format) {
			_f.AppendFormatted(value, format);
		}
		
		public void AppendFormatted<T>(T value, int alignment, string format) {
			_f.AppendFormatted(value, alignment, format);
		}
		
		public void AppendFormatted(RStr value)
			=> _f.AppendFormatted(value);
		
		public void AppendFormatted(RStr value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public void AppendFormatted(string value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public void AppendFormatted(object value, int alignment = 0, string format = null)
			=> _f.AppendFormatted(value, alignment, format);
		
		public string GetFormattedText() => _f.ToStringAndClear();
	}
#pragma warning restore 1591
	
	/// <summary>
	/// Loads a web page or RTF text from a file or URL into the last added element.
	/// </summary>
	/// <param name="source">File or URL to load. Supported element types and sources:
	/// <see cref="WebBrowser"/>, <see cref="Frame"/> - URL or file path.
	/// <see cref="RichTextBox"/> - path of a local <c>.rtf</c> file.
	/// </param>
	/// <exception cref="NotSupportedException">
	/// - Unsupported element type.
	/// - <b>RichTextBox</b> <i>source</i> does not end with <c>".rtf"</c>.
	/// </exception>
	/// <remarks>
	/// If fails to load, prints warning. See <see cref="print.warning"/>.
	/// </remarks>
	public wpfBuilder LoadFile(string source) {
		var c = Last;
		bool bad = false;
		try {
			source = _UriNormalize(source);
			switch (c) {
			case WebBrowser u: u.Source = new Uri(source); break;
			case Frame u: u.Source = new Uri(source); break;
			case RichTextBox u when source.Ends(".rtf", true):
				using (var fs = File.OpenRead(source)) { u.Selection.Load(fs, DataFormats.Rtf); }
				//also supports DataFormats.Text,Xaml,XamlPackage. If need HTML, download and try HtmlToXamlConverter. See https://www.codeproject.com/Articles/1097390/Displaying-HTML-in-a-WPF-RichTextBox
				break;
			default: bad = true; break;
			}
		}
		catch (Exception ex) { print.warning("LoadFile() failed. " + ex.ToString(), -1); }
		if (bad) throw new NotSupportedException("LoadFile(): Unsupported type of element or source.");
		return this;
	}
	
	/// <summary>
	/// Loads image into the last added <see cref="System.Windows.Controls.Image"/>.
	/// </summary>
	/// <param name="source">Sets <see cref="Image.Source"/>.</param>
	/// <param name="stretch">Sets <see cref="Image.Stretch"/>.</param>
	/// <param name="stretchDirection">Sets <see cref="Image.StretchDirection"/>.</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>Image</b>.</exception>
	/// <remarks>
	/// To load vector images from XAML, don't use <b>Image</b> control and this function. Instead create control from XAML, for example with <see cref="ImageUtil.LoadWpfImageElement"/>, and add it with <see cref="Add(FrameworkElement, WBAdd)"/>.
	/// </remarks>
	/// <seealso cref="icon.ToWpfImage"/>
	/// <seealso cref="ImageUtil"/>
	public wpfBuilder Image(ImageSource source, Stretch stretch = Stretch.None, StretchDirection stretchDirection = StretchDirection.DownOnly)
		 => _Image(source, null, stretch, stretchDirection);
	
	wpfBuilder _Image(ImageSource source, string file, Stretch stretch, StretchDirection stretchDirection) {
		var c = Last as Image ?? throw new NotSupportedException("Image(): Last added must be Image");
		if (file != null) {
			//try { source = new BitmapImage(_Uri(file)); }
			try { source = ImageUtil.LoadWpfImage(file); }
			catch (Exception ex) { print.warning("Image() failed. " + ex.ToString(), -1); }
		}
		c.Stretch = stretch; //default Uniform
		c.StretchDirection = stretchDirection; //default Both
		c.Source = source;
		return this;
	}
	
	/// <summary>
	/// Loads image from a file or URL into the last added <see cref="System.Windows.Controls.Image"/>.
	/// </summary>
	/// <param name="source">File path etc. See <see cref="ImageUtil.LoadWpfImage"/>. Sets <see cref="Image.Source"/>.</param>
	/// <param name="stretch">Sets <see cref="Image.Stretch"/>.</param>
	/// <param name="stretchDirection">Sets <see cref="Image.StretchDirection"/>.</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>Image</b>.</exception>
	/// <remarks>
	/// If fails to load, prints warning. See <see cref="print.warning"/>.
	/// </remarks>
	public wpfBuilder Image(string source, Stretch stretch = Stretch.None, StretchDirection stretchDirection = StretchDirection.DownOnly)
		=> _Image(null, source, stretch, stretchDirection);
	
	/// <summary>
	/// Sets vertical or horizontal splitter properties of the last added <see cref="GridSplitter"/>.
	/// </summary>
	/// <param name="vertical">If <c>true</c>, resizes columns, else rows.</param>
	/// <param name="span">How many rows spans vertical splitter, or how many columns spans horizontal splitter. Can be more than row/column count.</param>
	/// <param name="thickness">Width of vertical splitter or height of horizontal. If <b>double.NaN</b>, sets alignment "stretch", else "center".</param>
	/// <exception cref="NotSupportedException">The last added element is not <b>GridSplitter</b>.</exception>
	/// <example>
	/// Vertical splitter.
	/// <code><![CDATA[
	/// var b = new wpfBuilder("Window").WinSize(400)
	/// 	.Columns(30.., 0, -1) //the middle column is for splitter; the 30 is minimal width
	/// 	.R.Add(out TextBox _)
	/// 	.Add<GridSplitter>().Splitter(true, 2).Brush(Brushes.Orange) //add splitter in the middle column
	/// 	.Add(out TextBox _)
	/// 	.R.Add(out TextBox _).Skip().Add(out TextBox _) //skip the splitter's column
	/// 	.R.AddOkCancel()
	/// 	.End();
	/// if (!b.ShowDialog()) return;
	/// ]]></code>
	/// Horizontal splitter.
	/// <code><![CDATA[
	/// var b = new wpfBuilder("Window").WinSize(300, 300)
	/// 	.Row(27..).Add("Row", out TextBox _)
	/// 	.Add<GridSplitter>().Splitter(false, 2).Brush(Brushes.Orange)
	/// 	.Row(-1).Add("Row", out TextBox _)
	/// 	.R.AddOkCancel()
	/// 	.End();
	/// if (!b.ShowDialog()) return;
	/// ]]></code>
	/// </example>
	public wpfBuilder Splitter(bool vertical, int span = 1, double thickness = 4) {
		var g = _ParentOfLastAsOrThrow<_Grid>();
		var c = Last as GridSplitter ?? throw new NotSupportedException("Splitter(): Last added must be GridSplitter");
		if (vertical) {
			c.HorizontalAlignment = double.IsNaN(thickness) ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
			c.VerticalAlignment = VerticalAlignment.Stretch;
			c.ResizeDirection = GridResizeDirection.Columns;
			c.Width = thickness;
			if (span != 1) Grid.SetRowSpan(c, span);
		} else {
			c.HorizontalAlignment = HorizontalAlignment.Stretch;
			c.VerticalAlignment = double.IsNaN(thickness) ? VerticalAlignment.Stretch : VerticalAlignment.Center;
			c.ResizeDirection = GridResizeDirection.Rows;
			c.Height = thickness;
			if (span != 1) g.Span(span);
		}
		c.ResizeBehavior = GridResizeBehavior.PreviousAndNext;
		return this;
	}
	
	//FUTURE: need a numeric input control. This code is for WinForms NumericUpDown.
	//	public wpfBuilder Number(decimal? value = null, decimal? min = null, decimal? max=null, decimal? increment=null, int? decimalPlaces=null, bool? thousandsSeparator=null, bool? hex =null) {
	//		var c = Last as NumericUpDown ?? throw new NotSupportedException("Number(): Last added must be NumericUpDown");
	//		if(min!=null) c.Minimum=min.Value;
	//		if(max!=null) c.Maximum=max.Value;
	//		if(increment!=null) c.Increment=increment.Value;
	//		if(decimalPlaces!=null) c.DecimalPlaces=decimalPlaces.Value;
	//		if(thousandsSeparator!=null) c.ThousandsSeparator=thousandsSeparator.Value;
	//		if(hex!=null) c.Hexadecimal=hex.Value;
	//		if(value!=null) c.Value=value.Value; else c.Text=null;
	//		return this;
	//	}
	
	#endregion
	
	#region nested panel
	
	wpfBuilder _Start(_PanelBase p, bool childOfLast) {
		_p.BeforeAdd(childOfLast ? WBAdd.ChildOfLast : 0);
		_AddToParent(p.panel, childOfLast);
		_p = p;
		return this;
	}
	
	wpfBuilder _Start<T>(_PanelBase p, out T container, object header) where T : HeaderedContentControl, new() {
		Add(out container, header);
		container.Content = p.panel;
		if (container is GroupBox) p.panel.Margin = new Thickness(0, 2, 0, 0);
		_p = p;
		return this;
	}
	
	/// <summary>
	/// Adds <see cref="Grid"/> panel (table) that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="childOfLast"><inheritdoc cref="WBAdd.ChildOfLast" path="/summary/node()"/>.</param>
	/// <remarks>
	/// How <see cref="Last"/> changes: after calling this function it is the grid (<see cref="Panel"/>); after adding an element it is the element; finally, after calling <b>End</b> it is the grid if <i>childOfLast</i> <c>false</c>, else its parent. The same with all <b>StartX</b> functions.
	/// </remarks>
	public wpfBuilder StartGrid(bool childOfLast = false) => _Start(new _Grid(this), childOfLast);
	
	/// <summary>
	/// Adds a headered content control (<see cref="GroupBox"/>, <see cref="Expander"/>, etc) with child <see cref="Grid"/> panel (table) that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="header">Header text/content.</param>
	/// <remarks>
	/// How <see cref="Last"/> changes: after calling this function it is the grid (<see cref="Panel"/>); after adding an element it is the element; finally, after calling <b>End</b> it is the content control (grid's parent). The same with all <b>StartX</b> functions.
	/// </remarks>
	/// <example>
	/// <code><![CDATA[
	/// b.StartGrid<GroupBox>("Group");
	/// ]]></code>
	/// </example>
	public wpfBuilder StartGrid<T>(object header) where T : HeaderedContentControl, new() => _Start(new _Grid(this), out T _, header);
	
	/// <param name="container">Receives content control's variable. The function creates new control of the type.</param>
	/// <example>
	/// <code><![CDATA[
	/// b.StartGrid(out Expander g, "Expander"); g.IsExpanded=true;
	/// ]]></code>
	/// </example>
	/// <inheritdoc cref="StartGrid{T}(object)"/>
	public wpfBuilder StartGrid<T>(out T container, object header) where T : HeaderedContentControl, new() => _Start(new _Grid(this), out container, header);
	
	/// <summary>
	/// Adds <see cref="Canvas"/> panel that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="childOfLast"><inheritdoc cref="WBAdd.ChildOfLast" path="/summary/node()"/>.</param>
	/// <remarks>
	/// For each added control call <see cref="XY"/> or use indexer like <c>[x, y]</c> or <c>[x, y, width, height]</c>.
	/// </remarks>
	public wpfBuilder StartCanvas(bool childOfLast = false) => _Start(new _Canvas(this), childOfLast);
	
	/// <summary>
	/// Adds a headered content control (<see cref="GroupBox"/>, <see cref="Expander"/>, etc) with child <see cref="Canvas"/> panel that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="header">Header text/content.</param>
	public wpfBuilder StartCanvas<T>(object header) where T : HeaderedContentControl, new() => _Start(new _Canvas(this), out T _, header);
	
	/// <param name="container">Receives content control's variable. The function creates new control of the type.</param>
	/// <inheritdoc cref="StartCanvas{T}(object)"/>
	public wpfBuilder StartCanvas<T>(out T container, object header) where T : HeaderedContentControl, new() => _Start(new _Canvas(this), out container, header);
	
	/// <summary>
	/// Adds <see cref="DockPanel"/> panel that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="childOfLast"><inheritdoc cref="WBAdd.ChildOfLast" path="/summary/node()"/>.</param>
	/// <remarks>
	/// For added elements call <see cref="Dock"/>, maybe except for the last element that fills remaining space.
	/// </remarks>
	public wpfBuilder StartDock(bool childOfLast = false) => _Start(new _DockPanel(this), childOfLast);
	
	/// <summary>
	/// Adds a headered content control (<see cref="GroupBox"/>, <see cref="Expander"/>, etc) with child <see cref="DockPanel"/> panel that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="header">Header text/content.</param>
	public wpfBuilder StartDock<T>(object header) where T : HeaderedContentControl, new() => _Start(new _DockPanel(this), out T _, header);
	
	/// <param name="container">Receives content control's variable. The function creates new control of the type.</param>
	/// <inheritdoc cref="StartDock{T}(object)"/>
	public wpfBuilder StartDock<T>(out T container, object header) where T : HeaderedContentControl, new() => _Start(new _DockPanel(this), out container, header);
	
	/// <summary>
	/// Adds <see cref="StackPanel"/> panel that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="vertical"></param>
	/// <param name="childOfLast"><inheritdoc cref="WBAdd.ChildOfLast" path="/summary/node()"/>.</param>
	public wpfBuilder StartStack(bool vertical = false, bool childOfLast = false) => _Start(new _StackPanel(this, vertical), childOfLast);
	
	/// <summary>
	/// Adds a headered content control (<see cref="GroupBox"/>, <see cref="Expander"/>, etc) with child <see cref="StackPanel"/> panel that will contain elements added with <see cref="Add"/> etc. Finally call <see cref="End"/> to return to current panel.
	/// </summary>
	/// <param name="header">Header text/content.</param>
	/// <param name="vertical"></param>
	public wpfBuilder StartStack<T>(object header, bool vertical = false) where T : HeaderedContentControl, new() => _Start(new _StackPanel(this, vertical), out T _, header);
	
	/// <param name="container">Receives content control's variable. The function creates new control of the type.</param>
	/// <inheritdoc cref="StartStack{T}(object, bool)"/>
	public wpfBuilder StartStack<T>(out T container, object header, bool vertical = false) where T : HeaderedContentControl, new() => _Start(new _StackPanel(this, vertical), out container, header);
	
	/// <summary>
	/// Adds right-bottom-aligned horizontal stack panel (<see cref="StartStack"/>) for adding <b>OK</b>, <b>Cancel</b> and more buttons.
	/// When don't need more buttons, use just <see cref="AddOkCancel"/>.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// b.StartOkCancel().AddOkCancel().AddButton("Help", _ => {  }).Width(70).End();
	/// ]]></code>
	/// </example>
	public wpfBuilder StartOkCancel() {
		var pa = _p;
		StartStack();
		if (pa is not _Canvas) {
			_p.panel.HorizontalAlignment = HorizontalAlignment.Right;
			_p.panel.VerticalAlignment = VerticalAlignment.Bottom;
			_p.panel.Margin = new Thickness(0, 2, 0, 0);
		}
		return this;
	}
	
	#endregion
	
	#region util
	
	bool _IsNested => _p.parent != null;
	
	bool _IsWindowEnded => _p.ended && _p.parent == null;
	
	Window _FindWindow(DependencyObject c) => _window ?? Window.GetWindow(c); //CONSIDER: support top-level HwndSource window
	
	void _ThrowIfNotWindow([CallerMemberName] string m_ = null) {
		if (_window == null) throw new InvalidOperationException(m_ + "(): Container is not Window");
	}
	
	Control _LastAsControlOrThrow([CallerMemberName] string m_ = null) => (Last as Control) ?? throw new InvalidOperationException(m_ + "(): Last added element is not Control");
	
	_PanelBase _ParentOfLast => ReferenceEquals(Last, _p.panel) ? _p.parent : _p;
	
	T _ParentOfLastAsOrThrow<T>([CallerMemberName] string m_ = null) where T : _PanelBase {
		return _ParentOfLast is T t ? t : throw new InvalidOperationException($"{m_}() not in {typeof(T).Name[1..]} panel.");
	}
	
	//	void _ThrowIfParentOfLastIs<TControl>([CallerMemberName] string caller = null) where TControl : Panel {
	//		if(Last.Parent is TControl) throw new InvalidOperationException($"{caller}() in {typeof(TControl).Name} panel.");
	//	}
	
	static string _UriNormalize(string source) => pathname.normalize(source, flags: PNFlags.CanBeUrlOrShell);
	
	static Uri _Uri(string source) => new Uri(_UriNormalize(source));
	
	//Returns true if probably using a custom theme. I don't know what is the correct way, but this should work in most cases. Fast.
	static bool _IsCustomTheme() {
		if (s_isCustomTheme == null) {
			var app = Application.Current;
			s_isCustomTheme = app != null && app.Resources.MergedDictionaries.Count > 0;
		}
		return s_isCustomTheme == true;
	}
	static bool? s_isCustomTheme;
	
	/// <summary>
	/// Gets or sets a property of an element of any type that has the property.
	/// </summary>
	struct _PropGetSet<T> {
		FrameworkElement _e;
		PropertyInfo _p;
		
		public _PropGetSet(FrameworkElement e, string prop, [CallerMemberName] string m_ = null) {
			_e = e;
			_p = e.GetType().GetProperty(prop, typeof(T)) ?? throw new InvalidOperationException(m_ + $"(): Last added element does not have {prop} property");
		}
		
		/// <summary>
		/// Like ctor but does not throw.
		/// </summary>
		/// <returns><c>false</c> if failed.</returns>
		public static bool TryCreate(FrameworkElement e, string prop, out _PropGetSet<T> r) {
			var p = e.GetType().GetProperty(prop, typeof(T));
			if (p == null) { r = default; return false; }
			r = new() { _e = e, _p = p };
			return true;
		}
		
		public T Get => (T)_p.GetValue(_e);
		
		public void Set(T value) { _p.SetValue(_e, value); }
	}
	
	#endregion
}
