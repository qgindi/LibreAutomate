
//https://stackoverflow.com/questions/14243284/how-can-i-retrieve-string-literals-using-reflection

//#define FULL

using System.Reflection.Emit;

namespace Au.More
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct ILInstruction
	{
		private readonly OpCode operationCode; // 40.  56-64.  The entire structure is very big.  maybe do array lookup for opcode instead.

		private readonly byte[] instructionRawData;

		private readonly object instructionData;

		private readonly int instructionAddress;

		private readonly int index;

		internal ILInstruction(OpCode code, byte[] instructionRawData, int instructionAddress, object instructionData, int index) {
			this.operationCode = code;
			this.instructionRawData = instructionRawData;
			this.instructionAddress = instructionAddress;
			this.instructionData = instructionData;
			this.index = index;
		}

		public OpCode Op {
			get {
				return this.operationCode;
			}
		}

		/// <summary>
		/// Gets the raw data.
		/// </summary>
		public byte[] RawData {
			get {
				return this.instructionRawData;
			}
		}

		/// <summary>
		/// Gets the data.
		/// </summary>
		public object Data {
			get {
				return this.instructionData;
			}
		}

		/// <summary>
		/// Gets the address of the instruction.
		/// </summary>
		public int Address {
			get {
				return this.instructionAddress;
			}
		}

		/// <summary>
		/// Gets the index of the instruction.
		/// </summary>
		/// <value>
		/// The index of the instruction.
		/// </value>
		public int InstructionIndex {
			get {
				return this.index;
			}
		}

		/// <summary>
		/// Gets the value as integer
		/// </summary>
		/// <value>The data value.</value>
		public int DataValue {
			get {
				int value = 0;
				if (this.Data != null) {
					if (this.Data is byte) {
						value = (byte)this.Data;
					} else if (this.Data is short) {
						value = (short)this.Data;
					} else if (this.Data is int) {
						value = (int)this.Data;
					}
				}

				return value;
			}
		}

		/// <summary>
		/// Gets the length of the instructions and operands.
		/// </summary>
		/// <value>The length.</value>
		public int Length {
			get {
				return this.Op.Size + (this.RawData == null ? 0 : this.RawData.Length);
			}
		}

#if DEBUG
		/// <summary>
		/// Returns a <see cref="System.String"/> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String"/> that represents this instance.
		/// </returns>
		public override string ToString() {
			StringBuilder builder = new StringBuilder();
			builder.AppendFormat("0x{0:x4} {1,-10}", this.Address, this.Op.Name);

			if (this.Data != null) {
				builder.Append(this.Data.ToString());
			}

			if (this.RawData != null && this.RawData.Length > 0) {
				builder.Append(" [0x");
				for (int i = this.RawData.Length - 1; i >= 0; i--) {
					builder.Append(this.RawData[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
				}

				builder.Append(']');
			}

			return builder.ToString();
		}
#endif
	}

	/// <summary>
	/// Reads IL instructions from a byte stream.
	/// </summary>
	/// <remarks>Allows generated code to be viewed without debugger or enabled debug assemblies.</remarks>
	internal sealed class ILReader
	{
		/// <summary>
		/// The _instruction lookup.
		/// </summary>
		private static readonly Lazy<Dictionary<short, OpCode>> instructionLookup = new Lazy<Dictionary<short, OpCode>>(ILReader.GetLookupTable, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

		/// <summary>
		/// The IL reader provider.
		/// </summary>
		private IILReaderProvider intermediateLanguageProvider;

		/// <summary>
		/// Initializes a new instance of the <see cref="ILReader"/> class.
		/// </summary>
		public ILReader(MethodInfo method, byte[] il = null) {
			//au: il added because the caller itself gets IL array and does nothing if it's too big to disassemble. To avoid getting IL 2 times.

			if (method == null) {
				throw new ArgumentNullException("method");
			}

			this.intermediateLanguageProvider = ILReader.CreateILReaderProvider(method, il);
		}

		/// <summary>
		/// Gets the instructions.
		/// </summary>
		/// <value>The instructions.</value>
		public IEnumerable<ILInstruction> Instructions {
			get {
				byte[] instructionBytes = this.intermediateLanguageProvider.GetMethodBody();
				int instructionIndex = 0, startAddress;
				for (int position = 0; position < instructionBytes.Length;) {
					startAddress = position;
					short operationData = instructionBytes[position];
					if (IsInstructionPrefix(operationData)) {
						operationData = (short)((operationData << 8) | instructionBytes[++position]);
					}

					position++;

					OpCode code;
					if (!instructionLookup.Value.TryGetValue(operationData, out code)) {
						throw new InvalidProgramException(string.Format("0x{0:X2} is not a valid op code.", operationData));
					}

					int dataSize = GetSize(code.OperandType);
					byte[] data = new byte[dataSize];
					Buffer.BlockCopy(instructionBytes, position, data, 0, dataSize);
					object objData = this.GetData(code, data);
					position += dataSize;

					if (code.OperandType == OperandType.InlineSwitch) {
						dataSize = (int)objData;
						int[] labels = new int[dataSize];
						for (int index = 0; index < labels.Length; index++) {
							labels[index] = BitConverter.ToInt32(instructionBytes, position);
							position += 4;
						}

						objData = labels;
					}

					yield return new ILInstruction(code, data, startAddress, objData, instructionIndex);
					instructionIndex++;
				}
			}
		}


		private static IILReaderProvider CreateILReaderProvider(MethodInfo methodInfo, byte[] il = null) {
#if FULL
			IILReaderProvider reader = DynamicILReaderProvider.Create(methodInfo, il);
			if(reader != null) {
				return reader;
			}
#endif
			return new ILReaderProvider(methodInfo, il);
		}

		/// <summary>
		/// Checks to see if the IL instruction is a prefix indicating the length of the instruction is two bytes long.
		/// </summary>
		/// <param name="value">The IL instruction as a byte.</param>
		/// <remarks>IL instructions can either be 1 or 2 bytes.</remarks>
		/// <returns>True if this IL instruction is a prefix indicating the instruction is two bytes long.</returns>
		private static bool IsInstructionPrefix(short value) {
			return ((value & OpCodes.Prefix1.Value) == OpCodes.Prefix1.Value) || ((value & OpCodes.Prefix2.Value) == OpCodes.Prefix2.Value)
						|| ((value & OpCodes.Prefix3.Value) == OpCodes.Prefix3.Value) || ((value & OpCodes.Prefix4.Value) == OpCodes.Prefix4.Value)
						|| ((value & OpCodes.Prefix5.Value) == OpCodes.Prefix5.Value) || ((value & OpCodes.Prefix6.Value) == OpCodes.Prefix6.Value)
						|| ((value & OpCodes.Prefix7.Value) == OpCodes.Prefix7.Value) || ((value & OpCodes.Prefixref.Value) == OpCodes.Prefixref.Value);
		}

		/// <summary>
		/// The get lookup table.
		/// </summary>
		/// <returns>
		/// A dictionary of IL instructions.
		/// </returns>
		private static Dictionary<short, OpCode> GetLookupTable() {
			// Might be better to do an array lookup.  Use a seperate arrary for instructions without a prefix and array for each prefix.
			Dictionary<short, OpCode> lookupTable = new Dictionary<short, OpCode>();
			FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
			foreach (FieldInfo field in fields) {
				OpCode code = (OpCode)field.GetValue(null);
				lookupTable.Add(code.Value, code);
			}

			return lookupTable;
		}

		/// <summary>
		/// Gets the size of a operand.
		/// </summary>
		/// <param name="operandType">Defines the type of operand.</param>
		/// <returns>The size in bytes of the operand type.</returns>
		private static int GetSize(OperandType operandType) {
			switch (operandType) {
			case OperandType.InlineNone:
				return 0;
			case OperandType.ShortInlineBrTarget:
			case OperandType.ShortInlineI:
			case OperandType.ShortInlineVar:
				return 1;
			case OperandType.InlineVar:
				return 2;
			case OperandType.InlineBrTarget:
			case OperandType.InlineField:
			case OperandType.InlineI:
			case OperandType.InlineMethod:
			case OperandType.InlineSig:
			case OperandType.InlineString:
			case OperandType.InlineSwitch:
			case OperandType.InlineTok:
			case OperandType.InlineType:
			case OperandType.ShortInlineR:
				return 4;
			case OperandType.InlineI8:
			case OperandType.InlineR:
				return 8;
			default:
				return 0;
			}
		}

		private object GetData(OpCode code, byte[] rawData) {
			object data = null;
			switch (code.OperandType) {
			case OperandType.InlineField:
				data = this.intermediateLanguageProvider.ResolveField(BitConverter.ToInt32(rawData, 0));
				break;
			case OperandType.InlineSwitch:
				data = BitConverter.ToInt32(rawData, 0);

				break;
			case OperandType.InlineBrTarget:
			case OperandType.InlineI:
				data = BitConverter.ToInt32(rawData, 0);
				break;
			case OperandType.InlineI8:
				data = BitConverter.ToInt64(rawData, 0);
				break;
			case OperandType.InlineMethod:
				data = this.intermediateLanguageProvider.ResolveMethod(BitConverter.ToInt32(rawData, 0));
				break;
			case OperandType.InlineR:
				data = BitConverter.ToDouble(rawData, 0);
				break;
			case OperandType.InlineSig:
				data = this.intermediateLanguageProvider.ResolveSignature(BitConverter.ToInt32(rawData, 0));
				break;
			case OperandType.InlineString:
				data = this.intermediateLanguageProvider.ResolveString(BitConverter.ToInt32(rawData, 0));
				break;
			case OperandType.InlineTok:
			case OperandType.InlineType:
				data = this.intermediateLanguageProvider.ResolveType(BitConverter.ToInt32(rawData, 0));
				break;
			case OperandType.InlineVar:
				data = BitConverter.ToInt16(rawData, 0);
				break;
			case OperandType.ShortInlineVar:
			case OperandType.ShortInlineI:
			case OperandType.ShortInlineBrTarget:
				data = rawData[0];
				break;
			case OperandType.ShortInlineR:
				data = BitConverter.ToSingle(rawData, 0);
				break;
			}

			return data;
		}

		interface IILReaderProvider
		{
			byte[] GetMethodBody();

			FieldInfo ResolveField(int metadataToken);
			MemberInfo ResolveMember(int metadataToken);

			MethodBase ResolveMethod(int metadataToken);
			byte[] ResolveSignature(int metadataToken);

			string ResolveString(int metadataToken);
			Type ResolveType(int metadataToken);
		}

		class ILReaderProvider : IILReaderProvider
		{
			byte[] _il;

			public ILReaderProvider(MethodInfo method, byte[] il = null) {
				_il = il;
				this.Method = method;
				this.MethodBody = method.GetMethodBody();
				this.MethodModule = method.Module;
			}

			public MethodInfo Method { get; private set; }

			public MethodBody MethodBody { get; private set; }

			public Module MethodModule { get; private set; }

			public byte[] GetMethodBody() {
				return _il ??= this.MethodBody.GetILAsByteArray();
			}

			public FieldInfo ResolveField(int metadataToken) {
				return this.MethodModule.ResolveField(metadataToken);
			}

			public MemberInfo ResolveMember(int metadataToken) {
				return this.MethodModule.ResolveMember(metadataToken);
			}

			public MethodBase ResolveMethod(int metadataToken) {
				return this.MethodModule.ResolveMethod(metadataToken);
			}

			public byte[] ResolveSignature(int metadataToken) {
				return this.MethodModule.ResolveSignature(metadataToken);
			}

			public string ResolveString(int metadataToken) {
				return this.MethodModule.ResolveString(metadataToken);
			}

			public Type ResolveType(int metadataToken) {
				return this.MethodModule.ResolveType(metadataToken);
			}
		}

#if FULL
		class DynamicILReaderProvider :IILReaderProvider
		{
			public const int TypeRidPrefix = 0x02000000;

			public const int MethodRidPrefix = 0x06000000;

			public const int FieldRidPrefix = 0x04000000;

			public static readonly Type RuntimeDynamicMethodType;

			private static readonly FieldInfo fileLengthField = typeof(ILGenerator).GetField("m_length", BindingFlags.NonPublic | BindingFlags.Instance);

			private static readonly FieldInfo IntermediateLanguageBytesField = typeof(ILGenerator).GetField("m_ILStream", BindingFlags.NonPublic | BindingFlags.Instance);

			private static readonly MethodInfo bakeByteArrayMethod = typeof(ILGenerator).GetMethod("BakeByteArray", BindingFlags.NonPublic | BindingFlags.Instance);

			private static readonly PropertyInfo dynamicScopeIndexor;

			private static readonly FieldInfo dynamicScopeField;

			private static readonly Type genericMethodInfoType;

			private static readonly FieldInfo genericMethodHandleField;

			private static readonly FieldInfo genericMethodContextField;

			private static readonly Type varArgMethodType;

			private static readonly FieldInfo varArgMethodMethod;

			private static readonly Type genericFieldInfoType;

			private static readonly FieldInfo genericFieldInfoHandle;

			private static readonly FieldInfo genericFieldInfoContext;

			private static readonly FieldInfo ownerField;

			private object dynamicScope;

			private ILGenerator generator;

			static DynamicILReaderProvider()
			{
				BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
				dynamicScopeIndexor = Type.GetType("System.Reflection.Emit.DynamicScope").GetProperty("Item", bindingFlags);
				dynamicScopeField = Type.GetType("System.Reflection.Emit.DynamicILGenerator").GetField("m_scope", bindingFlags);

				varArgMethodType = Type.GetType("System.Reflection.Emit.VarArgMethod");
				varArgMethodMethod = varArgMethodType.GetField("m_method", bindingFlags);

				genericMethodInfoType = Type.GetType("System.Reflection.Emit.GenericMethodInfo");
				genericMethodHandleField = genericMethodInfoType.GetField("m_methodHandle", bindingFlags);
				genericMethodContextField = genericMethodInfoType.GetField("m_context", bindingFlags);

				genericFieldInfoType = Type.GetType("System.Reflection.Emit.GenericFieldInfo", false);
				if(genericFieldInfoType != null) {
					genericFieldInfoHandle = genericFieldInfoType.GetField("m_fieldHandle", bindingFlags);
					genericFieldInfoContext = genericFieldInfoType.GetField("m_context", bindingFlags);
				} else {
					genericFieldInfoHandle = genericFieldInfoContext = null;
				}

				RuntimeDynamicMethodType = typeof(DynamicMethod).GetNestedType("RTDynamicMethod", BindingFlags.NonPublic);
				ownerField = RuntimeDynamicMethodType.GetField("m_owner", bindingFlags);
			}

			private DynamicILReaderProvider(DynamicMethod method)
			{
				this.Method = method;
				this.generator = method.GetILGenerator();
				this.dynamicScope = dynamicScopeField.GetValue(this.generator);
			}

			public DynamicMethod Method { get; private set; }

			internal object this[int token]
			{
				get
				{
					return dynamicScopeIndexor.GetValue(this.dynamicScope, new object[] { token });
				}
			}

			public static DynamicILReaderProvider Create(MethodInfo method)
			{
				if(method == null) {
					throw new ArgumentNullException("method");
				}

				DynamicMethod dynamicMethod = method as DynamicMethod;
				if(dynamicMethod != null) {
					return new DynamicILReaderProvider(dynamicMethod);
				}

				Type methodType = method.GetType();
				if(RuntimeDynamicMethodType.IsAssignableFrom(methodType)) {
					return new DynamicILReaderProvider(ownerField.GetValue(method) as DynamicMethod);
				}

				return null;
			}

			public byte[] GetMethodBody()
			{
				byte[] data = null;
				ILGenerator ilgen = this.Method.GetILGenerator();

				try {
					data = (byte[])bakeByteArrayMethod.Invoke(ilgen, null) ?? new byte[0];
				}
				catch(TargetInvocationException) {
					int length = (int)fileLengthField.GetValue(ilgen);
					data = new byte[length];
					Array.Copy((byte[])IntermediateLanguageBytesField.GetValue(ilgen), data, length);
				}

				return data;
			}

			public FieldInfo ResolveField(int metadataToken)
			{
				object tokenValue = this[metadataToken];
				if(tokenValue is RuntimeFieldHandle) {
					return FieldInfo.GetFieldFromHandle((RuntimeFieldHandle)tokenValue);
				}

				if(tokenValue.GetType() == DynamicILReaderProvider.genericFieldInfoType) {
					return FieldInfo.GetFieldFromHandle(
						(RuntimeFieldHandle)genericFieldInfoHandle.GetValue(tokenValue),
						(RuntimeTypeHandle)genericFieldInfoContext.GetValue(tokenValue));
				}

				return null;
			}

			public MemberInfo ResolveMember(int metadataToken)
			{
				if((metadataToken & TypeRidPrefix) != 0) {
					return this.ResolveType(metadataToken);
				}

				if((metadataToken & MethodRidPrefix) != 0) {
					return this.ResolveMethod(metadataToken);
				}

				if((metadataToken & FieldRidPrefix) != 0) {
					return this.ResolveField(metadataToken);
				}

				return null;
			}

			public MethodBase ResolveMethod(int metadataToken)
			{
				object tokenValue = this[metadataToken];
				DynamicMethod dynamicMethod = tokenValue as DynamicMethod;
				if(dynamicMethod != null) {
					return dynamicMethod;
				}

				if(tokenValue is RuntimeMethodHandle) {
					return MethodBase.GetMethodFromHandle((RuntimeMethodHandle)this[metadataToken]);
				}

				if(tokenValue.GetType() == DynamicILReaderProvider.genericFieldInfoType) {
					return MethodBase.GetMethodFromHandle(
						(RuntimeMethodHandle)genericMethodHandleField.GetValue(tokenValue),
						(RuntimeTypeHandle)genericMethodContextField.GetValue(tokenValue));
				}

				if(tokenValue.GetType() == DynamicILReaderProvider.varArgMethodType) {
					return DynamicILReaderProvider.varArgMethodMethod.GetValue(tokenValue) as MethodInfo;
				}

				return null;
			}

			public byte[] ResolveSignature(int metadataToken)
			{
				return this[metadataToken] as byte[];
			}
			public string ResolveString(int metadataToken)
			{
				return this[metadataToken] as string;
			}

			public Type ResolveType(int metadataToken)
			{
				return Type.GetTypeFromHandle((RuntimeTypeHandle)this[metadataToken]);
			}
		}
#endif
	}
}
