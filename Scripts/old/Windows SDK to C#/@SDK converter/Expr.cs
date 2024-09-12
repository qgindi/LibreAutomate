namespace SdkConverter;

unsafe partial class Converter {
	_ExpressionResult _Expression(int iFrom, int iTo, string debugConstName = null) {
		Debug.Assert(iTo > iFrom);
		
		int nTok = iTo - iFrom, iLast = iTo - 1;
		
		string sValue, sType; bool isHex = false;
		
		//if(debugConstName == "[]") return new _ExpressionResult(_TokToString(iFrom, iTo), null, true);
		//print.it($"{debugConstName} {_TokToString(iFrom, iTo)}");
		
		//Unenclose whole expression (not parts).
		
		while (nTok >= 3 && _TokIsChar(iFrom, '(') && _TokIsChar(iLast, ')')) {
			if (nTok == 3 || _SkipEnclosed(iFrom) == iLast) { iFrom++; iLast--; iTo--; nTok -= 2; } else break;
		}
		
		//Simple cases.
		
		if (nTok == 1) {
			ulong value; _OP type;
			
			if (_TokIsNumber(iFrom)) {
				if (__ExprNumber(iFrom, out value, out type, ref isHex)) {
					type = __ExprMakeUintIfHex(type, isHex);
					sValue = __ExprValueToString(value, type);
					sType = __ExprTypeToString(type);
					return new _ExpressionResult(sValue, sType, false, value);
				} else {
					sValue = _TokToString(iFrom);
					sType = sValue.Ends("f", true) ? "float" : "double";
					return new _ExpressionResult(sValue, sType, false);
				}
				
			}
			
			if (_TokIsChar(iFrom, '"')) {
				return new _ExpressionResult(_TokToString(iFrom), "string", false);
			}
			if (_TokIsChar(iFrom, '\'')) {
				//is like 'ABCD'?
				int len = _tok[iFrom].len - 2;
				if (len > 1 && len <= 4) {
					char* s = T(iFrom) + 1;
					if (*s != '\\') {
						//print.it(_DebugGetLine(iFrom));
						uint k = 0; for (int j = 0; j < len; j++) k = (k << 8) | ((uint)s[j] & 0xff);
						return new _ExpressionResult($"0x{k:X}", "uint", false, k);
					}
				}
				//string k=_TokToString(iFrom).Re
				return new _ExpressionResult(_TokToString(iFrom), "char", false);
			}
			
			if (_TokIsIdent(iFrom)) {
				if (_EnumFindValue(iFrom, out value, out type)) {
					sValue = __ExprValueToString(value, type);
					sType = __ExprTypeToString(type);
					return new _ExpressionResult(sValue, sType, false, value);
				}
				
				//print.it($"<><c 0xff0000>{_TokToString(iFrom, iTo)}</c>");
			} else {
				//print.it($"<><c 0xff0000>{_TokToString(iFrom, iTo)}</c>"); //0 in SDK
			}
			
			return new _ExpressionResult(_TokToString(iFrom), null, true);
		}
		
		//Calculate expression.
		
		//print.it(debugConstName, _TokToString(iFrom, iTo));
		
		//Part 1 - convert the expression from normal infix form to RPN form which is easy to calculate.
		//Use the shunting-yard algorithm: https://en.wikipedia.org/wiki/Shunting-yard_algorithm
		//Put operands and operators in _eRPN, which is the "output queue" of the algorithm.
		
		int nRPN = 0; //number of elements in the output queue
		int nOp = 0; //number of elements in the operator stack
		int i;
		bool wasOperand = false;
		
		////Debug
		//if(debugConstName == "WTS_CURRENT_SERVER") {
		//	print.it(debugConstName);
		//	int stop = 0;
		//}
		
		for (i = iFrom; i < iTo; i++) {
			int iTok = i;
			_OP op;
			char c = *T(i);
			
			if (_IsCharIdent(c) || c == '\'') { //digit, identifier or 'character'
				if (wasOperand) goto gFail; //cannot be 2 operands
				wasOperand = true;
				
				//If the token is a number, then add it to the output queue.
				
				ulong value; _OP type = _OP.OperandInt;
				
				if (_IsCharDigit(c)) { //number
					if (!__ExprNumber(i, out value, out type, ref isHex)) {
						//_Err(i, "bad number"); //0 in SDK
						goto gFail;
					}
				} else if (c == '\'') {
					char* s = T(i) + 1;
					int len = _tok[i].len - 2;
					if (len > 1) {
						if (len == 2 && *s == '\\') s++;
						else goto gFail;
					}
					value = *s;
				} else { //identifier
					if (_TokIs(i, "sizeof")) {
						if (!_TokIsChar(++i, '(')) goto gFail;
						int rightParen = __ExprIsType(++i);
						if (rightParen != i + 1) goto gFail; //if not like a type, or with *
						if (!__ExprSizeof(i, out value)) goto gFail;
						i++;
					} else {
						if (!_EnumFindValue(i, out value, out type)) goto gFail;
					}
				}
				
				_eRPN[nRPN++] = new _EVAL(i, type, value);
				
			} else if (_IsCharOperator(c) || c == '(') { //operator or cast or (
				if (c == '(') {
					//is cast?
					int rightParen = __ExprIsType(i + 1);
					bool isCast = rightParen > 0;
					if (isCast && rightParen - i == 2 && _enumValues.ContainsKey(_tok[i + 1])) isCast = false; //is '(enumValue)'?
					if (isCast) { //cast
						iTok = i + 1;
						i = rightParen;
						op = _OP.Cast;
						
						//cast is an operator
					} else {
						iTok = i;
						op = _OP.LeftParen;
					}
				} else {
					char c2 = *T(i + 1);
					op = 0;
					
					if (wasOperand) //this must be a binary operator
					{
						//note: cannot be +=, ++ etc because they can be used only with variables, not with constants.
						
						wasOperand = false;
						switch (c) {
						case '+': op = _OP.Plus; break;
						case '-': op = _OP.Minus; break;
						case '*': op = _OP.Mul; break;
						case '/': op = _OP.Div; break;
						case '%': op = _OP.Mod; break;
						case '&':
							if (c2 == '&') { i++; op = _OP.LogAnd; } else op = _OP.BitAnd;
							break;
						case '|':
							if (c2 == '|') { i++; op = _OP.LogOr; } else op = _OP.BitOr;
							break;
						case '^': op = _OP.BitXor; break;
						case '<':
							if (c2 == '<') { i++; op = _OP.BitLshift; } else if (c2 == '=') { i++; op = _OP.LtEq; } else op = _OP.Lt;
							break;
						case '>':
							if (c2 == '>') { i++; op = _OP.BitRshift; } else if (c2 == '=') { i++; op = _OP.GtEq; } else op = _OP.Gt;
							break;
						case '=':
							if (c2 == '=') { i++; op = _OP.CompareEq; } else goto gFail;
							break;
						case '!':
							if (c2 == '=') { i++; op = _OP.CompareNotEq; } else goto gFail;
							break;
						//case '?':
						//	op = _OPERATOR.Ternary;
						//	break;
						default:
							goto gFail;
						}
					} else { //this must be a prefix operator
						
						//note: cannot be ++, & etc because they can be used only with variables, not with constants.
						
						switch (c) {
						case '+': continue;
						case '-': op = _OP.UnaryMinus; break;
						case '!': op = _OP.UnaryLogNot; break;
						case '~': op = _OP.UnaryBitNot; break;
						default: goto gFail;
						}
					}
				}
				
				//If the token is an operator (op), then:
				if (op != _OP.LeftParen) {
					//While there is an operator (op2) at the top of the stack, and either
					//	op is left-associative and its precedence is less than or equal to that of op2, or
					//	op is right associative, and has precedence less than that of o2,
					//		pop op2 off the stack, onto the output queue.
					for (; nOp > 0; nOp--) {
						int n = nOp - 1;
						_OP op2 = _eOp[n].op;
						if (op2 == _OP.LeftParen) break; //is an operator at the top of the stack?
						bool pop = false, opRTL = (op & _OP._FlagRightToLeft) != 0;
						int prec1 = (int)(op & _OP._MaskPrecedence), prec2 = (int)(op2 & _OP._MaskPrecedence);
						//print.it("  ", op, prec1, opRTL, op2, prec2);
						
						if (!opRTL) pop = prec1 >= prec2; //op is left-associative and its precedence is less than or equal to that of op2
						else pop = prec1 > prec2; //op is right associative, and has precedence less than that of o2
						
						if (!pop) break;
						_eRPN[nRPN++] = _eOp[n];
					}
				} //Else if the token is a left parenthesis (i.e. "("), then push it onto the stack.
				
				//push op to the stack
				_eOp[nOp++] = new _EVAL(iTok, op);
				
			} else if (c == ')') {
				//If the token is a right parenthesis (i.e. ")"):
				//	Until the token at the top of the stack is a left parenthesis, pop operators off the stack onto the output queue.
				for (; nOp > 0; nOp--) {
					int n = nOp - 1;
					if (_eOp[n].op == _OP.LeftParen) break;
					_eRPN[nRPN++] = _eOp[n];
				}
				//If the stack runs out without finding a left parenthesis, then there are mismatched parentheses.
				if (nOp == 0) goto gFail;
				//Pop the left parenthesis from the stack, but not onto the output queue.
				nOp--;
			} else {
				//"string" //0 in SDK
				goto gFail;
			}
		}
		
		//When there are no more tokens to read:
		//	While there are still operator tokens in the stack:
		//		If the operator token on the top of the stack is a parenthesis: error, there are mismatched parentheses.
		//		Else pop the operator onto the output queue.
		for (; nOp > 0; nOp--) {
			int n = nOp - 1;
			if (_eOp[n].op == _OP.LeftParen) goto gFail;
			_eRPN[nRPN++] = _eOp[n];
		}
		
		////Debug
		//if(debugConstName == "WTS_CURRENT_SERVER") {
		//	var deb = new StringBuilder(debugConstName);
		//	deb.Append(" = ");
		//	deb.Append(_TokToString(iFrom, iTo));
		//	deb.Append("  -> ");
		//	for(i = 0; i < nRPN; i++) {
		//		deb.Append(" ");
		//		if(_eRPN[i].op < _OP._FirstOperator) deb.Append(_eRPN[i].value);
		//		else deb.Append(_eRPN[i].op);
		//	}
		//	print.it(deb);
		
		//	int stop = 0;
		//}
		
		//Part 2 - calculate the RPN expression.
		//Use the postfix algorithm: https://en.wikipedia.org/wiki/Reverse_Polish_notation
		
		int nCalc = 0; //_eCalc stack length
		for (i = 0; i < nRPN; i++) {
			if (_eRPN[i].op < _OP._FirstOperator) { //operand
				
				//If the token is a value (operand): push it onto the stack.
				_eCalc[nCalc++] = _eRPN[i];
			} else { //operator
				
				//It is already known that the operator takes n arguments.
				int nOperands = (int)(_eRPN[i].op & _OP._MaskNOperands) >> 8;
				//If there are fewer than n values on the stack: error, the user has not input sufficient values in the expression.
				if (nCalc < nOperands) goto gFail;
				
				if (nOperands == 1) {
					
					//Pop the top n values from the stack.
					//A = _eCalc[--nCalc]; //operand (we change it in-place without pop/push)
					
					//Evaluate the operator, with the values as arguments.
					_OP op = _eRPN[i].op;
					if (op == _OP.Cast) {
						if (!__ExprCalcCast(_eRPN[i].iTok, ref _eCalc[nCalc - 1])) goto gFail;
					} else {
						__ExprCalcOperatorUnary(op, ref _eCalc[nCalc - 1]);
					}
					
				} else { //2 operands
					
					//Pop the top n values from the stack.
					_EVAL B = _eCalc[--nCalc]; //rigt operand
					//A = _eCalc[--nCalc]; //left operand (we change it in-place without pop/push)
					
					//Evaluate the operator, with the values as arguments.
					__ExprCalcOperatorBinary(_eRPN[i].op, ref _eCalc[nCalc - 1], B);
					
					//__ExprDebugOutValue(_eCalc[nCalc - 1]);
				}
				
				//Push the returned results, if any, back onto the stack.
				//_eCalc[nCalc++] = A; //we change it in-place without pop/push
			}
		}
		
		//If there is only one value in the stack, it is the result of the calculation.
		//Otherwise, there are more values in the stack: error, the user input has too many values.
		if (nCalc != 1) goto gFail;
		_OP rType = __ExprMakeUintIfHex(_eCalc[0].op, isHex);
		sValue = __ExprValueToString(_eCalc[0].value, rType);
		sType = __ExprTypeToString(rType);
		
		return new _ExpressionResult(sValue, sType, false, _eCalc[0].value);
		
		gFail:
		//print.it($"<><c 0xff>{debugConstName}    {_TokToString(iFrom, iTo)}</c>");
		//if(++_debugNFailed > 10) _Err(iFrom, "debug");
		return new _ExpressionResult(_TokToString(iFrom, iTo), null, true);
		
		//never mind: need functions:
		//LongToHandle
		//HRESULT_FROM_WIN32
		
		//never mind: sizeof pointers, IntPtr, nint and wnd.
	}
	
