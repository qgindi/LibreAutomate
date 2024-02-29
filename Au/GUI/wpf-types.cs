using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;

namespace Au.Types {
	
	/// <summary>
	/// Used with <see cref="wpfBuilder"/> constructor to specify the type of the root panel.
	/// </summary>
	public enum WBPanelType {
		///
		Grid,
		///
		Canvas,
		///
		Dock,
		///
		VerticalStack,
		///
		HorizontalStack,
	}
	
	/// <summary>
	/// Flags for <see cref="wpfBuilder.Add"/>.
	/// </summary>
	[Flags]
	public enum WBAdd {
		/// <summary>
		/// Add as child of <see cref="wpfBuilder.Last"/>, which can be of type (or base type):
		/// <br/>• <see cref="ContentControl"/>. Adds as its <see cref="ContentControl.Content"/> property. For example you can add a <b>CheckBox</b> in a <b>Button</b>.
		/// <br/>• <see cref="Decorator"/>, for example <see cref="Border"/>. Adds as its <see cref="Decorator.Child"/> property.
		/// </summary>
		ChildOfLast = 1,
		
		/// <summary>
		/// Don't adjust some properties (padding, aligning, specified in <see cref="wpfBuilder.Options"/>, etc) of some control types. Just set default margin, except if <i>ChildOfLast</i>.
		/// </summary>
		DontSetProperties = 2,
	}
	
	/// <summary>
	/// Used with <see cref="wpfBuilder"/> functions for width/height parameters. Allows to specify minimal and/or maximal values too.
	/// </summary>
	/// <remarks>
	/// Has implicit conversions from <b>double</b>, <b>Range</b> and tuple <b>(double length, Range minMax)</b>.
	/// To specify width or height, pass an <b>int</b> or <b>double</b> value, like <c>100</c> or <c>15.25</c>.
	/// To specify minimal value, pass a range like <c>100..</c>.
	/// To specify maximal value, pass a range like <c>..100</c>.
	/// To specify minimal and maximal values, pass a range like <c>100..500</c>.
	/// To specify width or height and minimal or/and maximal values, pass a tuple like <c>(100, 50..)</c> or <c>(100, ..200)</c> or <c>(100, 50..200)</c>.
	/// </remarks>
	public struct WBLength {
		double _v;
		Range _r;
		
		WBLength(double v, Range r) {
			if (r.Start.IsFromEnd || (r.End.IsFromEnd && r.End.Value != 0)) throw new ArgumentException();
			_v = v; _r = r;
		}
		
		///
		public static implicit operator WBLength(double v) => new WBLength { _v = v, _r = .. };
		
		///
		public static implicit operator WBLength(Range v) => new WBLength(double.NaN, v);
		
		///
		public static implicit operator WBLength((double length, Range minMax) v) => new WBLength(v.length, v.minMax);
		
		/// <summary>
		/// Gets the width or height value. Returns <c>false</c> if not set.
		/// </summary>
		public bool GetLength(out double value) {
			value = _v;
			return !double.IsNaN(_v);
		}
		
		/// <summary>
		/// Gets the minimal value. Returns <c>false</c> if not set.
		/// </summary>
		public bool GetMin(out int value) {
			value = _r.Start.Value;
			return value > 0;
		}
		
		/// <summary>
		/// Gets the maximal value. Returns <c>false</c> if not set.
		/// </summary>
		public bool GetMax(out int value) {
			value = _r.End.Value;
			return !_r.End.IsFromEnd;
		}
		
		/// <summary>
		/// Sets <b>Width</b> or <b>Height</b> or/and <b>MinWidth</b>/<b>MinHeight</b> or/and <b>MaxWidth</b>/<b>MaxHeight</b> of the element.
		/// </summary>
		/// <param name="e">Element.</param>
		/// <param name="height">Set <b>Height</b>. If <c>false</c>, sets <b>Width</b>.</param>
		public void ApplyTo(FrameworkElement e, bool height) {
			if (GetLength(out double d)) { if (height) e.Height = d; else e.Width = d; }
			if (GetMin(out int i)) { if (height) e.MinHeight = i; else e.MinWidth = i; }
			if (GetMax(out i)) { if (height) e.MaxHeight = i; else e.MaxWidth = i; }
		}
	}
	
