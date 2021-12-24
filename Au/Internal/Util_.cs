namespace Au.More
{
	//static class Util_
	//{

	//}
}

#if !true
namespace Au
{
	/// <summary>
	/// 
	/// </summary>
	public struct TestDoc
	{
		/// <summary>
		/// Sum One.
		/// </summary>
		/// <param name="i">Iiii.</param>
		/// <returns>Retto.</returns>
		/// <exception cref="ArgumentException">i is invalid.</exception>
		/// <exception cref="AuException">Failed.</exception>
		/// <remarks>Remmo.</remarks>
		/// <example>
		/// <code>TestDoc.One(1);</code>
		/// </example>
		public static int One(int i) {
			print.it(i);
			return 0;
		}

		/// <inheritdoc cref="One(int)"/>
		public static int One(int i, string s) {
			print.it(i, s);
			return 0;
		}

		/// <param name="b">Boo.</param>
		/// <exception cref="NotFoundException">Not found.</exception>
		/// <inheritdoc cref="One"/>
		public static int One(bool b, int i) {
			print.it(i, b);
			return 0;
		}

		/// <param name="b">Boo no except.</param>
		/// <inheritdoc cref="One"/>
		public static int One(string b, int i) {
			print.it(i, b);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <inheritdoc cref="One" path="/param"/>
		/// <param name="s">Last.</param>
		public static int One(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}
		///// <inheritdoc cref="One" path="/param[@name='i']"/>

		/// <summary>
		/// Sum Two.
		/// </summary>
		/// <param name="b">Boo.</param>
		/// <exception cref="TimeoutException">Timeout.</exception>
		/// <inheritdoc cref="One"/>
		public static int Two(double b, int i) {
			print.it(i, b);
			return 0;
		}

		/// <summary>
		/// Sum Two 2.
		/// </summary>
		/// <exception cref="TimeoutException">Timeout.</exception>
		/// <inheritdoc cref="One"/>
		public static int TwoNoParam(int i) {
			print.it(i);
			return 0;
		}

		/// <summary>
		/// Sum Two 2.
		/// </summary>
		/// <param name="b">Boo.</param>
		/// <inheritdoc cref="One"/>
		public static int TwoNoExcept(double b, int i) {
			print.it(i, b);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <inheritdoc cref="One" path="/param"/>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <inheritdoc cref="One(int)" path="/param"/>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara2(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <inheritdoc cref="One" path="/param[@name='i']"/>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara3(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <inheritdoc cref="One(int)" path="/param[@name='i']"/>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara4(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <param name="i"><inheritdoc cref="One"/></param>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara5(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <param name="i"><inheritdoc cref="One" path="/param"/></param>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara6(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <param name="i"><inheritdoc cref="One" path="/param[@name='i']"/></param>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara7(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}

		/// <inheritdoc cref="One" path="/summary"/>
		/// <param name="b">Boo.</param>
		/// <param name="i"><inheritdoc cref="One(int)" path="/param[@name='i']"/></param>
		/// <param name="s">Last.</param>
		public static int TwoInheritPara8(double b, int i, string s) {
			print.it(i, b, s);
			return 0;
		}
	}
}
#endif
