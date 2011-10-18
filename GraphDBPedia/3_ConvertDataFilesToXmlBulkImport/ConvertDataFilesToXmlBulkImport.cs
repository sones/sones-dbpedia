/*
* sones GraphDB - Community Edition - http://www.sones.com
* Copyright (C) 2007-2011 sones GmbH
*
* This file is part of sones GraphDB Community Edition.
*
* sones GraphDB is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as published by
* the Free Software Foundation, version 3 of the License.
*
* sones GraphDB is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with sones GraphDB. If not, see <http://www.gnu.org/licenses/>.
*
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using de.sones.solutions.lib.owl.data;
using de.sones.solutions.lib.xml;
using de.sones.solutions.lib.owl;
using System.Globalization;
using de.sones.solutions.lib.rdf;

namespace sones.solutions.dbpedia.import
{
    class ConvertDataFilesToXmlBulkImport
    {
        static void Main(string[] args)
        {
            DateTime now = DateTime.Now;
            StreamWriter sw = new StreamWriter(now.Year.ToString() + now.Month.ToString() + now.Day.ToString() + now.Hour.ToString() + now.Minute.ToString() + now.Second.ToString() + ".log");
            while (true)
            {
                #region define logging actions
                Action<string> LogDevNull = new Action<string>((msg) => { });
                Action<string> LogError = new Action<string>((msg) =>
                {
                    Console.WriteLine(DateTime.Now + " " + msg);
                });
                Action<string> LogMessage = new Action<string>((msg) =>
                {
                    sw.WriteLine(DateTime.Now + " " + msg);
                    sw.Flush();
                    Console.WriteLine(DateTime.Now + " " + msg);
                });
                #endregion

                #region check file existence and load ontology
                Ontology thisOntology = null;
                try
                {
                    string ontologyFilename = "dbpedia_3.6.owl";
                    de.sones.solutions.lib.xml.XmlElement rootElement = XmlParser.loadXml(new StreamReader(ontologyFilename), LogError);
                    if (rootElement == null)
                    {
                        LogError("Null or empty XmlStructure in file: '" + ontologyFilename + "'");
                        break;
                    }
                    thisOntology = OwlParser.CreateOntologyFromXml(rootElement, LogDevNull);

                    #region add Thing manually!
                    OClass thing = new OClass("http://www.w3.org/2002/07/owl#Thing");

                    OProperty oPropertyName = new OProperty();
                    oPropertyName.ID = "Name";
                    oPropertyName.Range = "http://www.w3.org/2001/XMLSchema#string";
                    thing.AddDatatype(oPropertyName);

                    OProperty oPropertyLongAbstract = new OProperty();
                    oPropertyLongAbstract.ID = "LongAbstract";
                    oPropertyLongAbstract.Range = "http://www.w3.org/2001/XMLSchema#string";
                    thing.AddDatatype(oPropertyLongAbstract);

                    OProperty oPropertyShortAbstract = new OProperty();
                    oPropertyShortAbstract.ID = "ShortAbstract";
                    oPropertyShortAbstract.Range = "http://www.w3.org/2001/XMLSchema#string";
                    thing.AddDatatype(oPropertyShortAbstract);

                    thisOntology.AddOntologyClass(thing);
                    #endregion
                }
                catch (Exception a)
                {
                    LogError("Error parsing ontology!");
                    LogError(a.Message);
                    LogError(a.StackTrace);

                    break;
                }
                #endregion

                #region ask for langs to import --> de, en
                List<String> lLangs = new List<string>();
                Console.WriteLine("Which language data should be added to the database?");
                Console.WriteLine("Enter language id and press <Enter>");
                Console.WriteLine("An empty line quits the iteration process.");
                String currentLine = null;
                bool bFirst = true;
                while ((currentLine = Console.ReadLine().Trim()) != "")
                {
                    lLangs.Add(currentLine);

                    bFirst = true;
                    Console.Write("CurrentlySelected: ");
                    foreach (String lang in lLangs)
                    {
                        if (bFirst)
                            bFirst = false;
                        else Console.Write(", ");
                        Console.Write(lang);
                    }
                    Console.WriteLine();
                }
                #endregion

                #region load dictExistingNodes
                Dictionary<string, List<string>> dictExistingNodes = new Dictionary<string, List<string>>();
                try
                {
                    StreamReader sr = new StreamReader("ExistingNodes.dict");
                    String line = null;
                    String[] strToken = null;
                    char[] cToken = { ';' };
                    List<string> lData = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        strToken = line.Split(cToken, StringSplitOptions.RemoveEmptyEntries);
                        if (strToken.Length != 4)
                        {
                            LogError("ERROR: load dictExistingNodes: reading line: " + line);
                            continue;
                        }
                        else
                        {
                            lData = new List<string>();
                            lData.Add(strToken[1]);
                            lData.Add(strToken[2]);
                            lData.Add(strToken[3]);
                            dictExistingNodes.Add(strToken[0], lData);
                        }
                    }
                }
                catch (Exception a)
                {
                    LogError("Error load dictExistingNodes");
                    LogError(a.Message);
                    LogError(a.StackTrace);
                    break;
                }
                LogMessage("dictExistingNodes.Count=" + dictExistingNodes.Count);
                #endregion

                #region write data to XmlBulkImport format
                try
                {
                    #region local vars
                    String[] token;
                    String Key, Type, Subject, Filename;
                    List<string> Value = null;
                    // long VertexID;
                    char[] sepChars = { '=' };
                    ODataItem dataItem = null;
                    Dictionary<string, List<string>> dictNodeAttributes;
                    uint fileCounter = 0;
                    uint lineCount = 0;
                    #endregion

                    #region begin writing xml structure (header)
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.Indent = true;
                    XmlWriter writer = XmlWriter.Create(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.XmlFile, settings);

                    writer.WriteStartDocument();  // <?xml version="1.0" encoding="UTF-8"?> ???

                    // <BulkImport xmlns="http://schema.sones.com/graphds/xmlbulkimport.xsd">
                    writer.WriteStartElement("BulkImport", "http://schema.sones.com/graphds/xmlbulkimport.xsd");

                    writer.WriteStartElement("Import");   // <Import>
                    #endregion

                    #region process all data files
                    LogMessage("begin 'process all data files'");
                    foreach (string dirname in Directory.EnumerateDirectories(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir))
                    {
                        foreach (string filename in Directory.EnumerateFiles(dirname))
                        {
                            Subject = filename.Substring(filename.LastIndexOf(Path.DirectorySeparatorChar) + 1, (filename.Length - filename.LastIndexOf(Path.DirectorySeparatorChar) - 1));

                            #region transfer all atributes from filestream to dictionary (for better handling)
                            dictNodeAttributes = new Dictionary<string, List<string>>();
                            foreach (string line in File.ReadAllLines(filename))
                            {
                                token = line.Split(sepChars);
                                if (token.Length < 2)
                                {
                                    LogError("Invalid line content: " + line);
                                    continue;
                                }
                                Key = token[0];

                                Value = new List<string>();
                                for (int i = 1; i < token.Length; i++)
                                {
                                    Value.Add(token[i]);
                                }

                                if (RemoveEvilCharactersForFile(Key).Equals(Subject))
                                    Key = RemoveEvilCharactersForFile(Key);
                                if (!dictNodeAttributes.ContainsKey(Key))
                                    dictNodeAttributes.Add(Key, Value);
                                else
                                {
                                    foreach (String value in Value)
                                    {
                                        dictNodeAttributes[Key].Add(value);
                                    }
                                }
                            }
                            #endregion

                            #region check data validity before starting evaluation
                            dataItem = null;
                            if (!dictNodeAttributes.ContainsKey(Subject) || dictNodeAttributes[Subject] == null)
                            {
                                LogError("Data file contains no Type of the Instance!" + filename); // Subject);
                                continue;
                            }
                            #endregion

                            #region process data
                            else
                            {
                                #region write <Insert VertexType="..." VertexId="...">
                                // <Insert VertexType="User" VertexID="-9223372036854775808">
                                Type = dictNodeAttributes[Subject][0];
                                writer.WriteStartElement("Insert");
                                writer.WriteAttributeString("VertexType", GqlStringExtension.RemoveEvilCharacters(Type));
                                writer.WriteAttributeString("VertexID", dictNodeAttributes["VertexID"][0]); // VertexID.ToString());
                                #endregion

                                #region write all other attributes
                                foreach (String key in dictNodeAttributes.Keys)
                                {
                                    if (!key.Equals(Subject) && !key.Equals("VertexID"))
                                    {
                                        #region get attribute's data-type information from Ontology
                                        OClass oClass = thisOntology.GetOClass(Type);
                                        string searchkey = key;
                                        if (key.LastIndexOf('_') > 0)
                                        {
                                            searchkey = key.Substring(0, key.LastIndexOf('_'));
                                        }
                                        OProperty oProp = oClass.GetProperty(searchkey);
                                        #endregion

                                        #region <SetValue ..> for all attributes
                                        if (oProp == null || oProp.Range == null || !oProp.Range.StartsWith("http://dbpedia.org/ontology/"))  // undefined attribute
                                        {
                                            // <SetValue Key="Name" Value="SGFydHdpZw=="/>

                                            #region write undefined attribute
                                            if (oProp == null || oProp.Range == null)
                                            {
                                                StringBuilder sbValue = new StringBuilder();
                                                foreach (String value in dictNodeAttributes[key])
                                                {
                                                    sbValue.Append(value);
                                                }

                                                writer.WriteStartElement("SetValue");
                                                writer.WriteAttributeString("AttributeName", GqlStringExtension.RemoveEvilCharacters(key));
                                                writer.WriteAttributeString("Value", Convert.ToBase64String(Encoding.Default.GetBytes(sbValue.ToString())));
                                                writer.WriteEndElement();  // </SetValue>
                                            }
                                            #endregion
                                            #region write ontology attribute (includes attribute data-type handling)
                                            else
                                            {
                                                String value = null;
                                                try
                                                {
                                                    switch (oProp.Range)
                                                    {
                                                        #region W3.org number data types
                                                        case "http://www.w3.org/2001/XMLSchema#integer":
                                                        case "http://www.w3.org/2001/XMLSchema#nonNegativeInteger":
                                                        case "http://www.w3.org/2001/XMLSchema#positiveInteger":
                                                        case "http://www.w3.org/2001/XMLSchema#gYear":
                                                            {
                                                                value = Convert.ToBase64String(Encoding.Default.GetBytes(Convert.ToInt64(dictNodeAttributes[key][0]).ToString(new CultureInfo("en-US"))));//.ToString()).ToString()));
                                                                break;
                                                            }
                                                        case "http://www.w3.org/2001/XMLSchema#double":
                                                        case "http://www.w3.org/2001/XMLSchema#float":
                                                            {
                                                                value = Convert.ToBase64String(Encoding.Default.GetBytes(Convert.ToDouble(dictNodeAttributes[key][0]).ToString(new CultureInfo("en-US"))));
                                                                break;
                                                            }
                                                        case "http://www.w3.org/2001/XMLSchema#boolean":
                                                            {
                                                                if (dictNodeAttributes[key][0].ToString().Equals("True") || dictNodeAttributes[key][0].ToString().Equals("1"))
                                                                    value = Convert.ToBase64String(Encoding.Default.GetBytes("True"));
                                                                else
                                                                    value = Convert.ToBase64String(Encoding.Default.GetBytes("False"));
                                                                break;
                                                            }

                                                        #endregion
                                                        #region dbpedia
                                                        case "http://dbpedia.org/datatype/squareKilometre":
                                                        case "http://dbpedia.org/datatype/squareMetre":
                                                        case "http://dbpedia.org/datatype/kilometrePerSecond":
                                                        case "http://dbpedia.org/datatype/kilometrePerHour":
                                                        case "http://dbpedia.org/datatype/kilogramPerCubicMetre":
                                                        case "http://dbpedia.org/datatype/gramPerKilometre":
                                                        case "http://dbpedia.org/datatype/day":
                                                        case "http://dbpedia.org/datatype/hour":
                                                        case "http://dbpedia.org/datatype/minute":
                                                        case "http://dbpedia.org/datatype/second":
                                                        case "http://dbpedia.org/datatype/cubicMetre":
                                                        case "http://dbpedia.org/datatype/cubicKilometre":
                                                        case "http://dbpedia.org/datatype/cubicCentimetre":
                                                        case "http://dbpedia.org/datatype/kilometre":
                                                        case "http://dbpedia.org/datatype/metre":
                                                        case "http://dbpedia.org/datatype/centimetre":
                                                        case "http://dbpedia.org/datatype/millimetre":
                                                        case "http://dbpedia.org/datatype/inhabitantsPerSquareKilometre":
                                                        case "http://dbpedia.org/datatype/kelvin":
                                                        case "http://dbpedia.org/datatype/cubicMetrePerSecond":
                                                        case "http://dbpedia.org/datatype/kilogram":
                                                        case "http://dbpedia.org/datatype/megabyte":
                                                        case "http://dbpedia.org/datatype/litre":
                                                        case "http://dbpedia.org/datatype/engineConfiguration":
                                                        case "http://dbpedia.org/datatype/kilowatt":
                                                        case "http://dbpedia.org/datatype/newtonMetre":
                                                        #endregion
                                                        #region currencies
                                                        case "http://dbpedia.org/datatype/usDollar":
                                                        case "http://dbpedia.org/datatype/euro":
                                                        case "http://dbpedia.org/datatype/bermudianDollar":
                                                        case "http://dbpedia.org/datatype/nicaraguanCórdoba":
                                                        case "http://dbpedia.org/datatype/poundSterling":
                                                        case "http://dbpedia.org/datatype/japaneseYen":
                                                        case "http://dbpedia.org/datatype/swedishKrona":
                                                        case "http://dbpedia.org/datatype/canadianDollar":
                                                        case "http://dbpedia.org/datatype/liberianDollar":
                                                        case "http://dbpedia.org/datatype/norwegianKrone":
                                                        case "http://dbpedia.org/datatype/namibianDollar":
                                                        case "http://dbpedia.org/datatype/ukrainianHryvnia":
                                                        case "http://dbpedia.org/datatype/czechKoruna":
                                                        case "http://dbpedia.org/datatype/swissFranc":
                                                        case "http://dbpedia.org/datatype/malaysianRinggit":
                                                        case "http://dbpedia.org/datatype/newZealandDollar":
                                                        case "http://dbpedia.org/datatype/danishKrone":
                                                        case "http://dbpedia.org/datatype/philippinePeso":
                                                        case "http://dbpedia.org/datatype/southKoreanWon":
                                                        case "http://dbpedia.org/datatype/hongKongDollar":
                                                        case "http://dbpedia.org/datatype/australianDollar":
                                                        case "http://dbpedia.org/datatype/indianRupee":
                                                        case "http://dbpedia.org/datatype/russianRouble":
                                                        case "http://dbpedia.org/datatype/singaporeDollar":
                                                        case "http://dbpedia.org/datatype/icelandKrona":
                                                        case "http://dbpedia.org/datatype/bosniaAndHerzegovinaConvertibleMarks":
                                                        case "http://dbpedia.org/datatype/polishZłoty":
                                                        case "http://dbpedia.org/datatype/latvianLats":
                                                        case "http://dbpedia.org/datatype/croatianKuna":
                                                        case "http://dbpedia.org/datatype/iranianRial":
                                                        case "http://dbpedia.org/datatype/egyptianPound":
                                                        case "http://dbpedia.org/datatype/lithuanianLitas":
                                                        case "http://dbpedia.org/datatype/pakistaniRupee":
                                                        case "http://dbpedia.org/datatype/bhutaneseNgultrum":
                                                        case "http://dbpedia.org/datatype/romanianNewLeu":
                                                        case "http://dbpedia.org/datatype/bangladeshiTaka":
                                                        case "http://dbpedia.org/datatype/nigerianNaira":
                                                        case "http://dbpedia.org/datatype/saudiRiyal":
                                                        case "http://dbpedia.org/datatype/brazilianReal":
                                                        case "http://dbpedia.org/datatype/turkishLira":
                                                        case "http://dbpedia.org/datatype/kazakhstaniTenge":
                                                        case "http://dbpedia.org/datatype/unitedArabEmiratesDirham":
                                                        case "http://dbpedia.org/datatype/mexicanPeso":
                                                        case "http://dbpedia.org/datatype/newTaiwanDollar":
                                                        case "http://dbpedia.org/datatype/hungarianForint":
                                                        case "http://dbpedia.org/datatype/falklandIslandsPound":
                                                        case "http://dbpedia.org/datatype/belizeDollar":
                                                        case "http://dbpedia.org/datatype/chileanPeso":
                                                        case "http://dbpedia.org/datatype/renminbi":
                                                        case "http://dbpedia.org/datatype/thaiBaht":
                                                        case "http://dbpedia.org/datatype/papuaNewGuineanKina":
                                                        case "http://dbpedia.org/datatype/kuwaitiDinar":
                                                        case "http://dbpedia.org/datatype/israeliNewSheqel":
                                                        case "http://dbpedia.org/datatype/sriLankanRupee":
                                                        case "http://dbpedia.org/datatype/peruvianNuevoSol":
                                                        case "http://dbpedia.org/datatype/estonianKroon":
                                                        case "http://dbpedia.org/datatype/southAfricanRand":
                                                        case "http://dbpedia.org/datatype/argentinePeso":
                                                        case "http://dbpedia.org/datatype/jamaicanDollar":
                                                        case "http://dbpedia.org/datatype/qatariRial":
                                                        #endregion
                                                            {
                                                                value = Convert.ToBase64String(Encoding.Default.GetBytes(Convert.ToDouble(dictNodeAttributes[key][0]).ToString(new CultureInfo("en-US"))));
                                                                break;
                                                            }
                                                        #region date
                                                        case "http://www.w3.org/2001/XMLSchema#date":
                                                            {
                                                                value = Convert.ToBase64String(Encoding.Default.GetBytes(Convert.ToDateTime(dictNodeAttributes[key][0]).ToString(new CultureInfo("en-US"))));
                                                                break;
                                                            }
                                                        case "http://www.w3.org/2001/XMLSchema#string":
                                                        case "http://www.w3.org/2001/XMLSchema#anyURI":
                                                        #endregion
                                                        #region workaround simple types
                                                        case "http://dbpedia.org/datatype/valvetrain":
                                                        case "http://dbpedia.org/datatype/fuelType":
                                                        #endregion
                                                            {
                                                                StringBuilder sbValue = new StringBuilder();
                                                                foreach (String strValue in dictNodeAttributes[key])
                                                                {
                                                                    sbValue.Append(strValue);
                                                                }

                                                                value = Convert.ToBase64String(Encoding.Default.GetBytes(sbValue.ToString()));
                                                                break;
                                                            }
                                                        default:
                                                            {
                                                                break;
                                                            }
                                                    }
                                                }
                                                catch (Exception a)
                                                {
                                                    StringBuilder sbValue = new StringBuilder();
                                                    foreach (String strValue in dictNodeAttributes[key])
                                                    {
                                                        sbValue.Append(strValue);
                                                    }

                                                    LogError("Invalid data content!: " + sbValue);
                                                    value = null;
                                                }
                                                if (value != null)
                                                {
                                                    writer.WriteStartElement("SetValue");
                                                    writer.WriteAttributeString("AttributeName", GqlStringExtension.RemoveEvilCharacters(key));
                                                    writer.WriteAttributeString("Value", value);
                                                    writer.WriteEndElement();  // </SetValue>
                                                }
                                            }
                                            #endregion
                                        }
                                        #endregion
                                        #region <MultiLink ..> for all attributes that represent edges
                                        else
                                        {
                                            writer.WriteStartElement("MultiLink");   // <MultiLink Key="Friends">
                                            writer.WriteAttributeString("AttributeName", GqlStringExtension.RemoveEvilCharacters(key));

                                            foreach (String value in dictNodeAttributes[key])
                                            {
                                                if (dictExistingNodes.ContainsKey(value))
                                                {
                                                    writer.WriteStartElement("Link");

                                                    // <Link VertexType="User" VertexID="-9223372036854775806"/>
                                                    // writer.WriteAttributeString("VertexType",  GqlStringExtension.RemoveEvilCharacters(oProp.Range));

                                                    writer.WriteAttributeString("VertexType", GqlStringExtension.RemoveEvilCharacters(dictExistingNodes[value][2]));
                                                    writer.WriteAttributeString("VertexID", dictExistingNodes[value][1]);

                                                    writer.WriteEndElement();
                                                }
                                            }
                                            writer.WriteEndElement(); // </MultiLink>
                                        }
                                        #endregion
                                    }
                                }
                                writer.WriteEndElement();  // </Insert>
                                #endregion
                            }  // end else   ( corr. if:(!dictNodeAttributes.ContainsKey(Subject) || dictNodeAttributes[Subject].Count < 1))
                            #endregion

                            #region some debug/processing info
                            fileCounter++;
                            if (fileCounter % 10000 == 0)
                            {
                                LogMessage("processed " + fileCounter + " items.");
                            }
                            #endregion
                        }  // string filename in Directory.EnumerateFiles(dirname)
                    }  // foreach (string dirname in Directory.EnumerateDirectories(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir))
                    LogMessage("end 'process all data files'");
                    #endregion

                    #region evaluate labels
                    LogMessage("begin 'evaluate labels'");
                    Dictionary<string, long> dictLabelIds = new Dictionary<string, long>();
                    Dictionary<string, Dictionary<string, List<long>>> dictInstanceLabels = new Dictionary<string, Dictionary<string, List<long>>>();
                    long currentLabelVertexID = long.MinValue;

                    foreach (String lang in lLangs)
                    {
                        if (Directory.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang))
                        {
                            Filename = "labels_" + lang + ".nt";
                            if (!File.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang + Path.DirectorySeparatorChar + Filename))
                            {
                                continue;
                            }
                            uint insertedLabels = 0;
                            LogMessage("begin read labels from " + Filename);

                            using (StreamReader srNodes = new StreamReader(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang + Path.DirectorySeparatorChar + Filename))
                            {
                                #region if input stream is empty --> do error handling
                                if (srNodes == null)
                                {
                                    LogError("Error reading Nodes file: '" + Filename + "'");
                                    return;
                                }
                                #endregion

                                #region init local vars
                                String strCurrentTriple;
                                Triple currentTriple;
                                #endregion

                                #region for each triple (line)
                                while ((strCurrentTriple = srNodes.ReadLine()) != null)
                                {
                                    currentTriple = NTripleParser.Split(strCurrentTriple, LogError);

                                    #region some debug info
                                    if (lineCount % 1000 == 0)
                                    {
                                        Console.Write(".");
                                    }
                                    if (lineCount % 100000 == 0)
                                    {
                                        LogMessage("EvaluateLabels('" + lang + "'): lineCount=" + lineCount);
                                        GC.Collect();
                                        GC.Collect();
                                        GC.WaitForFullGCComplete();
                                    }
                                    lineCount++;
                                    #endregion

                                    if (currentTriple == null || currentTriple.TripleObject == null)
                                        continue;

                                    if (!dictExistingNodes.ContainsKey(currentTriple.Subject))
                                    {
                                        continue;
                                    }

                                    if (!dictLabelIds.ContainsKey(currentTriple.TripleObject))
                                    {
                                        dictLabelIds.Add(currentTriple.TripleObject, currentLabelVertexID);
                                        currentLabelVertexID++;

                                        // <Insert VertexType="Label" VertexID="-9223372036854775808">
                                        writer.WriteStartElement("Insert");
                                        writer.WriteAttributeString("VertexType", "Label");
                                        writer.WriteAttributeString("VertexID", dictLabelIds[currentTriple.TripleObject].ToString()); // VertexID.ToString());

                                        // <SetValue AttributeName="Name" Value="SGFydHdpZw=="/>
                                        writer.WriteStartElement("SetValue");
                                        writer.WriteAttributeString("AttributeName", "Name");
                                        writer.WriteAttributeString("Value", Convert.ToBase64String(Encoding.Default.GetBytes(currentTriple.TripleObject)));
                                        writer.WriteEndElement(); // </SetValue>
                                        writer.WriteEndElement();  // </Insert>

                                        insertedLabels++;
                                    }

                                    if (!dictInstanceLabels.ContainsKey(currentTriple.Subject))
                                    {
                                        dictInstanceLabels.Add(currentTriple.Subject, new Dictionary<string, List<long>>());
                                    }

                                    if (!dictInstanceLabels[currentTriple.Subject].ContainsKey(lang))
                                    {
                                        dictInstanceLabels[currentTriple.Subject].Add(lang, new List<long>());
                                    }
                                    dictInstanceLabels[currentTriple.Subject][lang].Add(currentLabelVertexID - 1);
                                }  // while ((strCurrentTriple = srNodes.ReadLine()) != null)
                                #endregion
                            }  // using (StreamReader srNodes = new StreamReader(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang + Path.DirectorySeparatorChar + Filename))
                            LogMessage("end read labels from " + Filename + " inserted " + insertedLabels + "entries");
                        }  // if (Directory.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang))
                    }  // foreach (String lang in lLangs)
                    LogMessage("end 'evaluate labels'");
                    #endregion

                    #region create all instance entries in dictionary
                    LogMessage("begin 'create all instance entries in dictionary'");
                    Dictionary<string, List<KeyValuePair<long, string>>> dictInstances = new Dictionary<string, List<KeyValuePair<long, string>>>();
                    Dictionary<string, long> dictInstanceVertexIDs = new Dictionary<string, long>();
                    long lInstanceVertexID_count = long.MinValue;
                    foreach (String instance in dictExistingNodes.Keys)
                    {
                        List<KeyValuePair<long, string>> lNodeIds = new List<KeyValuePair<long, string>>();
                        lNodeIds.Add(new KeyValuePair<long, string>(Int64.Parse(dictExistingNodes[instance][1]), dictExistingNodes[instance][2]));
                        dictInstances.Add(instance, lNodeIds);
                        dictInstanceVertexIDs.Add(instance, lInstanceVertexID_count);
                        lInstanceVertexID_count++;
                    }
                    LogMessage("end 'create all instance entries in dictionary'");
                    #endregion

                    #region evaluate disambiguations
                    LogMessage("begin evaluate disambiguations");
                    foreach (String lang in lLangs)
                    {
                        if (Directory.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang))
                        {
                            Filename = "disambiguations_" + lang + ".nt";
                            if (!File.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang + Path.DirectorySeparatorChar + Filename))
                            {
                                continue;
                            }

                            using (StreamReader srNodes = new StreamReader(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang + Path.DirectorySeparatorChar + Filename))
                            {
                                #region if input stream is empty --> do error handling
                                if (srNodes == null)
                                {
                                    LogError("Error reading Nodes file: '" + Filename + "'");
                                    return;
                                }
                                #endregion

                                #region init local vars
                                String strCurrentTriple;
                                Triple currentTriple;
                                #endregion

                                #region for each triple (line), add or append dictInstances dictionary
                                while ((strCurrentTriple = srNodes.ReadLine()) != null)
                                {
                                    currentTriple = NTripleParser.Split(strCurrentTriple, LogError);

                                    if (dictExistingNodes.ContainsKey(currentTriple.Subject))
                                    {
                                        Console.WriteLine("should not happen currentTriple.Subject='" + currentTriple.Subject + "'");
                                        continue;
                                    }

                                    if (!dictExistingNodes.ContainsKey(currentTriple.TripleObject))
                                    {
                                        continue;
                                    }
                                    if (!dictInstanceVertexIDs.ContainsKey(currentTriple.Subject))
                                    {
                                        dictInstanceVertexIDs.Add(currentTriple.Subject, lInstanceVertexID_count);
                                        lInstanceVertexID_count++;
                                    }

                                    if (!dictInstances.ContainsKey(currentTriple.Subject))
                                    {
                                        dictInstances.Add(currentTriple.Subject, new List<KeyValuePair<long, string>>());
                                    }

                                    // string vertexType = "httpwwww3org200207owlThing";
                                    if (dictExistingNodes.ContainsKey(currentTriple.TripleObject))
                                    {
                                        // vertexType = dictExistingNodes[currentTriple.TripleObject][2];
                                        dictInstances[currentTriple.Subject].Add(new KeyValuePair<long, string>(Int64.Parse(dictExistingNodes[currentTriple.TripleObject][1]), dictExistingNodes[currentTriple.TripleObject][2]));
                                    }
                                    // dictInstances[currentTriple.Subject].Add(new KeyValuePair<long, string>(dictInstanceVertexIDs[currentTriple.TripleObject], vertexType));

                                } // while ((strCurrentTriple = srNodes.ReadLine()) != null)
                                #endregion
                            }  // using (StreamReader srNodes = new StreamReader(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang + Path.DirectorySeparatorChar + Filename))
                        }  // if (Directory.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang))
                    }  // foreach (String lang in lLangs)
                    LogMessage("end evaluate disambiguations");
                    #endregion

                    #region create Instance xml bulk import format
                    LogMessage("begin create Instance xml bulk import format");
                    foreach (string currentInstance in dictInstances.Keys)
                    {
                        if (dictInstances[currentInstance].Count < 1)
                        {
                            continue;
                        }

                        writer.WriteStartElement("Insert");
                        writer.WriteAttributeString("VertexType", "Instance");
                        writer.WriteAttributeString("VertexID", dictInstanceVertexIDs[currentInstance].ToString());

                        #region add instance name
                        // <SetValue AttributeName="Name" Value="SGFydHdpZw=="/>
                        writer.WriteStartElement("SetValue");
                        writer.WriteAttributeString("AttributeName", "Name");
                        writer.WriteAttributeString("Value", Convert.ToBase64String(Encoding.Default.GetBytes(currentInstance)));
                        writer.WriteEndElement(); // </SetValue>
                        #endregion

                        #region add labels (for all langs)
                        if (dictInstanceLabels.ContainsKey(currentInstance))
                        {
                            Dictionary<string, List<long>> dictLangLabels = dictInstanceLabels[currentInstance];

                            if (dictLangLabels.Keys.Count == 0)
                                LogMessage("'dictLangLabels.Keys.Count == 0' for '" + currentInstance + "'");

                            foreach (string currentLang in dictLangLabels.Keys)
                            {
                                List<long> lLabels = dictLangLabels[currentLang];

                                if (lLabels.Count == 0)
                                    LogMessage("'lLabels.Count == 0' for currentLang='" + currentLang + "' and currentInstance='" + currentInstance + "'");
                                else
                                {
                                    writer.WriteStartElement("MultiLink");   // <MultiLink Key="Label_de">
                                    writer.WriteAttributeString("AttributeName", "Labels_" + currentLang);

                                    foreach (long currentLabelId in lLabels)
                                    {
                                        writer.WriteStartElement("Link");

                                        // <Link VertexType="User" VertexID="-9223372036854775806"/>
                                        // writer.WriteAttributeString("VertexType",  GqlStringExtension.RemoveEvilCharacters(oProp.Range));

                                        writer.WriteAttributeString("VertexType", "Label");
                                        writer.WriteAttributeString("VertexID", currentLabelId.ToString());

                                        writer.WriteEndElement();
                                    }
                                    writer.WriteEndElement(); // </MultiLink>
                                }
                            }
                        }
                        else LogMessage("!dictInstanceLabels.ContainsKey('" + currentInstance + "')");
                        #endregion

                        #region add disambiguation
                        if (dictInstances[currentInstance].Count > 0)
                        {
                            writer.WriteStartElement("MultiLink");   // <MultiLink Key="Label_de">
                            writer.WriteAttributeString("AttributeName", "RefersToEntry");

                            foreach (KeyValuePair<long, string> currentThingId in dictInstances[currentInstance])
                            {
                                writer.WriteStartElement("Link");

                                // <Link VertexType="User" VertexID="-9223372036854775806"/>
                                // writer.WriteAttributeString("VertexType",  GqlStringExtension.RemoveEvilCharacters(oProp.Range));

                                writer.WriteAttributeString("VertexType", GqlStringExtension.RemoveEvilCharacters(currentThingId.Value));
                                writer.WriteAttributeString("VertexID", currentThingId.Key.ToString());

                                writer.WriteEndElement();
                            }

                            writer.WriteEndElement(); // </MultiLink>
                        }  // if (dictInstances[currentInstance].Count > 0)
                        #endregion

                        writer.WriteEndElement(); // </Insert>
                    }  // foreach (string currentInstance in dictInstances.Keys)
                    LogMessage("end create Instance xml bulk import format");

                    #endregion

                    #region finish writing xml-structure
                    writer.WriteEndElement();  // </Import>
                    writer.WriteEndElement();  // </BulkImport>
                    writer.Flush();
                    writer.Close();
                    #endregion
                }
                catch (Exception a)
                {
                    LogError("Error reading data files.");
                    LogError(a.Message);
                    LogError(a.StackTrace);
                }
                #endregion

                break; // default behaviour
            } // while (true)
            sw.Close();

            Console.WriteLine("Press <Enter> to quit.");
            Console.ReadLine();
        }

        private static string RemoveEvilCharactersForFile(String str)
        {
            return str
                .Replace("/", "")
                .Replace(":", "")
                .Replace(".", "")
                .Replace("#", "")
                .Replace("-", "")
                .Replace("*", "");
        }
    }
}