	/// <summary>
	/// Used with <see cref="wpfBuilder"/> functions to specify width/height of columns and rows. Allows to specify minimal and/or maximal values too.
	/// Like <see cref="WBLength"/>, but has functions to create <see cref="ColumnDefinition"/> and <see cref="RowDefinition"/>. Also has implicit conversion from these types.
	/// </summary>
	public struct WBGridLength {
		double _v;
		Range _r;
		DefinitionBase _def;
		
		WBGridLength(double v, Range r) {
			if (r.Start.IsFromEnd || (r.End.IsFromEnd && r.End.Value != 0)) throw new ArgumentException();
			_v = v; _r = r; _def = null;
		}
		
		///
		public static implicit operator WBGridLength(double v) => new WBGridLength { _v = v, _r = .. };
		
		///
		public static implicit operator WBGridLength((double length, Range minMax) v) => new WBGridLength(v.length, v.minMax);
		
		///
		public static implicit operator WBGridLength(Range v) => new WBGridLength(-1, v);
		
		///
		public static implicit operator WBGridLength(DefinitionBase v) => new WBGridLength { _def = v };
		
		/// <summary>
		/// Creates column definition object from assigned width or/and min/max width values. Or just returns the assigned or previously created object.
		/// </summary>
		public ColumnDefinition Column {
			get {
				if (_def is ColumnDefinition d) return d;
				d = new ColumnDefinition { Width = _GridLength(_v) };
				if (_r.Start.Value > 0) d.MinWidth = _r.Start.Value;
				if (!_r.End.IsFromEnd) d.MaxWidth = _r.End.Value;
				_def = d;
				return d;
			}
		}
		
		/// <summary>
		/// Creates row definition object from assigned height or/and min/max height values. Or just returns the assigned or previously created object.
		/// </summary>
		public RowDefinition Row {
			get {
				if (_def is RowDefinition d) return d;
				d = new RowDefinition { Height = _GridLength(_v) };
				if (_r.Start.Value > 0) d.MinHeight = _r.Start.Value;
				if (!_r.End.IsFromEnd) d.MaxHeight = _r.End.Value;
				_def = d;
				return d;
			}
		}
		
		GridLength _GridLength(double d) {
			if (d > 0) return new GridLength(d, GridUnitType.Pixel);
			if (d < 0) return new GridLength(-d, GridUnitType.Star);
			return new GridLength();
		}
	}
	
	/// <summary>
	/// Arguments for <see cref="wpfBuilder.AlsoAll"/> callback function.
	/// </summary>
	public class WBAlsoAllArgs {
		/// <summary>
		/// Gets 0-based column index of last added control, or -1 if not in grid.
		/// </summary>
		public int Column { get; internal set; }
		
		/// <summary>
		/// Gets 0-based row index of last added control, or -1 if not in grid.
		/// </summary>
		public int Row { get; internal set; }
	}
	
	/// <summary>
	/// Arguments for <see cref="wpfBuilder.AddButton"/> callback function.
	/// </summary>
	public class WBButtonClickArgs : CancelEventArgs {
		/// <summary>
		/// Gets the button.
		/// </summary>
		public Button Button { get; internal set; }
		
		/// <summary>
		/// Gets the window.
		/// </summary>
		public Window Window { get; internal set; }
	}
	
	/// <summary>
	/// Flags for <see cref="wpfBuilder.AddButton"/>.
	/// </summary>
	[Flags]
	public enum WBBFlags {
		/// <summary>It is OK button (<see cref="Button.IsDefault"/>, closes window, validates, <see cref="wpfBuilder.OkApply"/> event).</summary>
		OK = 1,
		
		/// <summary>It is Cancel button (<see cref="Button.IsCancel"/>, closes window).</summary>
		Cancel = 2,
		
		/// <summary>It is Apply button (size like OK/Cancel, validates, <see cref="wpfBuilder.OkApply"/> event).</summary>
		Apply = 4,
		
