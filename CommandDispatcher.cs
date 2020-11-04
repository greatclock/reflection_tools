using System;
using System.Collections.Generic;
using System.Reflection;

namespace GreatClock.Common.Reflections {

	/// <summary>
	/// Shell style command dispatcher. Command string for example : "dosomething param1 param2".
	/// <para>String, boolean and numeric types are supported in parameters of your command method.</para>
	/// <para>Options "-option_name:value" in command string is optional supported.</para>
	/// <para>For example : you can dispatch "Add 3.14 2.7" to execute your method Add(float a, float b).</para>
	/// </summary>
	public sealed class CommandDispatcher {

		/// <summary>
		/// Check if Options "-option_name:value" in command string is supported.
		/// </summary>
		public bool enableOptions = false;

		/// <summary>
		/// Register a single command function with the command name the same as function.
		/// </summary>
		/// <param name="function">The command function delegate</param>
		public void AddCommand(Delegate function) {
			if (function == null) {
				throw new InvalidOperationException("Parameter 'function' cannot be null !");
			}
			mCachedErrors.Clear();
			AddCommand(null, function.Target, function.Method, mCachedErrors);
			if (mCachedErrors.Count > 0) {
				string errors = string.Join("\n", mCachedErrors.ToArray());
				mCachedErrors.Clear();
				throw new InvalidOperationException(errors);
			}
		}

		/// <summary>
		/// Register a single command function with specified name.
		/// </summary>
		/// <param name="methodName">The name of the command function</param>
		/// <param name="function">The command function delegate</param>
		public void AddCommand(string methodName, Delegate function) {
			if (string.IsNullOrEmpty(methodName)) {
				throw new InvalidOperationException("Parameter 'methodName' cannot be null or empty !");
			}
			if (function == null) {
				throw new InvalidOperationException("Parameter 'function' cannot be null !");
			}
			mCachedErrors.Clear();
			AddCommand(methodName, function.Target, function.Method, mCachedErrors);
			if (mCachedErrors.Count > 0) {
				string errors = string.Join("\n", mCachedErrors.ToArray());
				mCachedErrors.Clear();
				throw new InvalidOperationException(errors);
			}
		}

		/// <summary>
		/// Register all static methods in 'type' as command functions.
		/// </summary>
		/// <param name="type">The type in which command functions are defined</param>
		public void AddCommands(Type type) {
			if (type == null) {
				throw new InvalidOperationException("Parameter 'type' cannot be null !");
			}
			Type typeIgnore = typeof(CommandIgnoreAttribute);
			mCachedErrors.Clear();
			MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0, imax = methods.Length; i < imax; i++) {
				MethodInfo method = methods[i];
				if (Attribute.IsDefined(method, typeIgnore)) { continue; }
				AddCommand(null, null, method, mCachedErrors);
			}
			if (mCachedErrors.Count > 0) {
				string errors = string.Join("\n", mCachedErrors.ToArray());
				mCachedErrors.Clear();
				throw new InvalidOperationException(errors);
			}
		}

