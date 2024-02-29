namespace Au.Types;

/// <summary>
/// Wraps some UI Automation API.
/// </summary>
static class UiaUtil {
	public static UiaApi.IUIAutomation Uia => _uia ??= new UiaApi.CUIAutomation() as UiaApi.IUIAutomation;
	[ThreadStatic] static UiaApi.IUIAutomation _uia;
	
	/// <summary>
	/// Gets UI element from point.
	/// </summary>
	/// <param name="xy">Screen coordinates.</param>
	/// <returns><c>null</c> if failed.</returns>
	public static UiaApi.IUIAutomationElement ElementFromPoint(POINT xy) {
		return 0 == Uia.ElementFromPoint(xy, out var e) ? e : null;
	}
	
	/// <summary>
	/// Gets the focused element.
	/// </summary>
	/// <returns><c>null</c> if failed.</returns>
	public static UiaApi.IUIAutomationElement ElementFocused() {
		return 0 == Uia.GetFocusedElement(out var e) ? e : null;
	}
	
	///// <summary>
	///// Gets the container control of this or nearest ancestor element that can retrieve it.
	///// </summary>
	//public static wnd Hwnd(this UiaApi.IUIAutomationElement t) {
	//	if (0 == t.get_CurrentNativeWindowHandle(out var w) && !w.Is0) return w;
		
	//	if (0 == Uia.get_RawViewWalker(out var walker)) {
	//		while (0 == walker.GetParentElement(t, out var p) && p != null && p != t) {
	//			t = p;
	//			if (0 == t.get_CurrentNativeWindowHandle(out w) && !w.Is0) return w;
	//		}
	//	}
		
	//	return default;
	//}
	
	/// <summary>
	/// Gets caret rectangle in screen from this focused element.
	/// </summary>
	public static bool GetCaretRect(this UiaApi.IUIAutomationElement t, out RECT r) {
		if (0 == t.GetCurrentPattern(UiaApi.UIA_TextPattern2Id, out var o) && o is UiaApi.IUIAutomationTextPattern2 p2) {
			if (0 == p2.GetCaretRange(out bool isActive, out var tr) && tr != null /*&& isActive*/) {
				return tr.GetRect(out r, t);
			}
		}
		if (0 == t.GetCurrentPattern(UiaApi.UIA_TextPatternId, out o) && o is UiaApi.IUIAutomationTextPattern p) {
			if (0 == p.GetSelection(out var ranges) && 0 == ranges.GetElement(0, out var tr)) {
				return tr.GetRect(out r, t, selectionToCaret: true);
			}
		}
		r = default;
		return false;
	}
	
	/// <summary>
	/// Gets rectangle in screen.
	/// </summary>
	public static unsafe bool GetRect(this UiaApi.IUIAutomationTextRange t, out RECT r, UiaApi.IUIAutomationElement e, bool selectionToCaret = false) {
		if (_GetRect(t, out r)) {
			if (selectionToCaret) r.left = r.right - 1;
			return true;
		}
		
		//probably no selection
		
		if (0 == t.ExpandToEnclosingUnit(UiaApi.TextUnit.TextUnit_Character) && _GetRect(t, out r)) {
			r.right = r.left + 1;
			return true;
		}
		
		//probably caret at the end
		
		if (0 == t.MoveEndpointByUnit(UiaApi.TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start, UiaApi.TextUnit.TextUnit_Character, -1, out int m) && m < 0 && _GetRect(t, out r)) {
			//moved to previous line?
			if (0 == t.GetText(2, out var s) && !s.NE() && s[0] is '\r' or '\n' && 0 == t.ExpandToEnclosingUnit(UiaApi.TextUnit.TextUnit_Line) && _GetRect(t, out var r2)) {
				r.Offset(0, r2.Height);
				r.right = (r.left = r2.left) + 1;
			} else {
				r.right = (r.left = r.right) + 1;
			}
			return true;
		}
		
		//probably no text
		
		//get the left edge of e rect
		if (0 == e.get_CurrentBoundingRectangle(out r)) {
			int dpi = Dpi.OfWindow(wnd.active);
			int h = (int)Dpi.Unscale(r.Height, dpi);
			if (h < 111) { //assume it's a single-line edit control
				if (h > 32) r.top = r.bottom - Dpi.Scale(32, dpi); //get the bottom max 32 logical pixels
				r.right = r.left + 1;
				return true;
			}
		}
		
		return false;
		
		static unsafe bool _GetRect(UiaApi.IUIAutomationTextRange t, out RECT r) {
			r = default;
			if (0 != t.GetBoundingRectangles(out var sap) || sap == null) return false;
			uint n = sap->rgsabound.cElements / 4;
			if (n > 0) {
				var p = (double*)sap->pvData + sap->rgsabound.cElements - 4;
				r = new(p[0].ToInt(), p[1].ToInt(), p[2].ToInt(), p[3].ToInt());
			}
			UiaApi.SafeArrayDestroy(sap);
			return n > 0;
		}
	}
	