		/// <summary>Perform validation like OK and Apply buttons.</summary>
		Validate = 8,
	}
	
	/// <summary>
	/// [Obsolete]
	/// Can be used with <see cref="wpfBuilder.Text"/> to add a hyperlink.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)] //obsolete
	public class WBLink {
		///
		public Hyperlink Hlink { get; }
		
		WBLink(string text, bool bold) {
			Run run = new(text);
			Hlink = new(bold ? new Bold(run) : run);
		}
		
		/// <summary>
		/// Sets link text and action.
		/// </summary>
		public WBLink(string text, Action action, bool bold = false) : this(text, bold) {
			Hlink.Click += (o, e) => action();
		}
		
		/// <summary>
		/// Sets link text and action.
		/// </summary>
		/// <param name="text">Link text.</param>
		/// <param name="action">Action to execute on click.</param>
		/// <param name="bold">Bold font.</param>
		public WBLink(string text, Action<Hyperlink> action, bool bold = false) : this(text, bold) {
			Hlink.Click += (o, e) => action(o as Hyperlink);
		}
		
		/// <summary>
		/// Sets link text and target URL or file etc.
		/// On click will call <see cref="run.itSafe"/>.
		/// </summary>
		/// <param name="text">Link text.</param>
		/// <param name="urlOrPath">URL or path for <see cref="run.itSafe"/>. If <c>null</c>, uses <i>text</i>.</param>
		/// <param name="args"><i>args</i> for <b>run.itSafe</b>.</param>
		/// <param name="bold">Bold font.</param>
		public WBLink(string text, string urlOrPath = null, string args = null, bool bold = false) : this(text, _ => run.itSafe(urlOrPath ?? text, args)) { }
	}
}

namespace Au.More {
	//rejected. Unsafe etc. For example, when assigning to object, uses CheckBool whereas the user may expect bool.
	//	/// <summary>
	//	/// <see cref="CheckBox"/> that can be used like bool.
	//	/// For example instead of <c>if(c.IsChecked == true)</c> can be used <c>if(c)</c>.
	//	/// </summary>
	//	public class CheckBool : CheckBox
	//	{
	//		///
	//		public CheckBool()
	//		{
	//			this.SetResourceReference(StyleProperty, typeof(CheckBox));
	//		}
	//
	//		/// <summary>
	//		/// Returns true if <see cref="ToggleButton.IsChecked"/> == true.
	//		/// </summary>
	//		public static implicit operator bool(CheckBool c) => c.IsChecked.GetValueOrDefault();
	//	}
	
	
	/// <summary>
	/// Grid splitter control. Based on <see cref="GridSplitter"/>, changes its behavior.
	/// </summary>
	/// <remarks>
	/// Try this class when <see cref="GridSplitter"/> does not work as you want.
	/// 
	/// Limitations (bad or good):
	/// - Splitters must be on own rows/columns. Throws exception if <b>ResizeBehavior</b> is not <b>PreviousAndNext</b> (which is default).
	/// - Throws exception is there are star-sized splitter rows.
	/// - Does not resize auto-sized rows/columns. Only pixel-sized and star-sized.
	/// - With <b>UseLayoutRounding</b> may flicker when resizing, especially when high DPI.
	/// </remarks>
	public class GridSplitter2 : GridSplitter {
		static GridSplitter2() {
			EventManager.RegisterClassHandler(typeof(GridSplitter2), Thumb.DragStartedEvent, new DragStartedEventHandler(_OnDragStarted));
			EventManager.RegisterClassHandler(typeof(GridSplitter2), Thumb.DragCompletedEvent, new DragCompletedEventHandler(_OnDragCompleted));
			EventManager.RegisterClassHandler(typeof(GridSplitter2), Thumb.DragDeltaEvent, new DragDeltaEventHandler(_OnDragDelta));
		}
		
		///
		public GridSplitter2() {
			ResizeBehavior = GridResizeBehavior.PreviousAndNext;
			SnapsToDevicePixels = true;
			Focusable = false;
		}
		