	void __ExprDebugOutValue(_EVAL v) {
		print.it($"    {v.op}, { __ExprValueToString(v.value, v.op)}");
	}
	
	//int _debugNFailed;
	
	#region CALC_UNARY
	
	void __ExprCalcOperatorUnary(_OP op, ref _EVAL A) {
		switch (A.op) {
		case _OP.OperandInt: A.value = (ulong)__ExprCalcOperatorUnary(op, (int)A.value); break;
		case _OP.OperandUint: A.value = __ExprCalcOperatorUnary(op, (uint)A.value); break;
		case _OP.OperandLong: A.value = (ulong)__ExprCalcOperatorUnary(op, (long)A.value); break;
		case _OP.OperandUlong: A.value = __ExprCalcOperatorUnary(op, A.value); break;
		}
	}
	
	int __ExprCalcOperatorUnary(_OP op, int A) {
		switch (op) {
		case _OP.UnaryMinus: return -A;
		case _OP.UnaryLogNot: return A == 0 ? 0 : 1;
		case _OP.UnaryBitNot: return ~A;
		}
		Debug.Assert(false);
		return 0;
	}
	
	uint __ExprCalcOperatorUnary(_OP op, uint A) {
		switch (op) {
		case _OP.UnaryMinus: return (uint)-(int)A;
		case _OP.UnaryLogNot: return A == 0U ? 0U : 1U;
		case _OP.UnaryBitNot: return ~A;
		}
		Debug.Assert(false);
		return 0;
	}
	