	public static bool GetCaretRectInPowerShell(out RECT r) {
		//GetGUIThreadInfo and MSAA don't work with PowerShell.
		//	Does not support IUIAutomationTextPattern2.
		//	IUIAutomationTextPattern.GetSelection -> IUIAutomationTextRange.GetBoundingRectangles returns client coord, which may be fixed in the future.
		//	Win+; doesn't work too. But PhraseExpress works. And IME (interesting: temporarily replaces the caret).
		//	I would't care, but this was a user request.
		//Now instead using an undocumented PS feature.
		
		var t = ElementFocused();
		if (t != null && 0 == t.get_CurrentControlType(out var ct) && ct == UiaApi.TypeId.Edit) {
			if (0 == Uia.get_RawViewWalker(out var walker)) {
				UiaApi.IUIAutomationElement e = null;
				while (0 == (e == null ? walker.GetFirstChildElement(t, out e) : walker.GetNextSiblingElement(e, out e)) && e != null) {
					//if (0 == e.get_CurrentControlType(out ct)) print.it(ct);
					//if (0 == e.get_CurrentAutomationId(out var ai)) print.it(ai);
					if (0 == e.get_CurrentAutomationId(out var ai) && ai.Find("Caret", true) >= 0 && 0 == e.get_CurrentControlType(out ct) && ct == UiaApi.TypeId.Custom) {
						if (0 == e.get_CurrentBoundingRectangle(out r)) {
							return true;
						}
					}
				}
				//There are 3 child elements. The first is caret.
				//This is slow. Tested MSAA, but it gets only child element "selection", and cannot get rect when selection empty.
			}
		}
		r = default;
		return false;
	}
	
	///// <summary>
	///// Gets text of TextPattern paragraph from point.
	///// </summary>
	///// <param name="t"></param>
	///// <param name="xy">Screen coordinates.</param>
	///// <returns><c>null</c> if the element does not support TextPattern or if failed.</returns>
	//public static string PatternTextFromPoint(this UiaApi.IUIAutomationElement t, POINT xy) {
	//	if (0 == t.GetCurrentPattern(UiaApi.UIA_TextPatternId, out var o) && o is UiaApi.IUIAutomationTextPattern p) {
	//		if (0 == p.RangeFromPoint(xy, out var tr) && 0 == tr.ExpandToEnclosingUnit(UiaApi.TextUnit.TextUnit_Paragraph)) {
	//			if (0 == tr.GetText(5000, out var s)) return s;
	//		}
	//	}
	//	return null;
	//}
	
	///// <summary>
	///// Gets text of ValuePattern.
	///// </summary>
	///// <param name="t"></param>
	///// <returns><c>null</c> if the element does not support ValuePattern or if failed.</returns>
	//public static string ValueText(this UiaApi.IUIAutomationElement t) {
	//	if (0 == t.GetCurrentPattern(UiaApi.UIA_ValuePatternId, out var o) && o is UiaApi.IUIAutomationValuePattern p) {
	//		if (0 == p.get_CurrentValue(out var s)) return s;
	//	}
	//	return null;
	//}
}

#pragma warning disable 1591, 649, 169

/// <summary>
/// Declarations of some UI Automation API.
/// </summary>
unsafe class UiaApi : NativeApi {
	
