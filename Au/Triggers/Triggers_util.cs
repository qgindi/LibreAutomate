namespace Au.Triggers;

static class TrigUtil {
	/// <summary>
	/// Gets left and right modifiers. Uses <b>keys.isPressed</b>.
	/// Returns <c>modL | modR</c>.
	/// </summary>
	public static KMod GetModLR(out KMod modL, out KMod modR) {
		KMod L = 0, R = 0;
		if (keys.isPressed(KKey.LCtrl)) L |= KMod.Ctrl;
		if (keys.isPressed(KKey.LShift)) L |= KMod.Shift;
		if (keys.isPressed(KKey.LAlt)) L |= KMod.Alt;
		if (keys.isPressed(KKey.Win)) L |= KMod.Win;
		if (keys.isPressed(KKey.RCtrl)) R |= KMod.Ctrl;
		if (keys.isPressed(KKey.RShift)) R |= KMod.Shift;
		if (keys.isPressed(KKey.RAlt)) R |= KMod.Alt;
		if (keys.isPressed(KKey.RWin)) R |= KMod.Win;
		modL = L; modR = R;
		return L | R;
	}
}
