using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GreatClock.Common.Reflections {

	/// <summary>
	/// String Content Enrichment Tool.
	/// <para>For unsertain contents in a string, such as player's nick name or player's current level, a replacement can be used and resolved at runtime to retrieve the final content.</para>
	/// <para>A function call just as "[@func(arg1,arg2)]" and a variable getter just as "[@$varname]" are available replacements in your content string.</para>
	/// <para>Variables in a function call can also be a variable, which should start with "$". Maths statements and numeric variables, whick "$" is NOT applicable, are also available.</para>
	/// <para>For example : [@$PlayerName] defeated [@GetNpcName($npcid)], and passed [@GetGateName($gateid)]!</para>
	/// <para>Available numeric types : int, uint, long, ulong, float, double.</para>
	/// <para>The last parameter in predefined enrich function can ba an array, such as funcname(params type[] args).</para>
	/// </summary>
	public sealed class StringEnricher {

		/// <summary>
		/// Delegate defination for receiving warnings.
		/// </summary>
		/// <param name="warning">warning content</param>
		public delegate void WarningDelegate(string warning);

		/// <summary>
		/// Delegate defination for retrieving string variable value.
		/// </summary>
		/// <param name="varName">variable name</param>
		/// <returns>string value of the variable</returns>
		public delegate string StringGetterDelegate(string varName);

		/// <summary>
		/// Delegate defination for calculating maths statement
		/// </summary>
		/// <param name="statement">maths statement in function call parameters</param>
		/// <param name="varGetter">variable getter delegate to retrieve numeric values in registered types or fields</param>
		/// <returns></returns>
		public delegate double MathsCalculaterDelegate(string statement, NumericGetterDelegate varGetter);

		/// <summary>
		/// Delegate defination for retrieving numeric values when calculating maths statements.
		/// </summary>
		/// <param name="varName">numeric variable name to retrieve value</param>
		/// <returns>numeric variable value</returns>
		public delegate double NumericGetterDelegate(string varName);

		// The following delegates are used for 'Delegate' converting in registering via RegisterFunction. 
		// For example : "string GetColoredText(string text, int colorLevel)" is a function that you have defined in your C# code.
		//     Register this function to a StringEnricher instance named "se" :
		//     se.RegisterFunction((StringEnricher.Function<string, int>)GetColoredText);
		public delegate string Function();
		public delegate string Function<in T>(T arg);
		public delegate string Function<in T1, in T2>(T1 arg1, T2 arg2);
		public delegate string Function<in T1, in T2, in T3>(T1 arg1, T2 arg2, T3 arg3);
		public delegate string Function<in T1, in T2, in T3, in T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
		public delegate string Function<in T1, in T2, in T3, in T4, in T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
		public delegate string Function<in T1, in T2, in T3, in T4, in T5, in T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
		public delegate string Function<in T1, in T2, in T3, in T4, in T5, in T6, in T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
		public delegate string Function<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
		public delegate string Function<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
		public delegate string Function<in T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, in T9, in T10>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);

		/// <summary>
		/// Delegate to receive warnings.
		/// </summary>
		public event WarningDelegate onWarning = null;

		public StringEnricher(MathsCalculaterDelegate mathsCalculater) {
			mNumericGetter = GetMathsVariable;
			mMathsCalculater = mathsCalculater;
		}

		/// <summary>
		/// Register enrich functions, string variables and numeric variables defined as STATIC methods, fields and properties.
		/// </summary>
		/// <param name="type">The type that contains enrich functions and variables as STATICs</param>
		public void Register(Type type) {
			if (type == null) {
				throw new Exception("Invalid parameters !");
			}
			BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			Type typeIgnore = typeof(StringEnricherIgnoreAttribute);
			MethodInfo[] methods = type.GetMethods(flags);
			for (int i = 0, imax = methods.Length; i < imax; i++) {
				MethodInfo method = methods[i];
				if (Attribute.IsDefined(method, typeIgnore)) { continue; }
				RegFunc(null, null, method);
			}
			PropertyInfo[] properties = type.GetProperties(flags);
			for (int i = 0, imax = properties.Length; i < imax; i++) {
				PropertyInfo property = properties[i];
				if (Attribute.IsDefined(property, typeIgnore)) { continue; }
				RegVar(null, null, property);
			}
			FieldInfo[] fields = type.GetFields(flags);
			for (int i = 0, imax = fields.Length; i < imax; i++) {
				FieldInfo field = fields[i];
				if (Attribute.IsDefined(field, typeIgnore)) { continue; }
				RegVar(null, null, field);
			}
		}

		/// <summary>
		/// Register enrich functions, string variables and numeric variables defined as MEMBER methods, fields and properties
		/// </summary>
		/// <typeparam name="T">The type that contains enrich functions and variables as MEMBERs</typeparam>
		/// <param name="obj">The instance that contains enrich functions and variables</param>
		/// <param name="baseType">The type and its base types of which any definations in it will be ignored</param>
		public void Register<T>(T obj, Type baseType) where T : class {
			if (obj == null) {
				throw new Exception("Invalid parameters !");
			}
			if (baseType == null) {
				baseType = typeof(object);
			}
			BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			Type typeIgnore = typeof(StringEnricherIgnoreAttribute);
			Type type = typeof(T);
			MethodInfo[] methods = type.GetMethods(flags);
			for (int i = 0, imax = methods.Length; i < imax; i++) {
				MethodInfo method = methods[i];
				Type declaringType = method.DeclaringType;
				if (baseType.Equals(declaringType) || baseType.IsSubclassOf(declaringType)) { continue; }
				if (Attribute.IsDefined(method, typeIgnore)) { continue; }
				RegFunc(obj, null, method);
			}
			PropertyInfo[] properties = type.GetProperties(flags);
			for (int i = 0, imax = properties.Length; i < imax; i++) {
				PropertyInfo property = properties[i];
				Type declaringType = property.DeclaringType;
				if (baseType.Equals(declaringType) || baseType.IsSubclassOf(declaringType)) { continue; }
				if (Attribute.IsDefined(property, typeIgnore)) { continue; }
				RegVar(obj, null, property);
			}
			FieldInfo[] fields = type.GetFields(flags);
			for (int i = 0, imax = fields.Length; i < imax; i++) {
				FieldInfo field = fields[i];
				Type declaringType = field.DeclaringType;
				if (baseType.Equals(declaringType) || baseType.IsSubclassOf(declaringType)) { continue; }
				if (Attribute.IsDefined(field, typeIgnore)) { continue; }
				RegVar(obj, null, field);
			}
		}

		/// <summary>
		/// Register a single enrich function with the same name of 'func' defined in your code.
		/// </summary>
		/// <param name="func">the enrich function to be registered</param>
		public void RegisterFunction(Delegate func) {
			if (func == null) { throw new Exception("Invalid parameters !"); }
			MethodInfo method = func.Method;
			string error = RegFunc(func.Target, null, method);
			if (error != null) { throw new Exception(error); }
		}

		/// <summary>
		/// Register a single enrich function with the name 'funcName'
		/// </summary>
		/// <param name="funcName">the function name that will be used in content string</param>
		/// <param name="func">the enrich function to be registered</param>
		public void RegisterFunction(string funcName, Delegate func) {
			if (string.IsNullOrEmpty(funcName) || func == null) { throw new Exception("Invalid parameters !"); }
			MethodInfo method = func.Method;
			string error = RegFunc(func.Target, funcName, method);
			if (error != null) { throw new Exception(error); }
		}

		/// <summary>
		/// Register string variable value getter function that is used to retrieve string values in string content.
		/// </summary>
		/// <param name="getter">the function to retrieve string values</param>
		/// <returns>the function is successfully registered or not</returns>
		public bool RegisterVariableGetter(StringGetterDelegate getter) {
			if (getter == null) { return false; }
			if (mVariableStringGetters.Contains(getter)) { return false; }
			mVariableStringGetters.Add(getter);
			return true;
		}

		/// <summary>
		/// Unregister a string variable value getter function.
		/// </summary>
		/// <param name="getter">the function to be unregistered</param>
		/// <returns>the function is successfully unregistered or not</returns>
		public bool UnregisterStringGetter(StringGetterDelegate getter) {
			if (getter == null) { return false; }
			return mVariableStringGetters.Remove(getter);
		}

		/// <summary>
		/// String enrichment method, which is used for replace the replacements and retrieve the final string content.
		/// </summary>
		/// <param name="str">the string content that contains replacements to be replaced</param>
		/// <returns>the final string content</returns>
		public string EnrichString(string str) {
			while (true) {
				Match match = s_reg_func.Match(str);
				if (match == null || !match.Success) { break; }
				str = s_reg_func.Replace(str, GetFunctionString);
			}
			return str;
		}

		#region private fields

		private enum eParaType {
			Unsupported, Int, UInt, Long, ULong, Float, Double, Bool, String
		}

		private class FunctionData {
			public object target;
			public MethodInfo method;
			public List<eParaType> paraTypes;
			public bool hasArrayParams;
		}

		private class VariableData {
			public object target;
			public MemberInfo member;
		}

		private const string empty_chars = " \t\n\r";
		private static Type s_type_int = typeof(int);
		private static Type s_type_uint = typeof(uint);
		private static Type s_type_long = typeof(long);
		private static Type s_type_ulong = typeof(ulong);
		private static Type s_type_float = typeof(float);
		private static Type s_type_double = typeof(double);
		private static Type s_type_bool = typeof(bool);
		private static Type s_type_string = typeof(string);

		private static Regex s_reg_func = new Regex(@"\[@((?!\[@).)+?]");

		private NumericGetterDelegate mNumericGetter;
		private MathsCalculaterDelegate mMathsCalculater;

		private List<StringGetterDelegate> mVariableStringGetters = new List<StringGetterDelegate>();

		private string GetFunctionString(Match match) {
			string func = match.Value;
			//Debug.LogWarning(func);
			string str = "";
			try {
				str = GetStringInternal(func.Substring(2, func.Length - 3));
			} catch (Exception e) {
				if (onWarning != null) {
					onWarning(e.Message);
				}
			}
			return str;
		}

		private string GetVariableString(string varName) {
			VariableData data;
			if (mVariables.TryGetValue(varName, out data)) {
				string str = null;
				if (data.member is PropertyInfo) {
					str = (string)((data.member as PropertyInfo).GetValue(data.target, null));
				} else if (data.member is FieldInfo) {
					str = (string)((data.member as FieldInfo).GetValue(data.target));
				}
				if (str != null) { return str; }
			}
			for (int i = 0, imax = mVariableStringGetters.Count; i < imax; i++) {
				string str = mVariableStringGetters[i](varName);
				if (str != null) { return str; }
			}
			if (onWarning != null) {
				onWarning(string.Format("Cannot find string value of '{0}' !", varName));
			}
			return varName;
		}

		private double GetMathsVariable(string var) {
			VariableData data;
			if (mMathVariables.TryGetValue(var, out data)) {
				double ret = double.NaN;
				if (data.member is PropertyInfo) {
					ret = Convert.ToDouble((data.member as PropertyInfo).GetValue(data.target, null));
				} else if (data.member is FieldInfo) {
					ret = Convert.ToDouble((data.member as FieldInfo).GetValue(data.target));
				}
				if (!double.IsNaN(ret)) { return ret; }
			}
			return double.NaN;
		}

		private List<string> mCachedParams = new List<string>();
		private string GetStringInternal(string str) {
			if (string.IsNullOrEmpty(str)) { return null; }
			int index = 0;
			int length = str.Length;
			string funcName = GetSegment(str, ref index);
			char chr = GetNextChar(str, index - 1);
			if (string.IsNullOrEmpty(funcName)) {
				throw new Exception(string.Format("Unexpected char '{0}', at '{1}' !", chr, index));
			}
			if (chr != '(') {
				if (funcName[0] == '$') {
					return string.Concat(GetVariableString(funcName.Substring(1, funcName.Length - 1)),
						str.Substring(index, length - index));
				}
				return str;
			}
			index++;
			mCachedParams.Clear();
			int pStart = index;
			int pLength = 0;
			bool inQuote = false;
			bool isTrans = false;
			int bracketCount = 0;
			bool done = false;
			while (index < length) {
				isTrans = isTrans ? false : chr == '\\';
				chr = str[index++];
				if (chr == ',') {
					if (!inQuote && bracketCount == 0) {
						string p = str.Substring(pStart, pLength);
						if (string.IsNullOrEmpty(p.Trim())) {
							throw new Exception(string.Format("Unexpected char '{0}', at {1} !", chr, (index - 1)));
						}
						mCachedParams.Add(p);
						pStart = index;
						pLength = -1;
					}
				} else if (chr == ')') {
					if (!inQuote) {
						bracketCount--;
						if (bracketCount < 0) {
							string p = str.Substring(pStart, pLength);
							if (string.IsNullOrEmpty(p.Trim())) {
								if (mCachedParams.Count > 0) {
									throw new Exception(string.Format("Unexpected char '{0}', at {1} !", chr, (index - 1)));
								}
							} else {
								mCachedParams.Add(p);
							}
							done = true;
							break;
						}
					}
				} else if (chr == '(') {
					if (!inQuote) {
						bracketCount++;
					}
				} else if (chr == '\"') {
					if (!isTrans) {
						inQuote = !inQuote;
					}
				}
				pLength++;
			}
			if (!done) {
				throw new Exception("Statement not finished !");
			}
			//Debug.LogWarning(string.Format("{0}({1})", funcName, string.Join(", ", mCachedParams.ToArray())));

			int count = mCachedParams.Count;
			int min = count > 1 ? 1 : count;
			bool firstCheck = true;
			FunctionData funcData = null;
			while (count >= min) {
				string key = null;
				if (firstCheck) {
					key = string.Format("{0}:{1}", funcName, count);
					firstCheck = false;
					count++;
				} else if (count > 0) {
					key = string.Format("{0}:{1}+", funcName, count);
					count--;
				} else {
					break;
				}
				//Debug.LogWarning("key when dispatch : " + key);
				if (mFunctions.TryGetValue(key, out funcData)) { break; }
			}
			if (funcData == null) {
				throw new Exception(string.Format("Cannot find matched command '{0}' !" + str));
			}
			int normalParaCount = funcData.paraTypes.Count;
			if (funcData.hasArrayParams) { normalParaCount--; }
			string errorParam = null;
			eParaType errorType = eParaType.Unsupported;
			int parasCount = funcData.paraTypes.Count;
			object[] paras = new object[parasCount];
			int parasIndex = 0;
			for (int i = 0; i < normalParaCount; i++) {
				string para = mCachedParams[i];
				object val;
				if (TryParseValue(para, funcData.paraTypes[i], out val)) {
					paras[parasIndex] = val;
					parasIndex++;
				} else {
					errorParam = para;
					errorType = funcData.paraTypes[i];
					break;
				}
			}
			if (errorParam != null) {
				throw new Exception(string.Format("Fail to parse '{0}' into {1} !", errorParam, errorType));
			}
			if (funcData.hasArrayParams) {
				eParaType arrayItemType = funcData.paraTypes[normalParaCount];
				int arrayParasCount = mCachedParams.Count;
				Array arrayParas;
				if (!TryGetTypeArray(arrayItemType, arrayParasCount - normalParaCount, out arrayParas)) {
					throw new Exception("Fail to get array parameters array");
				}
				//Debug.Log("arrayParasCount  " + arrayParasCount + "   " + arrayParas.Length);
				for (int i = normalParaCount; i < arrayParasCount; i++) {
					string para = mCachedParams[i];
					object val;
					if (TryParseValue(para, funcData.paraTypes[normalParaCount], out val)) {
						arrayParas.SetValue(val, i - normalParaCount);
					} else {
						errorParam = para;
						errorType = funcData.paraTypes[normalParaCount];
					}
				}
				paras[parasIndex] = arrayParas;
				parasIndex++;
			}
			if (errorParam != null) {
				throw new Exception(string.Format("fail to parse '{0}' into {1}", errorParam, errorType));
			}
			mCachedParams.Clear();
			//Debug.Log("ready to invoke command : " + funcData.method.Name);
			return (string)funcData.method.Invoke(funcData.target, paras);
		}

		private string GetSegment(string str, ref int index) {
			int len = str.Length;
			int start = index;
			int segLen = 0;
			while (index < len) {
				bool done = false;
				char chr = str[index];
				if (empty_chars.IndexOf(chr) >= 0) {
					if (segLen > 0) {
						done = true;
					} else {
						start++;
					}
				} else if (chr == '(' || chr == ')' || chr == ',') {
					done = true;
				} else {
					segLen++;
				}
				if (done) { break; }
				index++;
			}
			return str.Substring(start, segLen);
		}

		private char GetNextChar(string str, int index) {
			int len = str.Length;
			while (++index < len) {
				char chr = str[index];
				if (empty_chars.IndexOf(chr) >= 0) { continue; }
				return chr;
			}
			return ' ';
		}

		private Dictionary<string, FunctionData> mFunctions = new Dictionary<string, FunctionData>();
		private Dictionary<string, VariableData> mVariables = new Dictionary<string, VariableData>();
		private Dictionary<string, VariableData> mMathVariables = new Dictionary<string, VariableData>();

		private List<string> mAliases = new List<string>();
		private StringBuilder mCachedErrors = new StringBuilder();

		private string RegFunc(object target, string funcName, MethodInfo method) {
			if (method.IsGenericMethod || method.ContainsGenericParameters) {
				return "Generic method is not supported !";
			}
			if (method.ReturnType != s_type_string) {
				return string.Format("Function '{0}' should return a string !", method.Name);
			}
			ParameterInfo[] paras = method.GetParameters();
			FunctionData funcData = new FunctionData();
			funcData.target = target;
			funcData.method = method;
			funcData.paraTypes = new List<eParaType>();
			funcData.hasArrayParams = false;
			//Debug.Log("method name : " + method.Name);
			for (int j = 0, jmax = paras.Length - 1; j <= jmax; j++) {
				ParameterInfo para = paras[j];
				Type tParam = para.ParameterType;
				//Debug.Log("method para type : " + tParam);
				eParaType eType = eParaType.Unsupported;
				if (tParam.IsArray) {
					if (j == jmax) {
						eType = GetParaType(tParam.GetElementType());
						funcData.hasArrayParams = true;
						//Debug.Log(GetParaType(tParam.GetElementType()) + " array");
					} else {
						return "Array can only be the last param";
					}
				} else {
					eType = GetParaType(tParam);
					//Debug.LogWarning(GetParaType(tParam));
				}
				if (eType == eParaType.Unsupported) {
					return string.Format("Type '{0}' not supportted !", tParam);
				}
				funcData.paraTypes.Add(eType);
			}
			mAliases.Clear();
			mCachedErrors.Remove(0, mCachedErrors.Length);
			if (string.IsNullOrEmpty(funcName)) {
				mAliases.Add(method.Name);
				StringEnricherAliasAttribute[] aliases = method.GetCustomAttributes(typeof(StringEnricherAliasAttribute), false) as StringEnricherAliasAttribute[];
				for (int i = 0, imax = aliases == null ? 0 : aliases.Length; i < imax; i++) {
					StringEnricherAliasAttribute alias = aliases[i];
					for (int j = 0, jmax = alias.alias.Length; j < jmax; j++) {
						string a = alias.alias[j];
						if (!mAliases.Contains(a)) { mAliases.Add(a); }
					}
				}
			} else {
				mAliases.Add(funcName);
			}
			for (int i = 0, imax = mAliases.Count; i < imax; i++) {
				string alias = mAliases[i];
				string key = string.Format("{0}:{1}{2}", alias, funcData.paraTypes.Count, funcData.hasArrayParams ? "+" : "");
				if (mFunctions.ContainsKey(key)) {
					mCachedErrors.AppendLine(string.Format("Method ({0}) with same name, param amount but different para type is not supportted !", alias));
					continue;
				}
				mFunctions.Add(key, funcData);
			}
			mAliases.Clear();
			string ret = mCachedErrors.Length > 0 ? mCachedErrors.ToString() : null;
			mCachedErrors.Remove(0, mCachedErrors.Length);
			return ret;
		}

		private string RegVar(object target, string varName, FieldInfo field) {
			if (field.FieldType == s_type_string) {
				return RegVarInternal(target, varName, field, false);
			} else if (IsNumberType(field.FieldType)) {
				return RegVarInternal(target, varName, field, true);
			} else {
				return string.Format("Invalid Property Type ({0} {1}) !", field.FieldType.FullName, field.Name);
			}
		}

		private string RegVar(object target, string varName, PropertyInfo property) {
			if (property.PropertyType == s_type_string) {
				return RegVarInternal(target, varName, property, false);
			} else if (IsNumberType(property.PropertyType)) {
				return RegVarInternal(target, varName, property, true);
			} else {
				return string.Format("Invalid Property Type ({0} {1}) !", property.PropertyType.FullName, property.Name);
			}
		}

		private string RegVarInternal(object target, string varName, MemberInfo member, bool isMaths) {
			VariableData data = new VariableData();
			data.target = target;
			data.member = member;
			mAliases.Clear();
			mCachedErrors.Remove(0, mCachedErrors.Length);
			if (string.IsNullOrEmpty(varName)) {
				mAliases.Add(member.Name);
				StringEnricherAliasAttribute[] aliases = member.GetCustomAttributes(typeof(StringEnricherAliasAttribute), false) as StringEnricherAliasAttribute[];
				for (int i = 0, imax = aliases == null ? 0 : aliases.Length; i < imax; i++) {
					StringEnricherAliasAttribute alias = aliases[i];
					for (int j = 0, jmax = alias.alias.Length; j < jmax; j++) {
						string a = alias.alias[j];
						if (!mAliases.Contains(a)) { mAliases.Add(a); }
					}
				}
			} else {
				mAliases.Add(varName);
			}
			Dictionary<string, VariableData> vars = isMaths ? mMathVariables : mVariables;
			for (int i = 0, imax = mAliases.Count; i < imax; i++) {
				string alias = mAliases[i];
				if (vars.ContainsKey(alias)) {
					mCachedErrors.AppendLine(string.Format("Variable with the name '{0}' is already existed !", alias));
					continue;
				}
				vars.Add(alias, data);
			}
			mAliases.Clear();
			string ret = mCachedErrors.Length > 0 ? mCachedErrors.ToString() : null;
			mCachedErrors.Remove(0, mCachedErrors.Length);
			return ret;
		}

		private eParaType GetParaType(Type t) {
			if (t == null) { return eParaType.Unsupported; }
			if (t.Equals(s_type_string)) { return eParaType.String; }
			if (t.Equals(s_type_int)) { return eParaType.Int; }
			if (t.Equals(s_type_uint)) { return eParaType.UInt; }
			if (t.Equals(s_type_bool)) { return eParaType.Bool; }
			if (t.Equals(s_type_float)) { return eParaType.Float; }
			if (t.Equals(s_type_long)) { return eParaType.Long; }
			if (t.Equals(s_type_ulong)) { return eParaType.ULong; }
			if (t.Equals(s_type_double)) { return eParaType.Double; }
			return eParaType.Unsupported;
		}

		private bool IsNumberType(Type type) {
			if (type == null) { return false; }
			if (type.Equals(s_type_int)) { return true; }
			if (type.Equals(s_type_int)) { return true; }
			if (type.Equals(s_type_uint)) { return true; }
			if (type.Equals(s_type_float)) { return true; }
			if (type.Equals(s_type_long)) { return true; }
			if (type.Equals(s_type_ulong)) { return true; }
			if (type.Equals(s_type_double)) { return true; }
			return false;
		}

		private bool TryParseValue(string p, eParaType t, out object val) {
			double d;
			switch (t) {
				case eParaType.Int:
					if (Calculate(p, out d) && !double.IsNaN(d)) {
						val = (int)d;
						return true;
					}
					break;
				case eParaType.UInt:
					if (Calculate(p, out d) && !double.IsNaN(d)) {
						val = (uint)d;
						return true;
					}
					break;
				case eParaType.Long:
					if (Calculate(p, out d) && !double.IsNaN(d)) {
						val = (long)d;
						return true;
					}
					break;
				case eParaType.ULong:
					if (Calculate(p, out d) && !double.IsNaN(d)) {
						val = (ulong)d;
						return true;
					}
					break;
				case eParaType.Float:
					if (Calculate(p, out d) && !double.IsNaN(d)) {
						val = (float)d;
						return true;
					}
					break;
				case eParaType.Double:
					if (Calculate(p, out d) && !double.IsNaN(d)) {
						val = d;
						return true;
					}
					break;
				case eParaType.Bool:
					bool boolValue;
					if (bool.TryParse(p, out boolValue)) {
						val = boolValue;
						return true;
					}
					break;
				case eParaType.String:
					val = "";
					try {
						val = GetStringInternal(p);
					} catch (Exception e) {
						if (onWarning != null) {
							onWarning(e.Message);
						}
						return false;
					}
					return true;
			}
			val = null;
			return false;
		}

		private bool TryGetTypeArray(eParaType t, int count, out Array arr) {
			switch (t) {
				case eParaType.Int:
					arr = new int[count];
					return true;
				case eParaType.UInt:
					arr = new uint[count];
					return true;
				case eParaType.Long:
					arr = new long[count];
					return true;
				case eParaType.ULong:
					arr = new ulong[count];
					return true;
				case eParaType.Float:
					arr = new float[count];
					return true;
				case eParaType.Double:
					arr = new double[count];
					return true;
				case eParaType.Bool:
					arr = new bool[count];
					return true;
				case eParaType.String:
					arr = new string[count];
					return true;
			}
			arr = null;
			return false;
		}

		private bool Calculate(string statement, out double ret) {
			if (double.TryParse(statement, out ret) && !double.IsNaN(ret)) { return true; }
			ret = GetMathsVariable(statement);
			if (double.IsNaN(ret) && mMathsCalculater != null) {
				try {
					ret = mMathsCalculater(statement, mNumericGetter);
				} catch { }
			}
			return !double.IsNaN(ret);
		}

		#endregion

	}

	/// <summary>
	/// An attribute that is used to specify alias for enrich functions and variables.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
	public sealed class StringEnricherAliasAttribute : Attribute {
		public readonly string[] alias;
		public StringEnricherAliasAttribute(params string[] alias) {
			this.alias = alias;
		}
	}

	/// <summary>
	/// An attribute that is used to specify a mehotd, field or property is NOT used for string enrichment.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
	public sealed class StringEnricherIgnoreAttribute : Attribute { }

}
