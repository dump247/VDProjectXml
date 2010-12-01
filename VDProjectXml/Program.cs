using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

namespace VDProjectXml
{
	class Program
	{
		/// <summary>
		/// Matches "key" [= "8:value"] with support for backslash escapes.
		/// </summary>
		private static readonly Regex ElementRegex = new Regex(@"^""([^""\\]*(?:\\.[^""\\]*)*)""(?:\s*=\s*""(\d+):([^""\\]*(?:\\.[^""\\]*)*)"")?$", RegexOptions.Singleline);

		private const string Indent = "    ";

		static void Main(string[] args)
		{
			string inputFile = args[0];
			string outputFile = args.Length > 1 ? args[1] : null;

			switch (Path.GetExtension(inputFile).Trim().ToLowerInvariant())
			{
				case ".xml":
					ConvertXmlVDProj(inputFile, outputFile ?? Path.ChangeExtension(inputFile, ".vdproj"));
					break;

				case ".vdproj":
					ConvertVDProjToXml(inputFile, outputFile ?? Path.ChangeExtension(inputFile, ".xml"));
					break;

				default:
					throw new Exception("Unknown file type: " + inputFile);
			}
		}

		static void ConvertVDProjToXml(string vdrpojFile, string xmlFile)
		{
			using (StreamReader reader = File.OpenText(vdrpojFile))
			using (XmlWriter writer = XmlWriter.Create(xmlFile))
			{
				string line;
				bool prevLineElement = false;
				string prevLine = null;

				while ((line = reader.ReadLine()) != null)
				{
					line = line.Trim();

					Match match = ElementRegex.Match(line);

					if (match.Success)
					{
						if (prevLineElement)
							writer.WriteEndElement();

						string elementName = Unescape(match.Groups[1].Value);
						string fixedElementName = XmlConvert.EncodeLocalName(elementName);

						writer.WriteStartElement(fixedElementName);

						if (match.Groups[2].Success)
						{
							writer.WriteAttributeString("valueType", match.Groups[2].Value);
							writer.WriteAttributeString("value", Unescape(match.Groups[3].Value));
						}

						prevLineElement = true;
					}
					else
					{
						if (prevLineElement && line != "{")
							writer.WriteEndElement();

						prevLineElement = false;

						if (line == "}")
						{
							if (prevLine == "{")
								writer.WriteWhitespace(" ");

							writer.WriteEndElement();
						}
					}

					prevLine = line;
				}
			}
		}

		static void ConvertXmlVDProj(string vdrpojFile, string xmlFile)
		{
			int indentLevel = -1;

			using (StreamWriter writer = File.CreateText(vdrpojFile))
			using (XmlReader reader = XmlReader.Create(xmlFile))
			{
				while (reader.Read())
				{
					if (reader.NodeType == XmlNodeType.Element)
					{
						if (reader.IsEmptyElement)
						{
							WriteIndent(writer, indentLevel);
							WriteElement(reader, writer);
						}
						else
						{
							WriteIndent(writer, ++indentLevel);
							WriteElement(reader, writer);
							WriteIndent(writer, indentLevel);
							writer.WriteLine("{");
						}
					}
					else if (reader.NodeType == XmlNodeType.EndElement)
					{
						WriteIndent(writer, indentLevel--);
						writer.WriteLine("}");
					}
				}
			}
		}

		private static void WriteElement(XmlReader reader, TextWriter writer)
		{
			string elementName = Escape(XmlConvert.DecodeName(reader.LocalName));

			writer.Write("\"{0}\"", elementName);

			string value = reader.GetAttribute("value");

			if (value != null)
			{
				writer.Write(" = \"{0}:{1}\"", reader.GetAttribute("valueType"), Escape(value));
			}

			writer.WriteLine();
		}

		private static void WriteIndent(TextWriter writer, int level)
		{
			for (int i = 0; i < level; ++i)
			{
				writer.Write(Indent);
			}
		}

		private static string Escape(string value)
		{
			return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		private static string Unescape(string value)
		{
			return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
		}
	}
}