		static void _OnDragStarted(object sender, DragStartedEventArgs e) => (sender as GridSplitter2)._OnDragStarted(e);
		
		void _OnDragStarted(DragStartedEventArgs e) {
			if (!ShowsPreview) e.Handled = true;
			if (!_Init()) base.CancelDrag();
		}
		
		static void _OnDragCompleted(object sender, DragCompletedEventArgs e) => (sender as GridSplitter2)._OnDragCompleted(e);
		
		void _OnDragCompleted(DragCompletedEventArgs e) {
			if (!ShowsPreview) e.Handled = true; //else somehow GridSplitter does not resize, just removes the adorner
			if (_a == null) return; //two events if called CancelDrag
			if (!e.Canceled) _MoveSplitter();
			_a = null;
		}
		
		static void _OnDragDelta(object sender, DragDeltaEventArgs e) => (sender as GridSplitter2)._OnDragDelta(e);
		
		void _OnDragDelta(DragDeltaEventArgs e) {
			_delta = _isVertical ? e.HorizontalChange : e.VerticalChange;
			var di = DragIncrement; _delta = Math.Round(_delta / di) * di;
			if (ShowsPreview) return;
			e.Handled = true;
			if (_working) return; _working = true; //avoid too much CPU and delayed repainting of hwndhosts
			Dispatcher.InvokeAsync(() => _working = false, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
			_MoveSplitter();
		}
		bool _working;
		
		///
		protected override void OnKeyDown(KeyEventArgs e) {
			if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right) {
				e.Handled = true;
				if (!_Init()) return;
				_delta = KeyboardIncrement * ((e.Key == Key.Up || e.Key == Key.Right) ? -1 : 1);
				if (_isVertical && FlowDirection == FlowDirection.RightToLeft) _delta = -_delta;
				_MoveSplitter();
				_a = null;
			} else if (e.Key == Key.Escape && _a != null) {
				e.Handled = true;
				CancelDrag();
			}
		}
		
		bool _Init(Key key = default) {
			_a = null;
			_isVertical = _IsVertical();
			if (key != default && _isVertical != (key == Key.Left || key == Key.Right)) return false;
			//_resizeBehavior=_GetResizeBehavior(_isVertical);
			if (_GetResizeBehavior(_isVertical) != GridResizeBehavior.PreviousAndNext) throw new NotSupportedException("ResizeBehavior must be PreviousAndNext.");
			_delta = 0;
			_grid = Parent as Grid;
			_a = new List<_RowCol>();
			_index = 0;
			var splitters = _grid.Children.OfType<GridSplitter2>()
				.Where(o => o._IsVertical() == _isVertical)
				.Select(o => _IndexInGrid(o)).ToArray();
			int index = _IndexInGrid(this);
			for (int i = 0, n = (_isVertical ? _grid.ColumnDefinitions.Count : _grid.RowDefinitions.Count); i < n; i++) {
				var v = new _RowCol(_isVertical ? (DefinitionBase)_grid.ColumnDefinitions[i] : _grid.RowDefinitions[i]);
				if (splitters.Contains(i)) {
					if (v.IsStar) throw new InvalidOperationException("Splitter row/column cannot be star-sized.");
					if (i == index) _index = _a.Count;
				} else {
					if (v.Unit == GridUnitType.Auto) continue;
					if (v.IsStar) v.SetSize(v.ActualSize);
					_a.Add(v);
				}
			}
			if (_index == 0 || _index == _a.Count) { //no resizable items before or after
				_a = null;
				return false;
			}
			return true;
		}
		
		Grid _grid;
		bool _isVertical;
		//	GridResizeBehavior _resizeBehavior;
		List<_RowCol> _a; //resizable rows/columns, ie those without splitters and not auto-sized
		int _index; //index of first _a item after this splitter
		double _delta;
		
