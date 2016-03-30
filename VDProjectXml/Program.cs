using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;

namespace VDProject2Xml
{
	class Program
	{
        /// <summary>
        /// Matches <c>"key" [= "8:value"]</c> with support for backslash escapes.
        /// </summary>
        private static readonly Regex ElementRegex = new Regex(@"^""([^""\\]*(?:\\.[^""\\]*)*)""(?:\s*=\s*""(\d+):([^""\\]*(?:\\.[^""\\]*)*)"")?$", RegexOptions.Singleline);

        /// <summary>
        /// Mataches <c>"value:value"</c> for keyless entries like <c>"{EDC2488A-8267-493A-A98E-7D9C3B36CDF3}:.NETFramework,Version=v4.5.2"</c> 
        /// or <c>"BootstrapperCfg:{63ACBE69-63AA-4F98-B2B6-99F9E24495F2}"</c>.
        /// </summary>
        private static readonly Regex NoKeyEntry = new Regex(@"^""([^""]*?):([^""]*)""$", RegexOptions.Singleline);

        private const string Indent = "    ";
	    private const string NoKeyEntryName = "NoKeyEntry";
	    private const string ValueTypeAttribute = "valueType";
	    private const string ValueAttribute = "value";

	    static int Main(string[] args)
		{
	        if (args.Length == 0 || args.Length > 3)
	        {
	            ShowHelp();
	            return -1;
	        }

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
			        ShowHelp();
			        return -2;
			}

	        return 0;
		}

	    private static void ShowHelp()
	    {
	        Console.WriteLine("VDProject2Xml - A Visual Studio installer to XML converter.");
            Console.WriteLine("Usage: VDProject2Xml installerProject.vdproj [installerProject.xml]");
            Console.WriteLine("       VDProject2Xml installerProject.xml [installerProject.vdproj]");
	        Console.WriteLine("If no output file path is provided the program will use the input filename with the extension changed to .xml");
            Console.WriteLine("Set the environment variable VDPROJECT2XML_INDENT to \"{0}\" to make the outputed XML use newlines and indenting.", Boolean.TrueString);
	    }

	    public static bool CheckIfShouldIndent()
	    {
	        var indentString = Environment.GetEnvironmentVariable("VDPROJECT2XML_INDENT");
	        bool result;
	        if (!Boolean.TryParse(indentString, out result))
	        {
	            result = false;
	        }
	        return result;
	    }

	    static void ConvertVDProjToXml(string vdrpojFile, string xmlFile)
	    {
            
	        XmlWriterSettings settings = new XmlWriterSettings()
	        {
	            Indent = CheckIfShouldIndent()
            };

			using (StreamReader reader = File.OpenText(vdrpojFile))
			using (XmlWriter writer = XmlWriter.Create(xmlFile, settings))
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

                        Match noKeyMatch = NoKeyEntry.Match(line);
					    if (noKeyMatch.Success)
					    {
                            writer.WriteStartElement(NoKeyEntryName);
                            writer.WriteAttributeString(ValueTypeAttribute, Unescape(noKeyMatch.Groups[1].Value));
                            writer.WriteAttributeString(ValueAttribute, Unescape(noKeyMatch.Groups[2].Value));
					    }
					    else
					    {
					        string elementName = Unescape(match.Groups[1].Value);
					        string fixedElementName = XmlConvert.EncodeLocalName(elementName);

					        writer.WriteStartElement(fixedElementName);

					        if (match.Groups[2].Success)
					        {
					            writer.WriteAttributeString(ValueTypeAttribute, match.Groups[2].Value);
					            writer.WriteAttributeString(ValueAttribute, Unescape(match.Groups[3].Value));
					        }
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

		static void ConvertXmlVDProj(string xmlFile, string vdrpojFile)
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
            string valueType = reader.GetAttribute(ValueTypeAttribute);
            string value = reader.GetAttribute(ValueAttribute);

            if (reader.LocalName == NoKeyEntryName)
		    {
		        writer.Write("\"{0}:{1}\"", Escape(valueType), Escape(value));
		    }
		    else
		    {
                string elementName = Escape(XmlConvert.DecodeName(reader.LocalName));

                writer.Write("\"{0}\"", elementName);

                if (value != null)
                {
                    writer.Write(" = \"{0}:{1}\"", valueType, Escape(value));
                }
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
