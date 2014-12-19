using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SIL.WritingSystems.Collation
{
	public class LdmlCollationParser
	{
		private const string NewLine = "\r\n";
		private static readonly Regex UnicodeEscape4Digit = new Regex(@"\\[u]([0-9A-F]{4})", RegexOptions.IgnoreCase);
		private static readonly Regex UnicodeEscape8Digit = new Regex(@"\\[U]([0-9A-F]{8})", RegexOptions.IgnoreCase);

		/// <summary>
		/// This method will replace any unicode escapes in rules with their actual unicode characters
		/// and return the resulting string.
		/// Method created since IcuRulesCoallator does not appear to interpret unicode escapes.
		/// </summary>
		/// <param name="rules"></param>
		public static string ReplaceUnicodeEscapesForICU(string rules)
		{
			if (!string.IsNullOrEmpty(rules))
			{
				//replace all unicode escapes in the rules string with the unicode character they represent.
				rules = UnicodeEscape8Digit.Replace(rules, match => ((char) int.Parse(match.Groups[1].Value,
																					  NumberStyles.HexNumber)).ToString());
				rules = UnicodeEscape4Digit.Replace(rules, match => ((char) int.Parse(match.Groups[1].Value,
																					  NumberStyles.HexNumber)).ToString());
			}
			return rules;
		}

		public static string GetIcuRulesFromCollationNode(XElement collationElem)
		{
			if (collationElem == null)
			{
				throw new ArgumentNullException("collationElem");
			}

			string icuRules = string.Empty;
			string variableTop = null;
			int variableTopPositionIfNotUsed = 0;
			XElement settingsElem = collationElem.Element("settings");
			if (settingsElem != null)
			{
				icuRules += GetIcuSettingsFromSettingsNode(settingsElem, out variableTop);
				variableTopPositionIfNotUsed = icuRules.Length;
			}
			XElement suppressElem = collationElem.Element("suppress_contractions");
			if (suppressElem != null)
			{
				icuRules += GetIcuOptionFromNode(suppressElem);
			}
			XElement optimizeElem = collationElem.Element("optimize");
			if (optimizeElem != null)
			{
				icuRules += GetIcuOptionFromNode(optimizeElem);
			}
			XElement rulesElem = collationElem.Element("rules");
			if (rulesElem != null)
			{
				icuRules += GetIcuRulesFromRulesNode(rulesElem, ref variableTop);
			}

			if (variableTop != null)
			{
				string variableTopRule = String.Format(NewLine + "&{0} < [variable top]", EscapeForIcu(variableTop));
				if (variableTopPositionIfNotUsed == icuRules.Length)
				{
					icuRules += variableTopRule;
				}
				else
				{
					icuRules = String.Format("{0}{1}{2}", icuRules.Substring(0, variableTopPositionIfNotUsed),
											 variableTopRule, icuRules.Substring(variableTopPositionIfNotUsed));
				}
			}
			return TrimUnescapedWhitespace(icuRules);
		}

		/// <summary>
		/// Trim all whitespace from the beginning, and all unescaped whitespace from the end.
		/// </summary>
		/// <param name="icuRules"></param>
		/// <returns></returns>
		private static string TrimUnescapedWhitespace(string icuRules)
		{
			var lastEscapeIndex = icuRules.LastIndexOf('\\');
			if(lastEscapeIndex + 2 == icuRules.Length)
			{
				return icuRules.TrimStart();
			}
			return icuRules.Trim();
		}

		public static bool TryGetSimpleRulesFromCollationNode(XElement collationElem, out string rules)
		{
			if (collationElem == null)
			{
				throw new ArgumentNullException("collationElem");
			}

			rules = null;
			XElement settingsElem = collationElem.Element("settings");
			// simple rules can't deal with any non-default settings
			if (settingsElem != null)
			{ 
				return false;
			}
			XElement rulesElem = collationElem.Element("rules");
			if (rulesElem == null)
			{
				rules = string.Empty;
				return true;
			}
			rules = GetSimpleRulesFromRulesNode(rulesElem);
			return rules != null;
		}

		private static string GetSimpleRulesFromRulesNode(XElement element)
		{
			if (element.IsEmpty)
			{
				return string.Empty;
			}
			bool first = true;
			bool inGroup = false;
			string simpleRules = string.Empty;
			foreach(XElement childElem in element.Elements())
			{ 
				// First child node MUST BE <reset before="primary"><first_non_ignorable /></reset>
				if (first &&
					(childElem.Name != "reset" || GetBeforeOption(childElem) != "[before 1] " || GetIcuData(childElem) != "[first regular]"))
				{
					return null;
				}
				if (first)
				{
					first = false;
					continue;
				}
				switch (childElem.Name.ToString())
				{
					case "p":
						simpleRules += EndSimpleGroupIfNeeded(ref inGroup) + NewLine + GetTextData(childElem);
						break;
					case "s":
						simpleRules += EndSimpleGroupIfNeeded(ref inGroup) + " " + GetTextData(childElem);
						break;
					case "t":
						BeginSimpleGroupIfNeeded(ref inGroup, ref simpleRules);
						simpleRules += " " + GetTextData(childElem);
						break;
					case "pc":
						simpleRules += EndSimpleGroupIfNeeded(ref inGroup) +
									   BuildSimpleRulesFromConcatenatedData(NewLine, GetTextData(childElem));
						break;
					case "sc":
						simpleRules += EndSimpleGroupIfNeeded(ref inGroup) +
									   BuildSimpleRulesFromConcatenatedData(" ", GetTextData(childElem));
						break;
					case "tc":
						BeginSimpleGroupIfNeeded(ref inGroup, ref simpleRules);
						simpleRules += BuildSimpleRulesFromConcatenatedData(" ", GetTextData(childElem));
						break;
					default: // element name not allowed for simple rules conversion
						return null;
				}
			}
			simpleRules += EndSimpleGroupIfNeeded(ref inGroup);
			return simpleRules.Trim();
		}

		private static string EndSimpleGroupIfNeeded(ref bool inGroup)
		{
			if (!inGroup)
			{
				return string.Empty;
			}
			inGroup = false;
			return ")";
		}

		private static void BeginSimpleGroupIfNeeded(ref bool inGroup, ref string rules)
		{
			if (inGroup)
			{
				return;
			}
			inGroup = true;
			int lastIndexOfNewLine = rules.LastIndexOf(NewLine) + NewLine.Length;
			int lastIndexOfSpace = rules.LastIndexOf(" ") + " ".Length;
			int mostRecentPossiblepositionForABracket = Math.Max(lastIndexOfNewLine, lastIndexOfSpace);
			rules = rules.Insert(mostRecentPossiblepositionForABracket, "(");
			return;
		}

		/// <summary>
		/// This method will escape necessary characters while avoiding escaping characters that are already escaped
		/// and leave unicode escape sequences alone.
		/// </summary>
		/// <param name="unescapedData"></param>
		/// <returns></returns>
		private static string EscapeForIcu(string unescapedData)
		{
			const int longEscapeLen = 10; //length of a \UFFFFFFFF escape
			const int shortEscLen = 6; //length of a \uFFFF escape
			string result = string.Empty;
			bool escapeNeedsClosing = false;
			for (int i = 0; i < unescapedData.Length; i++)
			{
				//if we are looking at an backslash check if the following character needs escaping, if it does
				//we do not need to escape it again
				if ((unescapedData[i] == '\\' || unescapedData[i] == '\'') && i + 1 < unescapedData.Length
					&& NeedsEscaping(Char.ConvertToUtf32(unescapedData, i + 1), "" + unescapedData[i + 1]))
				{
					result += unescapedData[i++]; //add the backslash and advance
					result += unescapedData[i]; //add the already escaped character
				} //handle long unicode escapes
				else if (i + longEscapeLen <= unescapedData.Length &&
						 UnicodeEscape8Digit.IsMatch(unescapedData.Substring(i, longEscapeLen)))
				{
					result += unescapedData.Substring(i, longEscapeLen);
					i += longEscapeLen - 1;
				} //handle short unicode escapes
				else if (i + shortEscLen <= unescapedData.Length &&
						 UnicodeEscape4Digit.IsMatch(unescapedData.Substring(i, shortEscLen)))
				{
					result += unescapedData.Substring(i, shortEscLen);
					i += shortEscLen - 1;
				}
				else
				{
					//handle everything else
					result += EscapeForIcu(Char.ConvertToUtf32(unescapedData, i), ref escapeNeedsClosing);
					if (Char.IsSurrogate(unescapedData, i))
					{
						i++;
					}
				}
			}
			return escapeNeedsClosing ? result + "'" : result;
		}

		private static string EscapeForIcu(int code, ref bool alreadyEscaping)
		{
			string result = String.Empty;
			string ch = Char.ConvertFromUtf32(code);
			// ICU only requires escaping all whitespace and any ASCII character that is not a letter or digit
			// Honestly, there shouldn't be any whitespace that is a surrogate, but we're checking
			// to maintain the highest compatibility with future Unicode code points.
			if (NeedsEscaping(code, ch))
			{
				if (!alreadyEscaping)
				{
					//Escape a single quote ' with single quote '', but don't start a sequence.
					if (ch != "'")
					{
						alreadyEscaping = true;
					}
					//begin the escape sequence.
					result += "'";
				}
				result += ch;
			}
			else
			{
				if (alreadyEscaping)
				{
					alreadyEscaping = false;
					result = "'";
				}
				result += ch;
			}
			return result;
		}

		private static bool NeedsEscaping(int code, string ch)
		{
			return (code < 0x7F && !Char.IsLetterOrDigit(ch, 0)) || Char.IsWhiteSpace(ch, 0);
		}

		private static string BuildSimpleRulesFromConcatenatedData(string op, string data)
		{
			string rule = string.Empty;
			bool surrogate = false;
			for (int i = 0; i < data.Length; i++)
			{
				if (surrogate)
				{
					rule += data[i];
					surrogate = false;
					continue;
				}
				rule += op + data[i];
				if (Char.IsSurrogate(data, i))
				{
					surrogate = true;
				}
			}
			return rule;
		}

		private static string GetIcuSettingsFromSettingsNode(XElement settingsElem, out string variableTop)
		{
			Debug.Assert(settingsElem.Name == "settings");
			variableTop = null;
			Dictionary<string, string> strengthValues = new Dictionary<string, string>();
			strengthValues["primary"] = "1";
			strengthValues["secondary"] = "2";
			strengthValues["tertiary"] = "3";
			strengthValues["quaternary"] = "4";
			strengthValues["identical"] = "I";
			string icuSettings = string.Empty;
			foreach (XAttribute att in settingsElem.Attributes())
			{
				switch (att.Name.ToString())
				{
					case "alternate":
					case "normalization":
					case "caseLevel":
					case "caseFirst":
					case "numeric":
						icuSettings += String.Format(NewLine + "[{0} {1}]", att.Name, att.Value);
						break;
					case "strength":
						if (!strengthValues.ContainsKey(att.Value))
						{
							throw new ApplicationException("Invalid collation strength setting in LDML");
						}
						icuSettings += String.Format(NewLine + "[strength {0}]", strengthValues[att.Value]);
						break;
					case "backwards":
						if (att.Value != "off" && att.Value != "on")
						{
							throw new ApplicationException("Invalid backwards setting in LDML collation.");
						}
						icuSettings += String.Format(NewLine + "[backwards {0}]", att.Value == "off" ? "1" : "2");
						break;
					case "hiraganaQuaternary":
						icuSettings += String.Format(NewLine + "[hiraganaQ {0}]", att.Value);
						break;
					case "variableTop":
						variableTop = EscapeForIcu(UnescapeVariableTop(att.Value));
						break;
				}
			}
			return icuSettings;
		}

		private static string UnescapeVariableTop(string variableTop)
		{
			string result = string.Empty;
			foreach (string hexCode in variableTop.Split('u'))
			{
				if (String.IsNullOrEmpty(hexCode))
				{
					continue;
				}
				result += Char.ConvertFromUtf32(int.Parse(hexCode, NumberStyles.AllowHexSpecifier));
			}
			return result;
		}

		private static string GetIcuOptionFromNode(XElement collationElem)
		{
			Debug.Assert(collationElem.NodeType == XmlNodeType.Element);
			string result = string.Empty;
			switch (collationElem.Name.ToString())
			{
				case "suppress_contractions":
				case "optimize":
					result = String.Format(NewLine + "[{0} {1}]", collationElem.Name.ToString().Replace('_', ' '), collationElem.Value);
					break;
				default:
					throw new ApplicationException(String.Format("Invalid LDML collation option element: {0}", collationElem.Name));
			}
			return result;
		}

		private static string GetIcuRulesFromRulesNode(XElement rulesElem, ref string variableTop)
		{
			string rules = string.Empty;
			if (rulesElem != null)
			{ 
				foreach(XElement elem in rulesElem.Elements())
				{
					string icuData;
					switch (elem.Name.ToString())
					{
						case "reset":
							string beforeOption = GetBeforeOption(elem);
							icuData = GetIcuData(elem);
							// I added a space after the ampersand to increase readability with situations where the first
							// character following a reset may be a combining character or some other character that would be
							// rendered around the ampersand
							rules += String.Format(NewLine + "& {2}{0}{1}", icuData, GetVariableTopString(icuData, ref variableTop),
								beforeOption);
							break;
						case "p":
							icuData = GetIcuData(elem);
							rules += String.Format(" < {0}{1}", icuData, GetVariableTopString(icuData, ref variableTop));
							break;
						case "s":
							icuData = GetIcuData(elem);
							rules += String.Format(" << {0}{1}", icuData, GetVariableTopString(icuData, ref variableTop));
							break;
						case "t":
							icuData = GetIcuData(elem);
							rules += String.Format(" <<< {0}{1}", icuData, GetVariableTopString(icuData, ref variableTop));
							break;
						case "i":
							icuData = GetIcuData(elem);
							rules += String.Format(" = {0}{1}", icuData, GetVariableTopString(icuData, ref variableTop));
							break;
						case "pc":
							rules += BuildRuleFromConcatenatedData("<", elem, ref variableTop);
							break;
						case "sc":
							rules += BuildRuleFromConcatenatedData("<<", elem, ref variableTop);
							break;
						case "tc":
							rules += BuildRuleFromConcatenatedData("<<<", elem, ref variableTop);
							break;
						case "ic":
							rules += BuildRuleFromConcatenatedData("=", elem, ref variableTop);
							break;
						case "x":
							rules += GetRuleFromExtendedNode(elem);
							break;
						default:
							throw new ApplicationException(String.Format("Invalid LDML collation rule element: {0}", elem.Name));
					}
				}
			}
			return rules;
		}

		private static string GetBeforeOption(XElement element)
		{
			switch ((string)element.Attribute("before"))
			{
				case "primary":
					return "[before 1] ";
				case "secondary":
					return "[before 2] ";
				case "tertiary":
					return "[before 3] ";
				case "":
				case null:
					return string.Empty;
				default:
					throw new ApplicationException("Invalid before specifier on reset collation element.");
			}
		}

		private static string GetIcuData(XElement element)
		{
			if (element.IsEmpty)
			{
				throw new ApplicationException(String.Format("Empty LDML collation rule: {0}", element.Name));
			}
			XElement child = element.Elements().FirstOrDefault();
			if ((child != null) && (child.Name != "cp"))
			{
				return GetIndirectPosition(child);
			}
			string data = GetTextData(element);
			return EscapeForIcu(data);
		}

		private static string GetTextData(XElement element)
		{
			string data = string.Empty;
			foreach (XElement child in element.Elements("cp"))
				data += GetCPData(child);
			if (data == string.Empty)
				data = (string) element;
			return data;
		}

		private static string GetCPData(XElement element)
		{
			Debug.Assert(element.NodeType == XmlNodeType.Element);
			if (element.Name != "cp")
			{
				throw new ApplicationException(string.Format("Unexpected element '{0}' in text data node", element.Name));
			}
			string hex = (string)element.Attribute("hex");
			string result = string.Empty;
			if (!string.IsNullOrEmpty(hex))
			{
				int code;
				if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, null, out code))
				{
					throw new ApplicationException("Invalid non-hexadecimal character code in LDML 'cp' element.");
				}
				try
				{
					result = Char.ConvertFromUtf32(code);
				}
				catch (ArgumentOutOfRangeException e)
				{
					throw new ApplicationException("Invalid Unicode code point in LDML 'cp' element.", e);
				}
			}
			return result;
		}

		private static string GetIndirectPosition(XElement element)
		{
			string result;
			switch (element.Name.ToString())
			{
				case "first_non_ignorable":
					result = "[first regular]";
					break;
				case "last_non_ignorable":
					result = "[last regular]";
					break;
				default:
					result = "[" + element.Name.ToString().Replace('_', ' ') + "]";
					break;
			}
			return result;
		}

		private static string BuildRuleFromConcatenatedData(string op, XElement element, ref string variableTop)
		{
			string data = GetTextData(element);
			StringBuilder rule = new StringBuilder(20*data.Length);
			for (int i = 0; i < data.Length; i++)
			{
				bool escapeNeedsClosing = false;
				string icuData = EscapeForIcu(Char.ConvertToUtf32(data, i), ref escapeNeedsClosing);
				if (escapeNeedsClosing)
					icuData += ('\'');
				rule.AppendFormat(" {0} {1}{2}", op, icuData, GetVariableTopString(icuData, ref variableTop));
				if (Char.IsSurrogate(data, i))
				{
					i++;
				}
			}
			return rule.ToString();
		}

		private static string GetVariableTopString(string icuData, ref string variableTop)
		{
			if (variableTop == null || variableTop != icuData)
			{
				return string.Empty;
			}
			variableTop = null;
			return " < [variable top]";
		}

		private static string GetRuleFromExtendedNode(XElement element)
		{
			string rule = string.Empty;
			if (element.IsEmpty)
			{
				return rule;
			}
			foreach(XElement child in element.Elements())
			{
				switch (child.Name.ToString())
				{
					case "context":
						rule += String.Format("{0} | ", GetIcuData(child));
						break;
					case "extend":
						rule += String.Format(" / {0}", GetIcuData(child));
						break;
					case "p":
						rule = String.Format(" < {0}{1}", rule, GetIcuData(child));
						break;
					case "s":
						rule = String.Format(" << {0}{1}", rule, GetIcuData(child));
						break;
					case "t":
						rule = String.Format(" <<< {0}{1}", rule, GetIcuData(child));
						break;
					case "i":
						rule = String.Format(" = {0}{1}", rule, GetIcuData(child));
						break;
					default:
						throw new ApplicationException(String.Format("Invalid node in extended LDML collation rule: {0}", child.Name));
				}
			}
			return rule;
		}
	}
}