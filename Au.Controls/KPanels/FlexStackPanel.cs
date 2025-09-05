// Copyright (c) 2013 xmetropol.
// This code is distributed under the Microsoft Public License (Ms-PL).
// All rights reserved.

//https://www.codeproject.com/Articles/598123/WPF-Flexible-StackPanel

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Au.Controls;

public enum FlexStretchDirection {
	DownOnly,
	UpOnly,
	Both,
	None,
}

public class FlexStackPanel : Panel {
	#region Static Fields
	
	private const double Tolerance = 0.001;
	
	public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register
	  ("Orientation", typeof(Orientation), typeof(FlexStackPanel),
		  new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, OnOrientationChanged));
	
	public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register
	  ("StretchDirection", typeof(FlexStretchDirection), typeof(FlexStackPanel),
		 new FrameworkPropertyMetadata(default(FlexStretchDirection), FrameworkPropertyMetadataOptions.AffectsMeasure));
	
	public static readonly DependencyPropertyKey HasOverflowedChildrenPropertyKey = DependencyProperty.RegisterReadOnly
	  ("HasOverflowedChildren", typeof(bool), typeof(FlexStackPanel), new PropertyMetadata(default(bool)));
	
	public static readonly DependencyProperty HasOverflowedChildrenProperty = HasOverflowedChildrenPropertyKey.DependencyProperty;
	
	public static readonly DependencyProperty MinSlotSizeProperty = DependencyProperty.RegisterAttached
	  ("MinSlotSize", typeof(double?), typeof(FlexStackPanel),
		 new FrameworkPropertyMetadata(default(double?), FrameworkPropertyMetadataOptions.AffectsParentMeasure));
	
	public static readonly DependencyProperty MaxSlotSizeProperty = DependencyProperty.RegisterAttached
	  ("MaxSlotSize", typeof(double?), typeof(FlexStackPanel),
		 new FrameworkPropertyMetadata(default(double?), FrameworkPropertyMetadataOptions.AffectsParentMeasure));
	
	private static readonly DependencyPropertyKey IsOverflowedPropertyKey = DependencyProperty.RegisterAttachedReadOnly
	  ("IsOverflowed", typeof(bool), typeof(FlexStackPanel), new PropertyMetadata(default(bool)));
	
	public static readonly DependencyProperty IsOverflowedProperty = IsOverflowedPropertyKey.DependencyProperty;
	
	public static readonly DependencyProperty MeasureToSlotProperty = DependencyProperty.Register
	  ("MeasureToSlot", typeof(bool), typeof(FlexStackPanel), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));
	
	public static readonly DependencyProperty ShrinkOnOverflowProperty = DependencyProperty.RegisterAttached
	  ("ShrinkOnOverflow", typeof(bool), typeof(FlexStackPanel), new FrameworkPropertyMetadata(default(bool), FrameworkPropertyMetadataOptions.AffectsMeasure, OnAffectMeasurePropertyChanged));
	
	public static readonly DependencyProperty IgnoreMinConstraintsProperty = DependencyProperty.Register
	  ("IgnoreMinConstraints", typeof(bool), typeof(FlexStackPanel), new FrameworkPropertyMetadata(default(bool), FrameworkPropertyMetadataOptions.AffectsMeasure));
	
	public static readonly DependencyProperty IgnoreMaxConstraintsProperty = DependencyProperty.Register
	  ("IgnoreMaxConstraints", typeof(bool), typeof(FlexStackPanel), new FrameworkPropertyMetadata(default(bool), FrameworkPropertyMetadataOptions.AffectsMeasure));
	
	#endregion
	
	#region Fields
	
	private readonly Dictionary<UIElement, Size> childToConstraint = new Dictionary<UIElement, Size>();
	private bool isMeasureDirty;
	private bool isMeasureOverrideInProgress;
	private bool isHorizontal = true;
	private List<UIElement> orderedSequence;
	private Slot[] slots;
	
	#endregion
	
	#region Ctors
	
	static FlexStackPanel() {
		DefaultStyleKeyProperty.OverrideMetadata
		  (typeof(FlexStackPanel),
		   new FrameworkPropertyMetadata(typeof(FlexStackPanel)));
	}
	
	#endregion
	
	#region Properties
	
	protected override bool HasLogicalOrientation {
		get { return true; }
	}
	
	public bool HasOverflowedChildren {
		get { return (bool)GetValue(HasOverflowedChildrenProperty); }
		private set { SetValue(HasOverflowedChildrenPropertyKey, value); }
	}
	
	public bool IgnoreMaxConstraints {
		get { return (bool)GetValue(IgnoreMaxConstraintsProperty); }
		set { SetValue(IgnoreMaxConstraintsProperty, value); }
	}
	
	public bool IgnoreMinConstraints {
		get { return (bool)GetValue(IgnoreMinConstraintsProperty); }
		set { SetValue(IgnoreMinConstraintsProperty, value); }
	}
	
	protected override Orientation LogicalOrientation {
		get { return Orientation; }
	}
	
	public bool MeasureToSlot {
		get { return (bool)GetValue(MeasureToSlotProperty); }
		set { SetValue(MeasureToSlotProperty, value); }
	}
	
	public Orientation Orientation {
		get { return (Orientation)GetValue(OrientationProperty); }
		set { SetValue(OrientationProperty, value); }
	}
	
	public FlexStretchDirection StretchDirection {
		get { return (FlexStretchDirection)GetValue(StretchDirectionProperty); }
		set { SetValue(StretchDirectionProperty, value); }
	}
	
	#endregion
	
	#region Methods
	
	public static bool GetIsOverflowed(UIElement element) {
		return (bool)element.GetValue(IsOverflowedProperty);
	}
	
	public static double? GetMaxSlotSize(UIElement element) {
		return (double?)element.GetValue(MaxSlotSizeProperty);
	}
	
	public static double? GetMinSlotSize(UIElement element) {
		return (double?)element.GetValue(MinSlotSizeProperty);
	}
	
	public static bool GetShrinkOnOverflow(UIElement element) {
		return (bool)element.GetValue(ShrinkOnOverflowProperty);
	}
	
	public static void SetMaxSlotSize(UIElement element, double? value) {
		element.SetValue(MaxSlotSizeProperty, value);
	}
	
	public static void SetMinSlotSize(UIElement element, double? value) {
		element.SetValue(MinSlotSizeProperty, value);
	}
	
	public static void SetShrinkOnOverflow(UIElement element, bool value) {
		element.SetValue(ShrinkOnOverflowProperty, value);
	}
	
	protected override Size ArrangeOverride(Size finalSize) {
		var size = new Size(isHorizontal ? 0 : finalSize.Width, !isHorizontal ? 0 : finalSize.Height);
		
		var childrenCount = Children.Count;
		
		var rc = new Rect();
		for (var index = 0; index < childrenCount; index++) {
			var child = orderedSequence[index];
			if (GetIsOverflowed(child)) {
				child.Arrange(new Rect());
				continue;
			}
			
			var slotVal = slots[index].Val;
			if (isHorizontal) {
				rc.Width = double.IsInfinity(slotVal) ? child.DesiredSize.Width : slotVal;
				rc.Height = Math.Max(finalSize.Height, child.DesiredSize.Height);
				size.Width += rc.Width;
				size.Height = Math.Max(size.Height, rc.Height);
				child.Arrange(rc);
				rc.X += rc.Width;
			} else {
				rc.Width = Math.Max(finalSize.Width, child.DesiredSize.Width);
				rc.Height = double.IsInfinity(slotVal) ? child.DesiredSize.Height : slotVal;
				size.Width = Math.Max(size.Width, rc.Width);
				size.Height += rc.Height;
				child.Arrange(rc);
				rc.Y += rc.Height;
			}
		}
		
		return new Size(Math.Max(finalSize.Width, size.Width), Math.Max(finalSize.Height, size.Height));
	}
	
	protected override Size MeasureOverride(Size availableSize) {
		try {
			isMeasureOverrideInProgress = true;
			
			var ignoreMinConstraints = IgnoreMinConstraints;
			var ignoreMaxConstraints = IgnoreMaxConstraints;
			
			for (var i = 0; i < 3; i++) {
				isMeasureDirty = false;
				
				var childrenDesiredSize = new Size();
				
				var childrenCount = Children.Count;
				
				if (childrenCount == 0)
					return childrenDesiredSize;
				
				var childConstraint = GetChildrenConstraint(availableSize);
				var uniSize = GetUniformSize(availableSize);
				
				slots = new Slot[childrenCount];
				
				orderedSequence = Children.Cast<UIElement>().ToList();
				
				for (var index = 0; index < childrenCount; index++) {
					if (isMeasureDirty)
						break;
					
					var child = orderedSequence[index];
					var minLength = (ignoreMinConstraints ? (double?)0.0 : null) ?? GetMinSlotSize(child);
					var maxLength = (ignoreMaxConstraints ? (double?)0.0 : null) ?? GetMaxSlotSize(child);
					
					var frameworkChild = child as FrameworkElement;
					if (frameworkChild != null) {
						var margin = frameworkChild.Margin;
						minLength = minLength ?? (isHorizontal ? frameworkChild.MinWidth : frameworkChild.MinHeight);
						maxLength = maxLength ?? (isHorizontal ? frameworkChild.MaxWidth + margin.Width() : frameworkChild.MaxHeight + margin.Height());
					}
					
					minLength = minLength ?? 0.0;
					maxLength = maxLength ?? double.PositiveInfinity;
					
					MeasureChild(child, childConstraint);
					
					if (isHorizontal) {
						childrenDesiredSize.Width += child.DesiredSize.Width;
						slots[index] = new Slot(minLength.Value, maxLength.Value, StretchDirection == FlexStretchDirection.Both ? uniSize.Width : child.DesiredSize.Width);
						childrenDesiredSize.Height = Math.Max(childrenDesiredSize.Height, child.DesiredSize.Height);
					} else {
						childrenDesiredSize.Height += child.DesiredSize.Height;
						slots[index] = new Slot(minLength.Value, maxLength.Value, StretchDirection == FlexStretchDirection.Both ? uniSize.Height : child.DesiredSize.Height);
						childrenDesiredSize.Width = Math.Max(childrenDesiredSize.Width, child.DesiredSize.Width);
					}
				}
				
				if (isMeasureDirty)
					continue;
				
				var current = slots.Sum(s => s.Val);
				var target = GetSizePart(availableSize);
				
				var finalSize = new Size
				  (Math.Min(availableSize.Width, isHorizontal ? current : childrenDesiredSize.Width),
				   Math.Min(availableSize.Height, isHorizontal ? childrenDesiredSize.Height : current));
				
				if (double.IsInfinity(target))
					return finalSize;
				
				RecalcSlots(current, target);
				
				// Current length is greater than available and we have no possibility to stretch down -> mark elements as overflow
				current = 0.0;
				for (var index = 0; index < childrenCount; index++) {
					var child = orderedSequence[index];
					
					var slot = slots[index];
					
					if (GetShrinkOnOverflow(child) && IsGreater(current + slot.Val, target, Tolerance) && IsGreater(target, current, Tolerance)) {
						var rest = IsGreater(target, current, Tolerance) ? target - current : 0.0;
						if (IsGreater(rest, slot.Min, Tolerance))
							slot.Val = rest;
					}
					
					current += slot.Val;
					SetIsOverflowed(child, IsGreater(current, target, Tolerance));
				}
				
				HasOverflowedChildren = current > target;
				
				if (MeasureToSlot)
					RemeasureChildren(finalSize);
				
				finalSize = new Size
				  (Math.Min(availableSize.Width, isHorizontal ? target : childrenDesiredSize.Width),
				   Math.Min(availableSize.Height, isHorizontal ? childrenDesiredSize.Height : target));
				
				if (isMeasureDirty)
					continue;
				
				return finalSize;
			}
		}
		finally {
			isMeasureOverrideInProgress = false;
		}
		return new Size();
	}
	
	protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved) {
		base.OnVisualChildrenChanged(visualAdded, visualRemoved);
		
		var removedUIElement = visualRemoved as UIElement;
		
		if (removedUIElement != null)
			childToConstraint.Remove(removedUIElement);
	}
	
	private static void ExpandSlots(IEnumerable<Slot> slots, double target) {
		var sortedSlots = slots.OrderBy(v => v.Val).ToList();
		var maxValidTarget = sortedSlots.Sum(s => s.Max);
		if (maxValidTarget < target) {
			foreach (var slot in sortedSlots)
				slot.Val = slot.Max;
			return;
		}
		do {
			var tmpTarget = target;
			for (var iSlot = sortedSlots.Count - 1; iSlot >= 0; iSlot--) {
				var slot = sortedSlots[iSlot];
				if (slot.Val * (iSlot + 1) <= tmpTarget) {
					var avg = tmpTarget / (iSlot + 1);
					var success = true;
					for (var jSlot = iSlot; jSlot >= 0; jSlot--) {
						var tslot = sortedSlots[jSlot];
						tslot.Val = Math.Min(tslot.Max, avg);
						
						// Max constraint skip success expand on this iteration
						if (Math.Abs(avg - tslot.Val) <= Tolerance) continue;
						
						target -= tslot.Val;
						success = false;
						sortedSlots.RemoveAt(jSlot);
					}
					if (success)
						return;
					
					break;
				}
				tmpTarget -= slot.Val;
			}
		} while (sortedSlots.Count > 0);
	}
	
	private Size GetChildrenConstraint(Size availableSize) {
		return new Size
		  (isHorizontal ? double.PositiveInfinity : availableSize.Width,
		   !isHorizontal ? double.PositiveInfinity : availableSize.Height);
	}
	
	private double GetSizePart(Size size) {
		return isHorizontal ? size.Width : size.Height;
	}
	
	private Size GetUniformSize(Size availableSize) {
		var childrenCount = Children.Count;
		if (childrenCount == 0)
			return new Size();
		
		return new Size
		  (isHorizontal ? availableSize.Width / childrenCount : availableSize.Width,
		   !isHorizontal ? availableSize.Height / childrenCount : availableSize.Height);
	}
	
	private static bool IsGreater(double a, double b, double tolerance) {
		return a - b > tolerance;
	}
	
	private void MeasureChild(UIElement child, Size childConstraint) {
		Size lastConstraint;
		if ((child.IsMeasureValid && childToConstraint.TryGetValue(child, out lastConstraint) && lastConstraint.Equals(childConstraint))) return;
		
		child.Measure(childConstraint);
		childToConstraint[child] = childConstraint;
	}
	
	private void OnAffectMeasureChanged() {
		if (isMeasureOverrideInProgress)
			isMeasureDirty = true;
	}
	
	private static void OnAffectMeasurePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs) {
		var flexStackPanel = VisualTreeHelper.GetParent(dependencyObject) as FlexStackPanel;
		if (flexStackPanel != null)
			flexStackPanel.OnAffectMeasureChanged();
	}
	
	private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
		var panel = (FlexStackPanel)d;
		panel.isHorizontal = panel.Orientation == Orientation.Horizontal;
	}
	
	private void RecalcSlots(double current, double target) {
		var shouldShrink = IsGreater(current, target, Tolerance) && (StretchDirection == FlexStretchDirection.DownOnly || StretchDirection == FlexStretchDirection.Both);
		var shouldExpand = IsGreater(target, current, Tolerance) && (StretchDirection == FlexStretchDirection.UpOnly || StretchDirection == FlexStretchDirection.Both);
		
		if (shouldShrink)
			ShrinkSlots(slots, target);
		else if (shouldExpand)
			ExpandSlots(slots, target);
	}
	
	private void RemeasureChildren(Size availableSize) {
		var childrenCount = Children.Count;
		if (childrenCount == 0)
			return;
		
		var childConstraint = GetChildrenConstraint(availableSize);
		for (var index = 0; index < childrenCount; index++) {
			var child = orderedSequence[index];
			if (Math.Abs(GetSizePart(child.DesiredSize) - slots[index].Val) > Tolerance)
				MeasureChild(child, new Size(isHorizontal ? slots[index].Val : childConstraint.Width,
											!isHorizontal ? slots[index].Val : childConstraint.Height));
		}
	}
	
	private static void SetIsOverflowed(UIElement element, bool value) {
		element.SetValue(IsOverflowedPropertyKey, value);
	}
	
	private static void ShrinkSlots(IEnumerable<Slot> slots, double target) {
		var sortedSlots = slots.OrderBy(v => v.Val).ToList();
		var minValidTarget = sortedSlots.Sum(s => s.Min);
		if (minValidTarget > target) {
			foreach (var slot in sortedSlots)
				slot.Val = slot.Min;
			return;
		}
		do {
			var tmpTarget = target;
			for (var iSlot = 0; iSlot < sortedSlots.Count; iSlot++) {
				var slot = sortedSlots[iSlot];
				if (slot.Val * (sortedSlots.Count - iSlot) >= tmpTarget) {
					var avg = tmpTarget / (sortedSlots.Count - iSlot);
					var success = true;
					for (var jSlot = iSlot; jSlot < sortedSlots.Count; jSlot++) {
						var tslot = sortedSlots[jSlot];
						tslot.Val = Math.Max(tslot.Min, avg);
						
						// Min constraint skip success expand on this iteration
						if (Math.Abs(avg - tslot.Val) <= Tolerance) continue;
						
						target -= tslot.Val;
						success = false;
						sortedSlots.RemoveAt(jSlot);
						jSlot--;
					}
					if (success)
						return;
					
					break;
				}
				tmpTarget -= slot.Val;
			}
		} while (sortedSlots.Count > 0);
	}
	
	#endregion
	
	#region Nested type: Slot
	
	private class Slot {
		#region Fields
		
		public readonly double Max;
		public readonly double Min;
		public double Val;
		
		#endregion
		
		#region Ctors
		
		public Slot(double min, double max, double val) {
			Min = min;
			Max = max;
			Val = val;
			
			Val = Math.Max(min, val);
			Val = Math.Min(max, Val);
		}
		
		#endregion
	}
	
	#endregion
}

file static class MeasureUtil {
	public static double Height(this Thickness thickness) {
		return thickness.Top + thickness.Bottom;
	}
	
	public static double Width(this Thickness thickness) {
		return thickness.Left + thickness.Right;
	}
}