		/// <summary>
		/// Register all STATIC methods in type T as command functions.
		/// </summary>
		/// <typeparam name="T">The type in which command functions are defined</typeparam>
		public void AddCommands<T>() where T : class {
			Type type = typeof(T);
			Type typeIgnore = typeof(CommandIgnoreAttribute);
			mCachedErrors.Clear();
			MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0, imax = methods.Length; i < imax; i++) {
				MethodInfo method = methods[i];
				if (Attribute.IsDefined(method, typeIgnore)) { continue; }
				AddCommand(null, null, method, mCachedErrors);
			}
			if (mCachedErrors.Count > 0) {
				string errors = string.Join("\n", mCachedErrors.ToArray());
				mCachedErrors.Clear();
				throw new InvalidOperationException(errors);
			}
		}

		/// <summary>
		/// Register all MEMBER methods in type T as command functions.
		/// </summary>
		/// <typeparam name="T">The type in which command functions are defined</typeparam>
		/// <param name="obj">The instance that contains command functions</param>
		public void AddCommands<T>(T obj, Type baseType) where T : class {
			if (obj == null) {
				throw new InvalidOperationException("Parameter 'obj' cannot be null when adding non static methods !");
			}
			if (baseType == null) {
				baseType = typeof(object);
			}
			Type type = typeof(T);
			Type typeIgnore = typeof(CommandIgnoreAttribute);
			mCachedErrors.Clear();
			MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0, imax = methods.Length; i < imax; i++) {
				MethodInfo method = methods[i];
				Type declaringType = method.DeclaringType;
				if (baseType.Equals(declaringType) || baseType.IsSubclassOf(declaringType)) { continue; }
				if (Attribute.IsDefined(method, typeIgnore)) { continue; }
				AddCommand(null, obj, method, mCachedErrors);
			}
			if (mCachedErrors.Count > 0) {
				string errors = string.Join("\n", mCachedErrors.ToArray());
				mCachedErrors.Clear();
				throw new InvalidOperationException(errors);
			}
		}

		/// <summary>
		/// The method for retrieving manual of a specific command.
		/// </summary>
		/// <param name="cmd">The name of the command</param>
		/// <param name="manuals">The list to receive command manuals</param>
		public void GetManual(string cmd, IList<string> manuals) {
			List<string> ms;
			if (!mManuals.TryGetValue(cmd, out ms)) { return; }
			for (int i = 0, imax = ms.Count; i < imax; i++) {
				manuals.Add(ms[i]);
			}
		}

		/// <summary>
		/// The method for getting all command that registered.
		/// <para>Each command name string follows the format "command_name:para_count" or "command_name:para_count+".</para>
		/// <para>If the command name string ends with '+', it means the last parameter of it is an array of arbitrary length.</para>
		/// </summary>
		/// <param name="list">The list to receive all command name strings</param>
		public void GetAllCommands(IList<string> list) {
			if (list == null) { return; }
			foreach (string key in mCmds.Keys) {
				list.Add(key);
			}
		}

		/// <summary>
		/// The method for executing a command.
		/// </summary>
		/// <param name="cmd">A command string that contains command name and parameters</param>
		public void Execute(string cmd) {
			if (string.IsNullOrEmpty(cmd)) { return; }
			int index = 0;
			int length = cmd.Length;
			int start = 0;
			bool inQuote = false;
			bool isLastQuote = false;
			bool isTrans = false;
			bool expectEmpty = false;
			string cmdName = null;
			int contentType = 0;
			mCachedParams.Clear();
			mCachedCmdOpts.Clear();
			string error = null;
			while (index < length) {
				char c = cmd[index];
				bool isLast = index == length - 1;
				bool trans = false;
				bool isEmpty = c == ' ' || c == '\t';
				bool forceBreak = false;
				int nextContentType = -1;
				if (expectEmpty && !isEmpty) {
					error = "Empty char expected";
					break;
				}
				expectEmpty = false;
				bool quote = false;
				if (c == '"') {
					if (!isTrans) {
						quote = true;
						inQuote = !inQuote;
						if (inQuote) {
							if (start != index) {
								error = "'\"' can only be the start of a content !";
								break;
							}
						} else {
							expectEmpty = true;
						}
					}
				} else if (c == '\\') {
					trans = true;
				} else if (c == '-') {
					if (enableOptions && start == index) {
						if (mCachedParams.Count > 0 || contentType == 0) {
							error = "Options should be between command name and the first parameter !";
							break;
						}
						nextContentType = 1;
						start = index + 1;
					}
				} else if (c == ':') {
					if (contentType == 1) {
						nextContentType = 2;
						forceBreak = true;
					}
				}
				if (isEmpty || isLast || forceBreak) {
					if (!inQuote) {
						if (index > start || isLast) {
							int startIndex = start;
							int strLength = index - start;
							if (isLastQuote || (isLast && quote)) {
								startIndex++;
								strLength -= 2;
							}
							string ss = cmd.Substring(startIndex, isLast ? strLength + 1 : strLength);
							if (contentType == 0) {
								cmdName = ss;
								//Debug.Log("cmd name : " + cmdName);
								contentType = 10;
							} else if (contentType == 1) {
								//Debug.Log("options : " + ss);
								for (int i = 0, imax = ss.Length; i < imax; i++) {
									CommandOption opt = new CommandOption();
									opt.optionName = ss[i];
									mCachedCmdOpts.Add(opt);
								}
								if (c == ':') {
									if (ss.Length != 1) {
										error = "Option parameter cannot follow multi options !";
										break;
									}
								} else {
									nextContentType = 10;
								}
							} else if (contentType == 2) {
								//Debug.Log("option param : " + ss);
								int i = mCachedCmdOpts.Count - 1;
								CommandOption opt = mCachedCmdOpts[i];
								opt.optionParam = ss;
								mCachedCmdOpts[i] = opt;
								nextContentType = 10;
							} else {
								mCachedParams.Add(ss.Replace("\\\"", "\""));
								//Debug.Log("param : " + ss.Replace("\\\"", "\""));
							}
						}
						start = index + 1;
					}
				}
				isLastQuote = quote;
				isTrans = trans;
				if (nextContentType >= 0) { contentType = nextContentType; }
				index++;
			}
			if (error != null) {
				throw new Exception(string.Format("{0} at {1}", error, index));
			}
			//List<string> opts = new List<string>();
			//foreach (CommandOption o in mCachedCmdOpts) {
			//opts.Add(string.Format("{0}：{1}", o.optionName, o.optionParam));
			//}
			//Debug.Log(string.Format("cmd name : {0}    opts : [{1}]    params : [{2}]", cmdName,
			//	string.Join(", ", opts.ToArray()), string.Join(", ", mCachedParams.ToArray())));
			int count = mCachedParams.Count;
			int min = count > 1 ? 1 : count;
			bool firstCheck = true;
			CommandData cmdData = null;
			while (count >= min) {
				string key = null;
				if (firstCheck) {
					key = string.Format("{0}:{1}", cmdName, count);
					firstCheck = false;
					count++;
				} else if (count > 0) {
					key = string.Format("{0}:{1}+", cmdName, count);
					count--;
				} else {
					break;
				}
				if (mCmds.TryGetValue(key, out cmdData)) { break; }
			}
			if (cmdData == null) {
				throw new Exception(string.Format("Cannot find matched command '{0}' !", cmd));
			}
			if (!cmdData.hasOptions && mCachedCmdOpts.Count > 0) {
				throw new Exception(string.Format("Command '{0}' does not require any options !", cmd));
			}
			int normalParaCount = cmdData.paraTypes.Count;
			if (cmdData.hasArrayParams) { normalParaCount--; }
			string errorParam = null;
			eParaType errorType = eParaType.Unsupported;
			int parasCount = cmdData.paraTypes.Count;
			if (cmdData.hasOptions) { parasCount++; }
			object[] paras = new object[parasCount];
			int parasIndex = 0;
			if (cmdData.hasOptions) {
				paras[parasIndex] = mCachedCmdOpts;
				parasIndex++;
			}
			for (int i = 0; i < normalParaCount; i++) {
				string para = mCachedParams[i];
				object val;
				if (TryParseValue(para, cmdData.paraTypes[i], out val)) {
					paras[parasIndex] = val;
					parasIndex++;
				} else {
					errorParam = para;
					errorType = cmdData.paraTypes[i];
					break;
				}
			}
			if (errorParam != null) {
				throw new Exception(string.Format("Fail to parse '{0}' into {1} !", errorParam, errorType));
			}
			if (cmdData.hasArrayParams) {
				eParaType arrayItemType = cmdData.paraTypes[normalParaCount];
				int arrayParasCount = mCachedParams.Count;
				Array arrayParas;
				if (!TryGetTypeArray(arrayItemType, arrayParasCount - normalParaCount, out arrayParas)) {
					throw new Exception("Fail to get array parameters array !");
				}
				for (int i = normalParaCount; i < arrayParasCount; i++) {
					string para = mCachedParams[i];
					object val;
					if (TryParseValue(para, cmdData.paraTypes[normalParaCount], out val)) {
						arrayParas.SetValue(val, i - normalParaCount);
					} else {
						errorParam = para;
						errorType = cmdData.paraTypes[normalParaCount];
					}
				}
				paras[parasIndex] = arrayParas;
				parasIndex++;
			}
			if (errorParam != null) {
				throw new Exception(string.Format("Fail to parse '{0}' into {1}", errorParam, errorType));
			}
			//Debug.Log("ready to invoke command : " + cmdData.method.Name);
			cmdData.method.Invoke(cmdData.obj, paras);
		}

		#region private
		private enum eParaType {
			Unsupported, Int, UInt, Long, ULong, Float, Double, Bool, String
		}

		private static Type s_type_int = typeof(int);
		private static Type s_type_uint = typeof(uint);
		private static Type s_type_long = typeof(long);
		private static Type s_type_ulong = typeof(ulong);
		private static Type s_type_float = typeof(float);
		private static Type s_type_double = typeof(double);
		private static Type s_type_bool = typeof(bool);
		private static Type s_type_string = typeof(string);

		private static Type s_type_list_cmdopt = typeof(List<CommandOption>);
		private static Type s_type_ilist_cmdopt = typeof(IList<CommandOption>);

		private class CommandData {
			public object obj;
			public MethodInfo method;
			public List<eParaType> paraTypes;
			public bool hasArrayParams;
			public bool hasOptions;
		}

		private Dictionary<string, CommandData> mCmds = new Dictionary<string, CommandData>();
		private Dictionary<string, List<string>> mManuals = new Dictionary<string, List<string>>();
		private List<CommandOption> mCachedCmdOpts = new List<CommandOption>();
		private List<string> mCachedParams = new List<string>();
		private List<string> mCachedErrors = new List<string>();
		private List<string> mCachedCmdNames = new List<string>();

		private void AddCommand(string methodName, object obj, MethodInfo method, IList<string> errors) {
			if (method.IsGenericMethod || method.ContainsGenericParameters) {
				errors.Add(string.Format("Generic method or method with generic parameters is not supported ! (At '{0}')", method.Name));
				return;
			}
			if (!method.IsStatic && obj == null) {
				errors.Add(string.Format("Instance is required when method is non static ! (At '{0}')", method.Name));
				return;
			}
			mCachedCmdNames.Clear();
			bool aliasSet = false;
			if (!string.IsNullOrEmpty(methodName)) {
				aliasSet = true;
				mCachedCmdNames.Add(methodName);
			}
			CommandAliasAttribute[] aliases = method.GetCustomAttributes(typeof(CommandAliasAttribute), false) as CommandAliasAttribute[];
			if (aliases != null && aliases.Length > 0) {
				for (int i = 0, imax = aliases.Length; i < imax; i++) {
					CommandAliasAttribute alias = aliases[i];
					for (int j = 0, jmax = alias.alias.Length; j < jmax; j++) {
						string a = alias.alias[j];
						if (string.IsNullOrEmpty(a) || a == methodName) { continue; }
						mCachedCmdNames.Add(a);
						aliasSet = true;
					}
				}
			}
			if (!aliasSet) {
				mCachedCmdNames.Add(method.Name);
			}
			ParameterInfo[] paras = method.GetParameters();
			CommandData cmd = new CommandData();
			int firstParaIndex = 0;
			if (paras.Length > 0) {
				ParameterInfo param = paras[0];
				if (param.ParameterType.Equals(s_type_list_cmdopt) || param.ParameterType.Equals(s_type_ilist_cmdopt)) {
					firstParaIndex = 1;
					cmd.hasOptions = true;
				}
			}
			cmd.obj = obj;
			cmd.method = method;
			cmd.paraTypes = new List<eParaType>();
			cmd.hasArrayParams = false;
			for (int j = firstParaIndex, jmax = paras.Length - 1; j <= jmax; j++) {
				ParameterInfo para = paras[j];
				Type tParam = para.ParameterType;
				eParaType eType = eParaType.Unsupported;
				if (tParam.IsArray) {
					if (j != jmax) {
						errors.Add(string.Format("Array can only be the last param !  (At '{0}')", method.Name));
						return;
					}
					eType = GetParaType(tParam.GetElementType());
					cmd.hasArrayParams = true;
					//Debug.Log(GetParaType(tParam.GetElementType()) + " array");
				} else {
					eType = GetParaType(tParam);
					//Debug.LogWarning(GetParaType(tParam));
				}
				if (eType == eParaType.Unsupported) {
					errors.Add(string.Format("Type '{0}' not supported ! (At '{0}')", tParam, method.Name));
					return;
				}
				cmd.paraTypes.Add(eType);
			}
			int ret = 0;
			for (int i = 0, imax = mCachedCmdNames.Count; i < imax; i++) {
				string cmdName = mCachedCmdNames[i];
				string key = string.Format("{0}:{1}{2}", cmdName, cmd.paraTypes.Count, cmd.hasArrayParams ? "+" : "");
				if (mCmds.ContainsKey(key)) {
					errors.Add(string.Format("Method with same name, param amount but different para type is not supported ! (At '{0}')", method.Name));
					ret++;
					continue;
				}
				mCmds.Add(key, cmd);
				CommandManualAttribute manual = Attribute.GetCustomAttribute(method, typeof(CommandManualAttribute)) as CommandManualAttribute;
				if (manual != null) {
					List<string> ms;
					if (mManuals.TryGetValue(cmdName, out ms)) {
						ms.Add(manual.manual);
					} else {
						ms = new List<string>();
						ms.Add(manual.manual);
						mManuals.Add(cmdName, ms);
					}
				}
			}
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

		private bool TryParseValue(string p, eParaType t, out object val) {
			switch (t) {
				case eParaType.Int:
					int intValue;
					if (int.TryParse(p, out intValue)) {
						val = intValue;
						return true;
					}
					break;
				case eParaType.UInt:
					uint uintValue;
					if (uint.TryParse(p, out uintValue)) {
						val = uintValue;
						return true;
					}
					break;
				case eParaType.Long:
					long longValue;
					if (long.TryParse(p, out longValue)) {
						val = longValue;
						return true;
					}
					break;
				case eParaType.ULong:
					ulong ulongValue;
					if (ulong.TryParse(p, out ulongValue)) {
						val = ulongValue;
						return true;
					}
					break;
				case eParaType.Float:
					float floatValue;
					if (float.TryParse(p, out floatValue)) {
						val = floatValue;
						return true;
					}
					break;
				case eParaType.Double:
					double doubleValue;
					if (double.TryParse(p, out doubleValue)) {
						val = doubleValue;
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
					val = p;
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
		#endregion

	}

	/// <summary>
	/// An attribute that is used to define manual content for command functions.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class CommandManualAttribute : Attribute {
		public readonly string manual;
		public CommandManualAttribute(string manual) {
			this.manual = manual;
		}
	}

	/// <summary>
	/// An attribute that is used to specify alias for command functions.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public sealed class CommandAliasAttribute : Attribute {
		public readonly string[] alias;
		public CommandAliasAttribute(params string[] alias) {
			this.alias = alias;
		}
	}

	/// <summary>
	/// An attribute that is used to mark a method is NOT a command function.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class CommandIgnoreAttribute : Attribute { }

	/// <summary>
	/// A struct that is used in command function parameter to deal with options parameters '-option_name:value'.
	/// </summary>
	public struct CommandOption {
		public char optionName;
		public string optionParam;
	}

}