	[ComImport, Guid("ff48dba4-60ef-4201-aa87-54103eef594e"), ClassInterface(ClassInterfaceType.None)]
	internal class CUIAutomation { }
	
	[ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomation {
		[PreserveSig] int CompareElements(IUIAutomationElement el1, IUIAutomationElement el2, [MarshalAs(UnmanagedType.Bool)] out bool areSame);
		[PreserveSig] int CompareRuntimeIds(SAFEARRAY* runtimeId1, SAFEARRAY* runtimeId2, [MarshalAs(UnmanagedType.Bool)] out bool areSame);
		[PreserveSig] int GetRootElement(out IUIAutomationElement root);
		[PreserveSig] int ElementFromHandle(void* hwnd, out IUIAutomationElement element);
		[PreserveSig] int ElementFromPoint(POINT pt, out IUIAutomationElement element);
		[PreserveSig] int GetFocusedElement(out IUIAutomationElement element);
		[PreserveSig] int GetRootElementBuildCache(IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement root);
		[PreserveSig] int ElementFromHandleBuildCache(void* hwnd, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement element);
		[PreserveSig] int ElementFromPointBuildCache(POINT pt, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement element);
		[PreserveSig] int GetFocusedElementBuildCache(IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement element);
		[PreserveSig] int CreateTreeWalker(IUIAutomationCondition pCondition, out IUIAutomationTreeWalker walker);
		[PreserveSig] int get_ControlViewWalker(out IUIAutomationTreeWalker walker);
		[PreserveSig] int get_ContentViewWalker(out IUIAutomationTreeWalker walker);
		[PreserveSig] int get_RawViewWalker(out IUIAutomationTreeWalker walker);
		[PreserveSig] int get_RawViewCondition(out IUIAutomationCondition condition);
		[PreserveSig] int get_ControlViewCondition(out IUIAutomationCondition condition);
		[PreserveSig] int get_ContentViewCondition(out IUIAutomationCondition condition);
		[PreserveSig] int CreateCacheRequest(out IUIAutomationCacheRequest cacheRequest);
		[PreserveSig] int CreateTrueCondition(out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateFalseCondition(out IUIAutomationCondition newCondition);
		[PreserveSig] int CreatePropertyCondition(int propertyId, object value, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreatePropertyConditionEx(int propertyId, object value, PropertyConditionFlags flags, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateAndCondition(IUIAutomationCondition condition1, IUIAutomationCondition condition2, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateAndConditionFromArray(SAFEARRAY* conditions, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateAndConditionFromNativeArray([MarshalAs(UnmanagedType.LPArray)][In] IUIAutomationCondition[] conditions, int conditionCount, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateOrCondition(IUIAutomationCondition condition1, IUIAutomationCondition condition2, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateOrConditionFromArray(SAFEARRAY* conditions, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateOrConditionFromNativeArray([MarshalAs(UnmanagedType.LPArray)][In] IUIAutomationCondition[] conditions, int conditionCount, out IUIAutomationCondition newCondition);
		[PreserveSig] int CreateNotCondition(IUIAutomationCondition condition, out IUIAutomationCondition newCondition);
		[PreserveSig] int AddAutomationEventHandler(int eventId, IUIAutomationElement element, TreeScope scope, IUIAutomationCacheRequest cacheRequest, IUIAutomationEventHandler handler);
		[PreserveSig] int RemoveAutomationEventHandler(int eventId, IUIAutomationElement element, IUIAutomationEventHandler handler);
		[PreserveSig] int AddPropertyChangedEventHandlerNativeArray(IUIAutomationElement element, TreeScope scope, IUIAutomationCacheRequest cacheRequest, IUIAutomationPropertyChangedEventHandler handler, [MarshalAs(UnmanagedType.LPArray)][In] int[] propertyArray, int propertyCount);
		[PreserveSig] int AddPropertyChangedEventHandler(IUIAutomationElement element, TreeScope scope, IUIAutomationCacheRequest cacheRequest, IUIAutomationPropertyChangedEventHandler handler, SAFEARRAY* propertyArray);
		[PreserveSig] int RemovePropertyChangedEventHandler(IUIAutomationElement element, IUIAutomationPropertyChangedEventHandler handler);
		[PreserveSig] int AddStructureChangedEventHandler(IUIAutomationElement element, TreeScope scope, IUIAutomationCacheRequest cacheRequest, IUIAutomationStructureChangedEventHandler handler);
		[PreserveSig] int RemoveStructureChangedEventHandler(IUIAutomationElement element, IUIAutomationStructureChangedEventHandler handler);
		[PreserveSig] int AddFocusChangedEventHandler(IUIAutomationCacheRequest cacheRequest, IUIAutomationFocusChangedEventHandler handler);
		[PreserveSig] int RemoveFocusChangedEventHandler(IUIAutomationFocusChangedEventHandler handler);
		[PreserveSig] int RemoveAllEventHandlers();
		[PreserveSig] int IntNativeArrayToSafeArray([MarshalAs(UnmanagedType.LPArray)][In] int[] array, int arrayCount, out SAFEARRAY* safeArray);
		[PreserveSig] int IntSafeArrayToNativeArray(SAFEARRAY* intArray, out int* array, out int arrayCount);
		[PreserveSig] int RectToVariant(RECT rc, out object var);
		[PreserveSig] int VariantToRect(object var, out RECT rc);
		[PreserveSig] int SafeArrayToRectNativeArray(SAFEARRAY* rects, out RECT* rectArray, out int rectArrayCount);
		[PreserveSig] int CreateProxyFactoryEntry();
		[PreserveSig] int get_ProxyFactoryMapping();
		[PreserveSig] int GetPropertyProgrammaticName(int property, out string name);
		[PreserveSig] int GetPatternProgrammaticName(int pattern, out string name);
		[PreserveSig] int PollForPotentialSupportedPatterns(IUIAutomationElement pElement, out SAFEARRAY* patternIds, out SAFEARRAY* patternNames);
		[PreserveSig] int PollForPotentialSupportedProperties(IUIAutomationElement pElement, out SAFEARRAY* propertyIds, out SAFEARRAY* propertyNames);
		[PreserveSig] int CheckNotSupported(object value, [MarshalAs(UnmanagedType.Bool)] out bool isNotSupported);
		[PreserveSig] int get_ReservedNotSupportedValue([MarshalAs(UnmanagedType.IUnknown)] out object notSupportedValue);
		[PreserveSig] int get_ReservedMixedAttributeValue([MarshalAs(UnmanagedType.IUnknown)] out object mixedAttributeValue);
		[PreserveSig] int ElementFromIAccessible();
		[PreserveSig] int ElementFromIAccessibleBuildCache();
	}
	
	[ComImport, Guid("c270f6b5-5c69-4290-9745-7a7f97169468"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationFocusChangedEventHandler {
		[PreserveSig] int HandleFocusChangedEvent(IUIAutomationElement sender);
	}
	
	[ComImport, Guid("e81d1b4e-11c5-42f8-9754-e7036c79f054"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationStructureChangedEventHandler {
		[PreserveSig] int HandleStructureChangedEvent(IUIAutomationElement sender, StructureChangeType changeType, SAFEARRAY* runtimeId);
	}
	
	[ComImport, Guid("40cd37d4-c756-4b0c-8c6f-bddfeeb13b50"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationPropertyChangedEventHandler {
		[PreserveSig] int HandlePropertyChangedEvent(IUIAutomationElement sender, int propertyId, object newValue);
	}
	
	[ComImport, Guid("146c3c17-f12e-4e22-8c27-f894b9b79c69"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationEventHandler {
		[PreserveSig] int HandleAutomationEvent(IUIAutomationElement sender, int eventId);
	}
	
	[Flags]
	internal enum PropertyConditionFlags : uint {
		PropertyConditionFlags_None,
		PropertyConditionFlags_IgnoreCase,
		PropertyConditionFlags_MatchSubstring
	}
	
	[ComImport, Guid("4042c624-389c-4afc-a630-9df854a541fc"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationTreeWalker {
		[PreserveSig] int GetParentElement(IUIAutomationElement element, out IUIAutomationElement parent);
		[PreserveSig] int GetFirstChildElement(IUIAutomationElement element, out IUIAutomationElement first);
		[PreserveSig] int GetLastChildElement(IUIAutomationElement element, out IUIAutomationElement last);
		[PreserveSig] int GetNextSiblingElement(IUIAutomationElement element, out IUIAutomationElement next);
		[PreserveSig] int GetPreviousSiblingElement(IUIAutomationElement element, out IUIAutomationElement previous);
		[PreserveSig] int NormalizeElement(IUIAutomationElement element, out IUIAutomationElement normalized);
		[PreserveSig] int GetParentElementBuildCache(IUIAutomationElement element, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement parent);
		[PreserveSig] int GetFirstChildElementBuildCache(IUIAutomationElement element, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement first);
		[PreserveSig] int GetLastChildElementBuildCache(IUIAutomationElement element, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement last);
		[PreserveSig] int GetNextSiblingElementBuildCache(IUIAutomationElement element, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement next);
		[PreserveSig] int GetPreviousSiblingElementBuildCache(IUIAutomationElement element, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement previous);
		[PreserveSig] int NormalizeElementBuildCache(IUIAutomationElement element, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement normalized);
		[PreserveSig] int get_Condition(out IUIAutomationCondition condition);
	}
	
	[ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationElement {
		[PreserveSig] int SetFocus();
		[PreserveSig] int GetRuntimeId(out SAFEARRAY* runtimeId);
		[PreserveSig] int FindFirst(TreeScope scope, IUIAutomationCondition condition, out IUIAutomationElement found);
		[PreserveSig] int FindAll(TreeScope scope, IUIAutomationCondition condition, out IUIAutomationElementArray found);
		[PreserveSig] int FindFirstBuildCache(TreeScope scope, IUIAutomationCondition condition, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement found);
		[PreserveSig] int FindAllBuildCache(TreeScope scope, IUIAutomationCondition condition, IUIAutomationCacheRequest cacheRequest, out IUIAutomationElementArray found);
		[PreserveSig] int BuildUpdatedCache(IUIAutomationCacheRequest cacheRequest, out IUIAutomationElement updatedElement);
		[PreserveSig] int GetCurrentPropertyValue(int propertyId, out object retVal);
		[PreserveSig] int GetCurrentPropertyValueEx(int propertyId, [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue, out object retVal);
		[PreserveSig] int GetCachedPropertyValue(int propertyId, out object retVal);
		[PreserveSig] int GetCachedPropertyValueEx(int propertyId, [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue, out object retVal);
		[PreserveSig] int GetCurrentPatternAs(int patternId, in Guid riid, void** patternObject);
		[PreserveSig] int GetCachedPatternAs(int patternId, in Guid riid, void** patternObject);
		[PreserveSig] int GetCurrentPattern(int patternId, [MarshalAs(UnmanagedType.IUnknown)] out object patternObject);
		[PreserveSig] int GetCachedPattern(int patternId, [MarshalAs(UnmanagedType.IUnknown)] out object patternObject);
		[PreserveSig] int GetCachedParent(out IUIAutomationElement parent);
		[PreserveSig] int GetCachedChildren(out IUIAutomationElementArray children);
		[PreserveSig] int get_CurrentProcessId(out int retVal);
		[PreserveSig] int get_CurrentControlType(out TypeId retVal);
		[PreserveSig] int get_CurrentLocalizedControlType(out string retVal);
		[PreserveSig] int get_CurrentName(out string retVal);
		[PreserveSig] int get_CurrentAcceleratorKey(out string retVal);
		[PreserveSig] int get_CurrentAccessKey(out string retVal);
		[PreserveSig] int get_CurrentHasKeyboardFocus([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentIsKeyboardFocusable([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentIsEnabled([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentAutomationId(out string retVal);
		[PreserveSig] int get_CurrentClassName(out string retVal);
		[PreserveSig] int get_CurrentHelpText(out string retVal);
		[PreserveSig] int get_CurrentCulture(out int retVal);
		[PreserveSig] int get_CurrentIsControlElement([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentIsContentElement([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentIsPassword([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentNativeWindowHandle(out wnd retVal);
		[PreserveSig] int get_CurrentItemType(out string retVal);
		[PreserveSig] int get_CurrentIsOffscreen([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentOrientation(out OrientationType retVal);
		[PreserveSig] int get_CurrentFrameworkId(out string retVal);
		[PreserveSig] int get_CurrentIsRequiredForForm([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentItemStatus(out string retVal);
		[PreserveSig] int get_CurrentBoundingRectangle(out RECT retVal);
		[PreserveSig] int get_CurrentLabeledBy(out IUIAutomationElement retVal);
		[PreserveSig] int get_CurrentAriaRole(out string retVal);
		[PreserveSig] int get_CurrentAriaProperties(out string retVal);
		[PreserveSig] int get_CurrentIsDataValidForForm([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CurrentControllerFor(out IUIAutomationElementArray retVal);
		[PreserveSig] int get_CurrentDescribedBy(out IUIAutomationElementArray retVal);
		[PreserveSig] int get_CurrentFlowsTo(out IUIAutomationElementArray retVal);
		[PreserveSig] int get_CurrentProviderDescription(out string retVal);
		[PreserveSig] int get_CachedProcessId(out int retVal);
		[PreserveSig] int get_CachedControlType(out int retVal);
		[PreserveSig] int get_CachedLocalizedControlType(out string retVal);
		[PreserveSig] int get_CachedName(out string retVal);
		[PreserveSig] int get_CachedAcceleratorKey(out string retVal);
		[PreserveSig] int get_CachedAccessKey(out string retVal);
		[PreserveSig] int get_CachedHasKeyboardFocus([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedIsKeyboardFocusable([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedIsEnabled([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedAutomationId(out string retVal);
		[PreserveSig] int get_CachedClassName(out string retVal);
		[PreserveSig] int get_CachedHelpText(out string retVal);
		[PreserveSig] int get_CachedCulture(out int retVal);
		[PreserveSig] int get_CachedIsControlElement([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedIsContentElement([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedIsPassword([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedNativeWindowHandle(void** retVal);
		[PreserveSig] int get_CachedItemType(out string retVal);
		[PreserveSig] int get_CachedIsOffscreen([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedOrientation(out OrientationType retVal);
		[PreserveSig] int get_CachedFrameworkId(out string retVal);
		[PreserveSig] int get_CachedIsRequiredForForm([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedItemStatus(out string retVal);
		[PreserveSig] int get_CachedBoundingRectangle(out RECT retVal);
		[PreserveSig] int get_CachedLabeledBy(out IUIAutomationElement retVal);
		[PreserveSig] int get_CachedAriaRole(out string retVal);
		[PreserveSig] int get_CachedAriaProperties(out string retVal);
		[PreserveSig] int get_CachedIsDataValidForForm([MarshalAs(UnmanagedType.Bool)] out bool retVal);
		[PreserveSig] int get_CachedControllerFor(out IUIAutomationElementArray retVal);
		[PreserveSig] int get_CachedDescribedBy(out IUIAutomationElementArray retVal);
		[PreserveSig] int get_CachedFlowsTo(out IUIAutomationElementArray retVal);
		[PreserveSig] int get_CachedProviderDescription(out string retVal);
		[PreserveSig] int GetClickablePoint(out POINT clickable, [MarshalAs(UnmanagedType.Bool)] out bool gotClickable);
	}
	
	internal enum OrientationType {
		OrientationType_None,
		OrientationType_Horizontal,
		OrientationType_Vertical
	}
	
	[ComImport, Guid("b32a92b5-bc25-4078-9c08-d7ee95c48e03"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationCacheRequest {
		[PreserveSig] int AddProperty(int propertyId);
		[PreserveSig] int AddPattern(int patternId);
		[PreserveSig] int Clone(out IUIAutomationCacheRequest clonedRequest);
		[PreserveSig] int get_TreeScope(out TreeScope scope);
		[PreserveSig] int put_TreeScope(TreeScope scope);
		[PreserveSig] int get_TreeFilter(out IUIAutomationCondition filter);
		[PreserveSig] int put_TreeFilter(IUIAutomationCondition filter);
		[PreserveSig] int get_AutomationElementMode(out AutomationElementMode mode);
		[PreserveSig] int put_AutomationElementMode(AutomationElementMode mode);
	}
	
	[ComImport, Guid("14314595-b4bc-4055-95f2-58f2e42c9855"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationElementArray {
		[PreserveSig] int get_Length(out int length);
		[PreserveSig] int GetElement(int index, out IUIAutomationElement element);
	}
	
	[ComImport, Guid("352ffba8-0973-437c-a61f-f64cafd81df9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationCondition { }
	
	[Flags]
	internal enum TreeScope : uint {
		TreeScope_None,
		TreeScope_Element,
		TreeScope_Children,
		TreeScope_Descendants = 0x4,
		TreeScope_Parent = 0x8,
		TreeScope_Ancestors = 0x10,
		TreeScope_Subtree = 0x7
	}
	
	internal struct SAFEARRAY {
		public ushort cDims;
		public ushort fFeatures;
		public uint cbElements;
		public uint cLocks;
		public void* pvData;
		/*[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]*/
		public SAFEARRAYBOUND rgsabound;
	}
	
	internal struct SAFEARRAYBOUND {
		public uint cElements;
		public int lLbound;
	}
	
	internal enum AutomationElementMode {
		AutomationElementMode_None,
		AutomationElementMode_Full
	}
	
	internal enum StructureChangeType {
		StructureChangeType_ChildAdded,
		StructureChangeType_ChildRemoved,
		StructureChangeType_ChildrenInvalidated,
		StructureChangeType_ChildrenBulkAdded,
		StructureChangeType_ChildrenBulkRemoved,
		StructureChangeType_ChildrenReordered
	}
	
	internal enum TypeId {
		Button = 50000,
		Calendar = 50001,
		CheckBox = 50002,
		ComboBox = 50003,
		Edit = 50004,
		Hyperlink = 50005,
		Image = 50006,
		ListItem = 50007,
		List = 50008,
		Menu = 50009,
		MenuBar = 50010,
		MenuItem = 50011,
		ProgressBar = 50012,
		RadioButton = 50013,
		ScrollBar = 50014,
		Slider = 50015,
		Spinner = 50016,
		StatusBar = 50017,
		Tab = 50018,
		TabItem = 50019,
		Text = 50020,
		ToolBar = 50021,
		ToolTip = 50022,
		Tree = 50023,
		TreeItem = 50024,
		Custom = 50025,
		Group = 50026,
		Thumb = 50027,
		DataGrid = 50028,
		DataItem = 50029,
		Document = 50030,
		SplitButton = 50031,
		Window = 50032,
		Pane = 50033,
		Header = 50034,
		HeaderItem = 50035,
		Table = 50036,
		TitleBar = 50037,
		Separator = 50038,
		SemanticZoom = 50039,
		AppBar = 50040,
		CustomLandmark = 80000,
		FormLandmark = 80001,
		MainLandmark = 80002,
		NavigationLandmark = 80003,
		SearchLandmark = 80004,
	}
	
	[DllImport("oleaut32.dll", EntryPoint = "#16", PreserveSig = true)]
	internal static extern int SafeArrayDestroy(SAFEARRAY* psa);
	
	public const int UIA_TextPatternId = 10014;
	internal const int UIA_TextPattern2Id = 10024;
	
	[ComImport, Guid("32eba289-3583-42c9-9c59-3b6d9a1e9b6a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IUIAutomationTextPattern {
		[PreserveSig] int RangeFromPoint(POINT pt, out IUIAutomationTextRange range);
		[PreserveSig] int RangeFromChild(IUIAutomationElement child, out IUIAutomationTextRange range);
		[PreserveSig] int GetSelection(out IUIAutomationTextRangeArray ranges);
		[PreserveSig] int GetVisibleRanges(out IUIAutomationTextRangeArray ranges);
		[PreserveSig] int get_DocumentRange(out IUIAutomationTextRange range);
		[PreserveSig] int get_SupportedTextSelection(out SupportedTextSelection supportedTextSelection);
	}
	
	public enum SupportedTextSelection {
		SupportedTextSelection_None,
		SupportedTextSelection_Single,
		SupportedTextSelection_Multiple
	}
	
	[ComImport, Guid("ce4ae76a-e717-4c98-81ea-47371d028eb6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IUIAutomationTextRangeArray {
		[PreserveSig] int get_Length(out int length);
		[PreserveSig] int GetElement(int index, out IUIAutomationTextRange element);
	}
	
	[ComImport, Guid("a543cc6a-f4ae-494b-8239-c814481187a8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IUIAutomationTextRange {
		[PreserveSig] int Clone(out IUIAutomationTextRange clonedRange);
		[PreserveSig] int Compare(IUIAutomationTextRange range, [MarshalAs(UnmanagedType.Bool)] out bool areSame);
		[PreserveSig] int CompareEndpoints(TextPatternRangeEndpoint srcEndPoint, IUIAutomationTextRange range, TextPatternRangeEndpoint targetEndPoint, out int compValue);
		[PreserveSig] int ExpandToEnclosingUnit(TextUnit textUnit);
		[PreserveSig] int FindAttribute(int attr, object val, [MarshalAs(UnmanagedType.Bool)] bool backward, out IUIAutomationTextRange found);
		[PreserveSig] int FindText(string text, [MarshalAs(UnmanagedType.Bool)] bool backward, [MarshalAs(UnmanagedType.Bool)] bool ignoreCase, out IUIAutomationTextRange found);
		[PreserveSig] int GetAttributeValue(int attr, out object value);
		[PreserveSig] int GetBoundingRectangles(out SAFEARRAY* boundingRects);
		[PreserveSig] int GetEnclosingElement(out IUIAutomationElement enclosingElement);
		[PreserveSig] int GetText(int maxLength, out string text);
		[PreserveSig] int Move(TextUnit unit, int count, out int moved);
		[PreserveSig] int MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextUnit unit, int count, out int moved);
		[PreserveSig] int MoveEndpointByRange(TextPatternRangeEndpoint srcEndPoint, IUIAutomationTextRange range, TextPatternRangeEndpoint targetEndPoint);
		[PreserveSig] int Select();
		[PreserveSig] int AddToSelection();
		[PreserveSig] int RemoveFromSelection();
		[PreserveSig] int ScrollIntoView([MarshalAs(UnmanagedType.Bool)] bool alignToTop);
		[PreserveSig] int GetChildren(out IUIAutomationElementArray children);
	}
	
	[ComImport, Guid("506a921a-fcc9-409f-b23b-37eb74106872"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IUIAutomationTextPattern2 : IUIAutomationTextPattern {
		// IUIAutomationTextPattern
		[PreserveSig] new int RangeFromPoint(POINT pt, out IUIAutomationTextRange range);
		[PreserveSig] new int RangeFromChild(IUIAutomationElement child, out IUIAutomationTextRange range);
		[PreserveSig] new int GetSelection(out IUIAutomationTextRangeArray ranges);
		[PreserveSig] new int GetVisibleRanges(out IUIAutomationTextRangeArray ranges);
		[PreserveSig] new int get_DocumentRange(out IUIAutomationTextRange range);
		[PreserveSig] new int get_SupportedTextSelection(out SupportedTextSelection supportedTextSelection);
		// IUIAutomationTextPattern2
		[PreserveSig] int RangeFromAnnotation(IUIAutomationElement annotation, out IUIAutomationTextRange range);
		[PreserveSig] int GetCaretRange([MarshalAs(UnmanagedType.Bool)] out bool isActive, out IUIAutomationTextRange range);
	}
	
	public enum TextUnit {
		TextUnit_Character,
		TextUnit_Format,
		TextUnit_Word,
		TextUnit_Line,
		TextUnit_Paragraph,
		TextUnit_Page,
		TextUnit_Document
	}
	
	public enum TextPatternRangeEndpoint {
		TextPatternRangeEndpoint_Start,
		TextPatternRangeEndpoint_End
	}
}