	long __ExprCalcOperatorUnary(_OP op, long A) {
		switch (op) {
		case _OP.UnaryMinus: return -A;
		case _OP.UnaryLogNot: return A == 0 ? 0 : 1;
		case _OP.UnaryBitNot: return ~A;
		}
		Debug.Assert(false);
		return 0;
	}
	
	ulong __ExprCalcOperatorUnary(_OP op, ulong A) {
		switch (op) {
		case _OP.UnaryMinus: return (ulong)-(long)A;
		case _OP.UnaryLogNot: return A == 0U ? 0U : 1U;
		case _OP.UnaryBitNot: return ~A;
		}
		Debug.Assert(false);
		return 0;
	}
	
	#endregion
	
	#region CALC_BINARY
	
	void __ExprCalcOperatorBinary(_OP op, ref _EVAL A, _EVAL B) {
		if (A.op < B.op) A.op = B.op;
		switch (A.op) {
		case _OP.OperandInt:
			A.value = (ulong)__ExprCalcOperatorBinary(op, (int)A.value, (int)B.value);
			break;
		case _OP.OperandUint:
			A.value = __ExprCalcOperatorBinary(op, (uint)A.value, (uint)B.value);
			break;
		case _OP.OperandLong:
			A.value = (ulong)__ExprCalcOperatorBinary(op, (long)A.value, (long)B.value);
			break;
		case _OP.OperandUlong:
			A.value = __ExprCalcOperatorBinary(op, A.value, B.value);
			break;
		}
	}
	
