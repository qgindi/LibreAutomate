using System.Windows.Controls;

static class ExtWpf {
	public static void AddChild(this Grid g, UIElement e, int row, int column, int rowSpan = 1, int columnSpan = 1) {
		Grid.SetRow(e, row);
		Grid.SetColumn(e, column);
		if (rowSpan > 1) Grid.SetRowSpan(e, rowSpan);
		if (columnSpan > 1) Grid.SetColumnSpan(e, columnSpan);
		g.Children.Add(e);
	}
	
	public static void AddColumns(this Grid g, params WBGridLength[] widths) {
		foreach (var v in widths) g.ColumnDefinitions.Add(v.Column);
	}
	
	public static void AddRows(this Grid g, params WBGridLength[] heights) {
		foreach (var v in heights) g.RowDefinitions.Add(v.Row);
	}
	
}

struct WBGridLength {
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
