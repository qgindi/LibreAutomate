namespace Au.More
{
	/// <summary>
	/// <see cref="TextWriter"/> optimized for writing full lines.
	/// </summary>
	/// <remarks>
	/// Derived class must override <see cref="WriteLineNow"/>. Don't need to override <b>Write</b>/<b>WriteLine</b>.
	/// If <b>Write</b> called with text that does not end with <c>'\n'</c>, just accumulates the text in this variable.
	/// When called <b>WriteLine</b> or <b>Flush</b> or <b>Write</b> with text that ends with <c>'\n'</c>, calls <see cref="WriteLineNow"/> of the derived class.
	/// </remarks>
	internal abstract class LineWriter_ : TextWriter
	{
		StringBuilder _b;

		/// <summary>
		/// Returns <b>Encoding.Unicode</b>.
		/// </summary>
		public override Encoding Encoding => Encoding.Unicode;

		/// <summary>
		/// If <i>value</i> is <c>'\n'</c>, writes accumulated text as full line and clears accumulated text, else just appends <i>value</i> to the accumulated text.
		/// </summary>
		public override void Write(char value) {
			//qm2.write((int)value, value);
			if (value == '\n') {
				WriteLine();
			} else {
				(_b ??= new StringBuilder()).Append(value);
			}
			base.Write(value);
		}

		/// <summary>
		/// If <i>value</i> ends with <c>'\n'</c>, writes line (accumulated text + <i>value</i>) and clears accumulated text, else just appends <i>value</i> to the accumulated text.
		/// </summary>
		public override void Write(string value) {
			//qm2.write($"'{value}'");
			//qm2.write("Write", $"'{value}'", value.ToCharArray());
			if (value.NE()) return;
			if (value.Ends('\n')) {
				WriteLine(value[..^(value.Ends("\r\n") ? 2 : 1)]);
			} else {
				(_b ??= new StringBuilder()).Append(value);
			}
		}

		/// <summary>
		/// If this variable contains accumulated text, writes it as full line and clears it. Else writes empty line.
		/// </summary>
		public override void WriteLine() {
			WriteLineNow(_PrependBuilder(null));
		}

		/// <summary>
		/// Writes line (accumulated text + <i>value</i>) and clears accumulated text.
		/// </summary>
		public override void WriteLine(string value) {
			//qm2.write("WriteLine", $"'{value}'", value.ToCharArray());
			WriteLineNow(_PrependBuilder(value));
		}

		string _PrependBuilder(string value) {
			if (_b != null && _b.Length > 0) {
				value = _b.ToString() + value;
				_b.Clear();
			}
			return value;
		}

		/// <summary>
		/// If this variable contains accumulated text, writes it as full line and clears it.
		/// </summary>
		public override void Flush() {
			var s = _PrependBuilder(null);
			if (!s.NE()) WriteLineNow(s);
		}

		/// <summary>
		/// Called to write full line.
		/// </summary>
		/// <param name="s">Line text. Does not end with line break characters.</param>
		protected abstract void WriteLineNow(string s);
	}
}