	int __ExprCalcOperatorBinary(_OP op, int A, int B) {
		switch (op) {
		case _OP.Plus: return A + B;
		case _OP.Minus: return A - B;
		case _OP.Mul: return A * B;
		case _OP.Div: return A / B;
		case _OP.Mod: return A % B;
		case _OP.BitLshift: return A << B;
		case _OP.BitRshift: return A >> B;
		case _OP.Lt: return A < B ? 1 : 0;
		case _OP.LtEq: return A <= B ? 1 : 0;
		case _OP.Gt: return A > B ? 1 : 0;
		case _OP.GtEq: return A >= B ? 1 : 0;
		case _OP.CompareEq: return A == B ? 1 : 0;
		case _OP.CompareNotEq: return A != B ? 1 : 0;
		case _OP.BitAnd: return A & B;
		case _OP.BitOr: return A | B;
		case _OP.BitXor: return A ^ B;
		case _OP.LogAnd: return A != 0 && B != 0 ? 1 : 0;
		case _OP.LogOr: return A != 0 || B != 0 ? 1 : 0;
		}
		Debug.Assert(false);
		return 0;
	}
	
	uint __ExprCalcOperatorBinary(_OP op, uint A, uint B) {
		switch (op) {
		case _OP.Plus: return A + B;
		case _OP.Minus: return A - B;
		case _OP.Mul: return A * B;
		case _OP.Div: return A / B;
		case _OP.Mod: return A % B;
		case _OP.BitLshift: return A << (int)B;
		case _OP.BitRshift: return A >> (int)B;
		case _OP.Lt: return A < B ? 1U : 0U;
		case _OP.LtEq: return A <= B ? 1U : 0U;
		case _OP.Gt: return A > B ? 1U : 0U;
		case _OP.GtEq: return A >= B ? 1U : 0U;
		case _OP.CompareEq: return A == B ? 1U : 0U;
		case _OP.CompareNotEq: return A != B ? 1U : 0U;
		case _OP.BitAnd: return A & B;
		case _OP.BitOr: return A | B;
		case _OP.BitXor: return A ^ B;
		case _OP.LogAnd: return A != 0U && B != 0U ? 1U : 0U;
		case _OP.LogOr: return A != 0U || B != 0U ? 1U : 0U;
		}
		Debug.Assert(false);
		return 0;
	}
	
