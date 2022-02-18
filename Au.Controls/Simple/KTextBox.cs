using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Au.Controls;

/// <summary>
/// Adds some features, eg watermark/hint/cue text.
/// </summary>
public class KTextBox : TextBox {
	/// <summary>
	/// Adds watermark/hint/cue text which is visible when the editable text is empty.
	/// Call once, before showing the control. Changing later isn't supported.
	/// The control must be a child of an <b>AdornerDecorator</b>; see example.
	/// </summary>
	/// <example>
	/// <code><![CDATA[
	/// b.R.Add<AdornerDecorator>().Add(out TextBox2 tPackage, flags: WBAdd.ChildOfLast);
	/// tPackage.Watermark = "Search";
	/// ]]></code>
	/// </example>
	public string Watermark {
		get => _wma.Text;
		set {
			if (_wma != null) throw new InvalidOperationException();
			_wma = new _WatermarkAdorner(this) { Text = value };
			AdornerLayer.GetAdornerLayer(this).Add(_wma);
		}
	}
	_WatermarkAdorner _wma;
	
	///
	protected override void OnTextChanged(TextChangedEventArgs e) {
		_wma.Visibility = Text.NE() ? Visibility.Visible : Visibility.Hidden;
		base.OnTextChanged(e);
	}

	class _WatermarkAdorner : Adorner {
		public string Text;

		public _WatermarkAdorner(UIElement adornedElement) : base(adornedElement) {
			IsHitTestVisible=false;
		}
		
		protected override void OnRender(DrawingContext dc) {
			//Rect r = new Rect(this.AdornedElement.RenderSize);
			
			var tf = new Typeface("Segoe UI");
			var ft = new FormattedText(Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 12, Brushes.Gray, 96);
			dc.DrawText(ft, new(5, 2));
		}
	}
}