		void _MoveSplitter() {
			if (_a == null || _delta == 0) return;
			
			_Side before = default, after = default;
			
			//resize multiple star-sized items at that side?
			if (ResizeNearest || Keyboard.Modifiers == ModifierKeys.Control) {
				before.single = after.single = true;
			} else {
				int stars = 0; //flags: 1 stars before, 2 stars after
				for (int i = 0; i < _a.Count; i++) if (_a[i].IsStar) stars |= i < _index ? 1 : 2;
				before.single = _index == 1 || 0 == (stars & 1) || _a[_index - 1].ActualSize < 4 || (stars == 3 && !_a[_index - 1].IsStar); //without the last || subexpression would be impossible to resize fixed-sized items if there are star-sized items at both sides
				after.single = _index == _a.Count - 1 || 0 == (stars & 2) || _a[_index].ActualSize < 4 || (stars == 3 && !_a[_index].IsStar);
			}
			
			for (int i = 0; i < _a.Count; i++) {
				if (!_IsResizable(i)) continue;
				if (i < _index) before.Add(_a[i]); else after.Add(_a[i]);
			}
			
			double v1 = Math.Clamp(before.size + _delta, before.min, before.max), v2 = Math.Clamp(after.size - _delta, after.min, after.max);
			_delta = 0;
			if (v1 == before.min || v1 == before.max) v2 = before.size + after.size - v1; else if (v2 == after.min || v2 == after.max) v1 = before.size + after.size - v2;
			
			_ResizeSide(before, true, v1);
			_ResizeSide(after, false, v2);
			
			void _ResizeSide(_Side side, bool isBefore, double size) {
				if (side.single) {
					_a[_index - (isBefore ? 1 : 0)].SetSize(size);
				} else {
					for (int i = isBefore ? 0 : _index, to = isBefore ? _index : _a.Count; i < to; i++) {
						if (!_IsResizable(i)) continue;
						var v = _a[i];
						var k = size * v.ActualSize; if (side.size > 0.1) k /= side.size; else k = 0.1;
						v.SetSize(k);
					}
				}
			}
			
			bool _IsResizable(int index) {
				if (index < _index) return before.single ? index == _index - 1 : _a[index].IsStar;
				return after.single ? index == _index : _a[index].IsStar;
			}
		}
		
		struct _Side {
			public double size, min, max;
			public bool single;
			public int stars;
			
			public void Add(_RowCol v) {
				size += v.ActualSize;
				min += v.Min;
				double x = v.Max;
				if (max != double.PositiveInfinity) { if (x == double.PositiveInfinity) max = x; else max += x; }
				if (!single && v.IsStar) stars++;
			}
		}
		
		/// <summary>
		/// Always resize only the nearest resizable row/column at each side.
		/// If <c>false</c> (default), may resize multiple star-sized rows/columns, unless with Ctrl key.
		/// </summary>
		public bool ResizeNearest { get; set; }
		
		#region util
		
		bool _IsVertical() { //see code of GridSplitter.GetEffectiveResizeDirection. The algorithm is documented.
			var dir = this.ResizeDirection;
			if (dir != GridResizeDirection.Auto) return dir == GridResizeDirection.Columns;
			if (this.HorizontalAlignment != HorizontalAlignment.Stretch) return true;
			if (this.VerticalAlignment != VerticalAlignment.Stretch) return false;
			return this.ActualWidth <= this.ActualHeight;
		}
		
		GridResizeBehavior _GetResizeBehavior(bool vertical) { //see code of GridSplitter.GetEffectiveResizeBehavior
			var r = ResizeBehavior;
			if (r == GridResizeBehavior.BasedOnAlignment) {
				if (vertical) r = HorizontalAlignment switch {
					HorizontalAlignment.Left => GridResizeBehavior.PreviousAndCurrent,
					HorizontalAlignment.Right => GridResizeBehavior.CurrentAndNext,
					_ => GridResizeBehavior.PreviousAndNext,
				};
				else r = VerticalAlignment switch {
					VerticalAlignment.Top => GridResizeBehavior.PreviousAndCurrent,
					VerticalAlignment.Bottom => GridResizeBehavior.CurrentAndNext,
					_ => GridResizeBehavior.PreviousAndNext,
				};
			}
			return r;
		}
		
		int _IndexInGrid(UIElement e) => _isVertical ? Grid.GetColumn(e) : Grid.GetRow(e);
		
