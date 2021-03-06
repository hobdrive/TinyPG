﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using TinyPG;
using TinyPG.Compiler;

namespace TinyPG.CodeGenerators.Java
{
	public class ParserGenerator : BaseGenerator, ICodeGenerator
	{
		internal ParserGenerator() : base("Parser.java")
		{
		}

		public string Generate(Grammar Grammar, bool Debug)
		{
			if (string.IsNullOrEmpty(Grammar.GetTemplatePath()))
				return null;

			// generate the parser file
			StringBuilder parsers = new StringBuilder();
			string parser = File.ReadAllText(Grammar.GetTemplatePath() + templateName);

			// build non terminal tokens
			foreach (NonTerminalSymbol s in Grammar.GetNonTerminals())
			{
				string method = GenerateParseMethod(s);
				parsers.Append(method);
			}

			parser = parser.Replace(@"<%SourceFilename%>", Grammar.SourceFilename);
			if (Debug)
			{
				parser = parser.Replace(@"<%Namespace%>", "TinyPG.Debug");
				parser = parser.Replace(@"<%IParser%>", " : TinyPG.Debug.IParser");
				parser = parser.Replace(@"<%IParseTree%>", "TinyPG.Debug.IParseTree");
				parser = parser.Replace(@"<%ParserCustomCode%>", Grammar.Directives["Parser"]["CustomCode"]);
			}
			else
			{
				parser = parser.Replace(@"<%Namespace%>", Grammar.Directives["TinyPG"]["Namespace"]);
				parser = parser.Replace(@"<%IParser%>", "");
				parser = parser.Replace(@"<%IParseTree%>", "ParseTree");
				parser = parser.Replace(@"<%ParserCustomCode%>", Grammar.Directives["Parser"]["CustomCode"]);
			}

			parser = parser.Replace(@"<%ParseNonTerminals%>", parsers.ToString());
			return parser;
		}

		// generates the method header and body
		private string GenerateParseMethod(NonTerminalSymbol s)
		{

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("        private void Parse" + s.Name + "(ParseNode parent)" + Helper.AddComment("NonTerminalSymbol: " + s.Name));
			sb.AppendLine("        {");
			sb.AppendLine("            Token tok;");
			sb.AppendLine("            ParseNode n;");
			sb.AppendLine("            ParseNode node = parent.CreateNode(scanner.GetToken(TokenType." + s.Name + "), \"" + s.Name + "\");");
			sb.AppendLine("            parent.getNodes().add(node);");
			sb.AppendLine("");

			foreach (Rule rule in s.Rules)
			{
				sb.AppendLine(GenerateProductionRuleCode(s.Rules[0], 3));
			}

			sb.AppendLine("            parent.Token.UpdateRange(node.Token);");
			sb.AppendLine("        }" + Helper.AddComment("NonTerminalSymbol: " + s.Name));
			sb.AppendLine();
			return sb.ToString();
		}

		// generates the rule logic inside the method body
		private string GenerateProductionRuleCode(Rule r, int indent)
		{
			int i = 0;
			Symbols firsts = null;
			StringBuilder sb = new StringBuilder();
			string Indent = IndentTabs(indent);

			switch (r.Type)
			{
				case RuleType.Terminal:
					// expecting terminal, so scan it.
					sb.AppendLine(Indent + "tok = scanner.Scan(TokenType." + r.Symbol.Name + ");" + Helper.AddComment("Terminal Rule: " + r.Symbol.Name));
					sb.AppendLine(Indent + "n = node.CreateNode(tok, tok.toString() );");
					sb.AppendLine(Indent + "node.Token.UpdateRange(tok);");
					sb.AppendLine(Indent + "node.getNodes().add(n);");
					sb.AppendLine(Indent + "if (tok.Type != TokenType." + r.Symbol.Name + ") {");
					sb.AppendLine(Indent + "    tree.Errors.add(new ParseError(\"Unexpected token '\" + tok.getText().replace(\"\\n\", \"\") + \"' found. Expected \" + TokenType." + r.Symbol.Name + ".toString(), 0x1001, tok));");
					sb.AppendLine(Indent + "    return;");
					sb.AppendLine(Indent + "}");
					break;
				case RuleType.NonTerminal:
					sb.AppendLine(Indent + "Parse" + r.Symbol.Name + "(node);" + Helper.AddComment("NonTerminal Rule: " + r.Symbol.Name));
					break;
				case RuleType.Concat:
					foreach (Rule rule in r.Rules)
					{
						sb.AppendLine();
						sb.AppendLine(Indent + Helper.AddComment("Concat Rule"));
						sb.Append(GenerateProductionRuleCode(rule, indent));
					}
					break;
				case RuleType.ZeroOrMore:
					firsts = r.GetFirstTerminals();
					i = 0;
					sb.Append(Indent + "tok = scanner.LookAhead(");
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append("TokenType." + s.Name);
						else
							sb.Append(", TokenType." + s.Name);
						i++;
					}
					sb.AppendLine(");" + Helper.AddComment("ZeroOrMore Rule"));

					i = 0;
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append(Indent + "while (tok.Type == TokenType." + s.Name);
						else
							sb.Append("\r\n" + Indent + "    || tok.Type == TokenType." + s.Name);
						i++;
					}
					sb.AppendLine(")");
					sb.AppendLine(Indent + "{");

