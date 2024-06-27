namespace Au.Controls;

[DebuggerStepThrough]
static unsafe class KApi {
	
	[DllImport("user32.dll")]
	internal static extern bool DrawFrameControl(IntPtr hdc, in RECT r, int type, int state);
	
	
}