	long __ExprCalcOperatorBinary(_OP op, long A, long B) {
		switch (op) {
		case _OP.Plus: return A + B;
		case _OP.Minus: return A - B;
		case _OP.Mul: return A * B;
		case _OP.Div: return A / B;
		case _OP.Mod: return A % B;
		case _OP.BitLshift: return A << (int)B;
		case _OP.BitRshift: return A >> (int)B;
		case _OP.Lt: return A < B ? 1 : 0;
		case _OP.LtEq: return A <= B ? 1 : 0;
		case _OP.Gt: return A > B ? 1 : 0;
		case _OP.GtEq: return A >= B ? 1 : 0;
		case _OP.CompareEq: return A == B ? 1 : 0;
		case _OP.CompareNotEq: return A != B ? 1 : 0;
		case _OP.BitAnd: return A & B;
		case _OP.BitOr: return A | B;
		case _OP.BitXor: return A ^ B;
		case _OP.LogAnd: return A != 0 && B != 0 ? 1 : 0;
		case _OP.LogOr: return A != 0 || B != 0 ? 1 : 0;
		}
		Debug.Assert(false);
		return 0;
	}
	
	ulong __ExprCalcOperatorBinary(_OP op, ulong A, ulong B) {
		switch (op) {
		case _OP.Plus: return A + B;
		case _OP.Minus: return A - B;
		case _OP.Mul: return A * B;
		case _OP.Div: return A / B;
		case _OP.Mod: return A % B;
		case _OP.BitLshift: return A << (int)B;
		case _OP.BitRshift: return A >> (int)B;
		case _OP.Lt: return A < B ? 1U : 0U;
		case _OP.LtEq: return A <= B ? 1U : 0U;
		case _OP.Gt: return A > B ? 1U : 0U;
		case _OP.GtEq: return A >= B ? 1U : 0U;
		case _OP.CompareEq: return A == B ? 1U : 0U;
		case _OP.CompareNotEq: return A != B ? 1U : 0U;
		case _OP.BitAnd: return A & B;
		case _OP.BitOr: return A | B;
		case _OP.BitXor: return A ^ B;
		case _OP.LogAnd: return A != 0U && B != 0U ? 1U : 0U;
		case _OP.LogOr: return A != 0U || B != 0U ? 1U : 0U;
		}
		Debug.Assert(false);
		return 0;
	}
	
	#endregion
	