		class _RowCol {
			RowDefinition _row;
			ColumnDefinition _col;
			
			public _RowCol(DefinitionBase def) {
				_row = def as RowDefinition;
				_col = def as ColumnDefinition;
				Min = _row?.MinHeight ?? _col.MinWidth;
				Max = _row?.MaxHeight ?? _col.MaxWidth;
				Unit = DefSizeGL.GridUnitType;
			}
			
			public double ActualSize => _row?.ActualHeight ?? _col.ActualWidth;
			
			public double DefSize {
				get => _row?.Height.Value ?? _col.Width.Value;
				//			set { DefSizeGL = new GridLength(value, Unit); }
			}
			
			GridLength DefSizeGL {
				get => _row?.Height ?? _col.Width;
				//			set { if(_row!=null) _row.Height=value; else _col.Width=value; }
			}
			
			public void SetSize(double size) {
				var z = new GridLength(size, Unit);
				if (_row != null) _row.Height = z; else _col.Width = z;
			}
			
			public GridUnitType Unit { get; private set; }
			
			public bool IsStar => Unit == GridUnitType.Star;
			
			public double Min { get; private set; }
			
			public double Max { get; private set; }
		}
		
		#endregion
	}
	
	/// <summary>
	/// Adorner that draws watermark/hint/cue text over the adorned control (<b>TextBox</b> etc).
	/// </summary>
	public class WatermarkAdorner : Adorner {
		string _text;
		Control _c;
		TextBox _tCB;
		
		/// <summary>
		/// Initializes and adds this adorner to the <b>AdornerLayer</b> of the control.
		/// </summary>
		/// <param name="c">The adorned control. Must be a child/descendant of an <b>AdornerDecorator</b>.</param>
		/// <param name="text">Watermark text.</param>
		/// <exception cref="InvalidOperationException">The control isn't in an <b>AdornerDecorator</b>.</exception>
		public WatermarkAdorner(Control c, string text) : base(c) {
			_c = c;
			_text = text;
			IsHitTestVisible = false;
			var layer = AdornerLayer.GetAdornerLayer(c) ?? throw new InvalidOperationException("The control isn't in an AdornerDecorator");
			layer.Add(this);
		}
		
		/// <summary>
		/// Gets or sets watermark text.
		/// </summary>
		public string Text {
			get => _text;
			set {
				if (value != _text) {
					_text = value;
					InvalidateVisual();
				}
			}
		}
		
		/// <summary>
		/// Sets events to show/hide the adorner depending on control text.
		/// The control must be TextBox or editable ComboBox.
		/// </summary>
		public void SetAdornerVisibility() {
			if (_c is TextBox t) {
				_Visibility(t);
			} else {
				_c.Loaded += (_, _) => {
					if (_c.FindVisualDescendant(o => o is TextBox) is TextBox t2) _Visibility(_tCB = t2);
				};
			}
			
			void _Visibility(TextBox t) {
				Visibility = t.Text.NE() ? Visibility.Visible : Visibility.Hidden;
				t.TextChanged += (_, _) => { Visibility = t.Text.NE() ? Visibility.Visible : Visibility.Hidden; };
			}
		}
		
		///
		protected override void OnRender(DrawingContext dc) {
			if (_text.NE()) return;
			var tf = new Typeface(_c.FontFamily, _c.FontStyle, _c.FontWeight, _c.FontStretch);
			var ft = new FormattedText(_text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, _c.FontSize, Brushes.Gray, 96);
			Thickness bt = _c.BorderThickness, pad = _c.Padding;
			Point p = new(bt.Left + pad.Left + 2, bt.Top + pad.Top);
			var z = _c.RenderSize;
			double w = z.Width - bt.Right - pad.Right - 2, h = z.Height - bt.Bottom - pad.Bottom;
			if (_tCB != null) w = _tCB.RenderSize.Width;
			if (w < 4 || h < 4) return;
			dc.PushClip(new RectangleGeometry(new(0, 0, w, h)));
			dc.DrawText(ft, p);
			dc.Pop();
		}
	}
}