					foreach (Rule rule in r.Rules)
					{
						sb.Append(GenerateProductionRuleCode(rule, indent + 1));
					}

					i = 0;
					sb.Append(Indent + "tok = scanner.LookAhead(");
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append("TokenType." + s.Name);
						else
							sb.Append(", TokenType." + s.Name);
						i++;
					}
					sb.AppendLine(");" + Helper.AddComment("ZeroOrMore Rule"));
					sb.AppendLine(Indent + "}");
					break;
				case RuleType.OneOrMore:
					sb.AppendLine(Indent + "do {" + Helper.AddComment("OneOrMore Rule"));

					foreach (Rule rule in r.Rules)
					{
						sb.Append(GenerateProductionRuleCode(rule, indent + 1));
					}

					i = 0;
					firsts = r.GetFirstTerminals();
					sb.Append(Indent + "    tok = scanner.LookAhead(");
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append("TokenType." + s.Name);
						else
							sb.Append(", TokenType." + s.Name);
						i++;
					}
					sb.AppendLine(");" + Helper.AddComment("OneOrMore Rule"));

					i = 0;
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append(Indent + "} while (tok.Type == TokenType." + s.Name);
						else
							sb.Append("\r\n" + Indent + "    || tok.Type == TokenType." + s.Name);
						i++;
					}
					sb.AppendLine(");" + Helper.AddComment("OneOrMore Rule"));
					break;
				case RuleType.Option:
					i = 0;
					firsts = r.GetFirstTerminals();
					sb.Append(Indent + "tok = scanner.LookAhead(");
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append("TokenType." + s.Name);
						else
							sb.Append(", TokenType." + s.Name);
						i++;
					}
					sb.AppendLine(");" + Helper.AddComment("Option Rule"));

					i = 0;
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append(Indent + "if (tok.Type == TokenType." + s.Name);
						else
							sb.Append("\r\n" + Indent + "    || tok.Type == TokenType." + s.Name);
						i++;
					}
					sb.AppendLine(")");
					sb.AppendLine(Indent + "{");

					foreach (Rule rule in r.Rules)
					{
						sb.Append(GenerateProductionRuleCode(rule, indent + 1));
					}
					sb.AppendLine(Indent + "}");
					break;
				case RuleType.Choice:
					i = 0;
					firsts = r.GetFirstTerminals();
					sb.Append(Indent + "tok = scanner.LookAhead(");
					var tokens = new List<string>();
					foreach (TerminalSymbol s in firsts)
					{
						if (i == 0)
							sb.Append("TokenType." + s.Name);
						else
							sb.Append(", TokenType." + s.Name);
						i++;
						tokens.Add(s.Name);
					}
					string expectedTokens;
					if (tokens.Count == 1)
						expectedTokens = tokens[0];
					else if (tokens.Count == 2)
						expectedTokens = tokens[0] + " or " + tokens[1];
					else
					{
						expectedTokens = string.Join(", ", tokens.GetRange(0, tokens.Count - 1).ToArray());
						expectedTokens += ", or " + tokens[tokens.Count - 1];
					}
					sb.AppendLine(");" + Helper.AddComment("Choice Rule"));

					sb.AppendLine(Indent + "switch (tok.Type)");
					sb.AppendLine(Indent + "{" + Helper.AddComment("Choice Rule"));
					foreach (Rule rule in r.Rules)
					{
						foreach (TerminalSymbol s in rule.GetFirstTerminals())
						{
							sb.AppendLine(Indent + "    case " + s.Name + ":");
						}
						sb.Append(GenerateProductionRuleCode(rule, indent + 2));
						sb.AppendLine(Indent + "        break;");
					}
					sb.AppendLine(Indent + "    default:");
					sb.AppendLine(Indent + "        tree.Errors.add(new ParseError(\"Unexpected token '\" + tok.getText().replace(\"\\n\", \"\") + \"' found. Expected " + expectedTokens + ".\", 0x0002, tok));");
					sb.AppendLine(Indent + "        break;");
					sb.AppendLine(Indent + "}" + Helper.AddComment("Choice Rule"));
					break;
				default:
					break;
			}
			return sb.ToString();
		}

		// replaces tabs by spaces, so outlining is more consistent
		public static string IndentTabs(int indent)
		{
			string t = "";
			for (int i = 0; i < indent; i++)
				t += "    ";

			return t;
		}
	}
}