	bool __ExprCalcCast(int i, ref _EVAL A) {
		//print.it(_tok[i]);
		
		_Symbol x;
		if (!_TryFindSymbol(i, out x, false)) {
			//print.it(_tok[i]); //0 in SDK
			//_Err(i, "unknown");
			return false;
		}
		if (0 != _Unalias(i, ref x) || _TokIsChar(i + 1, '*')) {
			//support expressions like '#define RT_BITMAP (LPWSTR)((ULONG_PTR)((WORD)(2)))'
			if (A.op <= _OP.OperandUint) {
				A.op = _OP.OperandInt;
				return true;
			}
			//_Err(i, "stop"); //0 in SDK
			return false;
		}
		var ct = x as _CppType;
		if (ct == null) {
			//support LPARAM, HWND
			if (A.op > _OP.OperandUint) return false; //0 in SDK
			if (x == _sym_LPARAM || x == _sym_wnd) return true;
			//support enum, callback
			if (x is _Enum || x is _Callback) return true;
			//_Err(i, "stop 2"); //0 in SDK
			return false;
		}
		
		switch (ct.sizeBytesCpp) {
		case 8:
			if (ct.csTypename[0] == 'd') return false; //double
			if (ct.csTypename[0] == 'I') { //IntPtr
				//print.it(_DebugGetLine(i));
				if (A.op > _OP.OperandUint) return false; //0 in SDK
				break; //let be the same type as in 32-bit mode (no long). All in SDK are like #define HKEY_CURRENT_USER (IntPtr)0x80000001.
			}
			A.op = ct.isUnsigned ? _OP.OperandUlong : _OP.OperandLong;
			return true;
		case 4:
			if (ct.csTypename[0] == 'f') return false; //float
			A.value = (uint)A.value;
			break;
		case 2:
			A.value = (ushort)A.value;
			break;
		case 1:
			if (ct.csTypename == "bool") A.value = A.value == 0U ? 0U : 1U;
			else A.value = (byte)A.value;
			break;
		default: return false;
		}
		
		A.op = ct.isUnsigned ? _OP.OperandUint : _OP.OperandInt;
		return true;
	}
	
	/// <summary>
	/// If token i is an identifier and is followed by 0 or more '*' characters and character ')', returns token index of the ')'.
	/// Else returns 0.
	/// </summary>
	int __ExprIsType(int i) {
		if (!_TokIsIdent(i)) return 0;
		i++;
		while (_TokIsChar(i, '*')) i++;
		if (!_TokIsChar(i, ')')) return 0;
		return i;
	}
	
	bool __ExprNumber(int i, out ulong value, out _OP type, ref bool isHex) {
		value = 0; type = _OP.OperandInt;
		
		char* s = T(i), se;
		if (*s == '0') {
			if (s[1] == 'x' || s[1] == 'X') { value = strtoui64(s, &se); isHex = true; } //hex
			else if (s[1] == 'b' || s[1] == 'B') { value = strtoui64(s + 2, &se, 2); isHex = true; } //binary constant like 0b10101010
			else value = strtoui64(s, &se, 8); //oct
		} else value = strtoui64(s, &se);
		
		bool isUnsigned = false, isLong = false;
		
		int tlen = _tok[i].len;
		if (se < s + tlen) {
			if (*se == 'U' || *se == 'u') { se++; isUnsigned = true; }
			if (se < s + tlen) {
				if (*se == 'L' || *se == 'l') { se++; isLong = true; }
				if (se < s + tlen) return false; //eg a floating-point number
			}
		}
		
		if (!isLong && value > uint.MaxValue) isLong = true;
		if (!isUnsigned) {
			if (isLong) isUnsigned = value > long.MaxValue;
			else isUnsigned = value > int.MaxValue;
		}
		
		if (isLong) type = isUnsigned ? _OP.OperandUlong : _OP.OperandLong; else if (isUnsigned) type = _OP.OperandUint;
		return true;
	}
	
	[DllImport("msvcrt.dll", EntryPoint = "_wcstoui64", CallingConvention = CallingConvention.Cdecl)]
	internal static extern ulong strtoui64(char* s, char** endPtr = null, int radix = 0);
	
	string __ExprTypeToString(_OP type) {
		Debug.Assert(type <= _OP.OperandUlong);
		switch (type) {
		case _OP.OperandInt: return "int";
		case _OP.OperandUint: return "uint";
		case _OP.OperandLong: return "long";
		default: return "ulong";
		}
	}
	
	string __ExprValueToString(ulong value, _OP type) {
		Debug.Assert(type <= _OP.OperandUlong);
		switch (type) {
		case _OP.OperandInt: return ((int)value).ToString();
		case _OP.OperandUint: return "0x" + ((uint)value).ToString("X");
		case _OP.OperandLong: return ((long)value).ToString();
		default: return "0x" + value.ToString("X");
		}
	}
	
	_OP __ExprMakeUintIfHex(_OP type, bool isHex) {
		Debug.Assert(type <= _OP.OperandUlong);
		if (isHex) {
			if (type == _OP.OperandInt) return _OP.OperandUint;
			if (type == _OP.OperandLong) return _OP.OperandUlong;
		}
		return type;
	}
	
	bool __ExprSizeof(int i, out ulong value) {
		value = 0;
		_Symbol x;
		if (!_TryFindSymbol(i, out x, true) || x is _Keyword) return false;
		if (0 != _Unalias(i, ref x)) return false;
		
		var ct = x as _CppType;
		if (ct != null) {
			if (ct.csTypename[0] == 'I') return false; //IntPtr (variable size)
			value = ct.sizeBytesCpp;
			return true;
		}
		
		var t = x as _Struct; if (t == null) return false;
		switch (_TokToString(i)) {
		case "SID": value = 12; break;
		case "IMAGE_SYMBOL_EX": value = 20; break; //in SDK used as array size
		default: return false; //most have pointer members (variable size)
		}
		return true;
	}
	
	/// <summary>
	/// C operator constants, sorted by precedence.
	/// Flags: 0x100 - 1 operand, 0x200 - 2 operands, 0x1000 - right-to-left.
	/// </summary>
	[Flags]
	enum _OP //:ushort
	{
		OperandInt, OperandUint, OperandLong, OperandUlong,
		
		//OperandDouble, OperandFloat, OperandBool, OperandString, //not supported
		
		_FirstOperator = 0x10,
		
		//FuncCall = 0x110, //f()
		//SuffixInc, SuffixDec, ArrElem, Member, MemberPtr, //x++, x--, a[], x.m, x->m  //only with variables
		
		UnaryMinus = 0x1120, UnaryLogNot, UnaryBitNot, Cast, //-x, !x, ~x, (t)x  //RTL
		
		//PrefixInc, PrefixDec, Deref, Address, //++x, --x, *x, &x  //RTL  //only with variables
		
		Mul = 0x230, Div, Mod, //x*y, x/y, x%y
		Plus = 0x240, Minus, //x+y, x-y
		BitLshift = 0x250, BitRshift, //x<<n, x>>n
		Lt = 0x260, LtEq, Gt, GtEq, //x<y, x<=y, x>y, x>=y
		CompareEq = 0x270, CompareNotEq, //==, !=
		BitAnd = 0x280, //x&y
		BitXor = 0x290, //x^y
		BitOr = 0x2A0, //x|y
		LogAnd = 0x2B0, //x&&y
		LogOr = 0x2C0, //x||y
		
		//Ternary = 0x13D0, //x?y:z  //RTL  //0 or few in SDK
		//Assign = 0x12E0, PlusAssign, MinusAssign, MulAssign, DivAssign, ModAssign, BitLshiftAssign, BitRshiftAssign, BitAndAssign, BitXorAssign, BitOrAssign, //=, +=, ...  //RTL  //only with variables
		
		LeftParen = 0xF0,
		
		_MaskPrecedence = 0xF0,
		_MaskNOperands = 0x300,
		_FlagRightToLeft = 0x1000,
	}
	
	struct _EVAL {
		public int iTok; //token
		public _OP op;
		public ulong value;
		
		public _EVAL(int iTok, _OP op) {
			this.iTok = iTok;
			this.op = op;
			value = 0;
		}
		
		public _EVAL(int iTok, _OP op, ulong value) {
			this.iTok = iTok;
			this.op = op;
			this.value = value;
		}
	};
	
	_EVAL[] _eRPN = new _EVAL[200]; //RPN output queue
	_EVAL[] _eOp = new _EVAL[200]; //operator stack used when building the RPN
	_EVAL[] _eCalc = new _EVAL[100]; //final stack used to calculate the RPN
	
	class _ExpressionResult {
		public string valueS;
		public string typeS;
		public bool notConst;
		public uint valueI;
		
		public _ExpressionResult(string valueS, string typeS, bool notConst, ulong valueI = 0) {
			this.valueS = valueS;
			this.typeS = typeS;
			this.notConst = notConst;
			this.valueI = (uint)valueI;
		}
	}
}
