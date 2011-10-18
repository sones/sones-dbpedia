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
using de.sones.solutions.lib.owl;
using de.sones.solutions.lib.owl.data;
using de.sones.solutions.lib.rdf;
using de.sones.solutions.lib.xml;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace sones.solutions.dbpedia.import
{
    class ParseAndConvertTripleDataFiles
    {
        static void Main(string[] args)
        {
            while (true)
            {
                #region create Logging Actions
                Action<string> LogMessage = new Action<string>((msg) =>
                {
                    Console.WriteLine(DateTime.Now + " " + msg);
                });
                Action<string> LogError = new Action<string>((msg) =>
                {
                    Console.WriteLine(DateTime.Now + " " + msg);
                });

                Action<string> LogNull = new Action<string>((msg) => { });
                #endregion

                #region check file existence and load ontology
                Ontology thisOntology = null;
                try
                {
                    string ontologyFilename = "dbpedia_3.6.owl";
                    XmlElement rootElement = XmlParser.loadXml(new StreamReader(ontologyFilename), LogNull);
                    if (rootElement == null)
                    {
                        LogError("Null or empty XmlStructure in file: '" + ontologyFilename + "'");
                        break;
                    }
                    thisOntology = OwlParser.CreateOntologyFromXml(rootElement, LogNull);

                    #region add Thing manually!
                    OClass thing = new OClass("http://www.w3.org/2002/07/owl#Thing");
                    OProperty oPropertyName = new OProperty();
                    oPropertyName.ID = "Name";
                    oPropertyName.Range = "http://www.w3.org/2001/XMLSchema#string";
                    thing.AddDatatype(oPropertyName);
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

                #region verify work environment
                if (!Directory.Exists(Properties.Settings.Default.WorkDir))
                {
                    LogError("WorkDir could not be loaded! " + Properties.Settings.Default.WorkDir);
                    break;
                }
                if (!Directory.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir))
                {
                    Directory.CreateDirectory(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir);
                }
                else
                {
                    LogMessage("Data directory is already existing.!");
                    Console.WriteLine("All data will be deleted (Y), or program aborted.");
                    ConsoleKeyInfo a = Console.ReadKey();
                    if (a.KeyChar == 'y' || a.KeyChar == 'Y')
                    {
                        Console.WriteLine(" - Deleting existing directory content.");
                        Directory.Delete(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir, true);
                        Directory.CreateDirectory(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir);
                        LogMessage("Finished deleting data directory");
                    }
                    else
                    {
                        Console.WriteLine("Aborting program execution.");
                        break;
                    }
                }
                #endregion

                #region init helper dictionaries
                Dictionary<string, List<string>> dictExistingNodes = new Dictionary<string, List<string>>();
                Dictionary<string, uint> dictDirectoryEntries = new Dictionary<string, uint>();
                Dictionary<string, long> dictVertexIds = new Dictionary<string, long>();
                foreach (OClass oclass in thisOntology.GetAllClasses())
                {
                    dictVertexIds.Add(oclass.ID, long.MinValue);
                }

                #endregion

                #region create nodes from instance_types
                foreach (String lang in lLangs)
                {
                    String filename = "instance_types_" + lang + ".nt";

                    if (!File.Exists(Properties.Settings.Default.WorkDir
                        + Path.DirectorySeparatorChar
                        + lang
                        + Path.DirectorySeparatorChar
                        + filename))
                        continue;

                    CreateNodes(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar
                        + lang + Path.DirectorySeparatorChar + filename, thisOntology, dictExistingNodes, dictDirectoryEntries, dictVertexIds, LogMessage, LogError);
                }
                #endregion

                #region parse other NTripleFiles with specific implementation
                String filenameSub = null;

                foreach (String lang in lLangs)
                {
                    if (Directory.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang))
                        foreach (String filename in Directory.EnumerateFiles(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + lang))
                        {
                            // foreach filename.endsWith(lang+".nt")   geo_coordinates_de.nt
                            FileInfo fi = new FileInfo(filename);
                            if (!fi.Extension.Equals(".nt"))
                            {
                                continue;
                            }

                            filenameSub = fi.Name.Substring(0, (fi.Name.Length - fi.Extension.Length));
                            if (filenameSub.EndsWith("_" + lang))
                            {
                                filenameSub = filenameSub.Substring(0, (filenameSub.Length - lang.Length - 1));
                            }

                            switch (filenameSub)
                            {
                                case "mappingbased_properties":
                                case "specific_mappingbased_properties":
                                    {
                                        string tmp_filename = filename;
                                        string tmp_lang = lang;
                                        //actions.Add(() => 
                                        EvaluateMappingbasedProperties(tmp_filename, tmp_lang, dictExistingNodes, dictDirectoryEntries, thisOntology, LogMessage, LogError);  //);
                                        // EvaluateMappingbasedProperties(filename, lang, thisOntology);
                                        // call impl.
                                        break;
                                    }
                                case "short_abstracts":
                                    {
                                        string tmp_filename = filename;
                                        string tmp_lang = lang;
                                        string tmp_KindOfText = "ShortAbstract";
                                        // actions.Add(() => 
                                        EvaluateAttributes(tmp_filename, tmp_KindOfText, tmp_lang, dictExistingNodes, dictDirectoryEntries, LogMessage, LogError); // );
                                        // EvaluateAttributes(filename, "ShortAbstract", lang);
                                        break;
                                    }
                                case "long_abstracts":
                                    {
                                        string tmp_filename = filename;
                                        string tmp_lang = lang;
                                        string tmp_KindOfText = "LongAbstract";
                                        // actions.Add(() => 
                                        EvaluateAttributes(tmp_filename, tmp_KindOfText, tmp_lang, dictExistingNodes, dictDirectoryEntries, LogMessage, LogError); // );
                                        // EvaluateAttributes(filename, "LongAbstract", lang);
                                        break;
                                    }
                                // other impl.
                                default:
                                    {
                                        LogMessage("ignore " + filename);
                                        // do nothing
                                        break;
                                    }
                            }
                        }
                }
                // Parallel.Invoke(actions.ToArray());
                #endregion

                #region save dictionary dictExistingNodes to file - needed for step 3 for performant evaluation of data within data dir
                try
                {
                    StreamWriter tw = new StreamWriter("ExistingNodes.dict");
                    foreach (string key in dictExistingNodes.Keys)
                    {
                        List<string> values = dictExistingNodes[key];
                        if (values.Count != 3)
                        {
                            LogError("Error savind data entry: '" + key + "' in ExistingNodes.dict");
                            continue;
                        }
                        else
                        {
                            tw.WriteLine(key + ";" + values[0] + ";" + values[1] + ";" + values[2]);
                            Console.Write(".");
                        }
                    }
                    tw.Flush();
                    tw.Close();
                }
                catch (Exception a)
                {
                    LogError("Error saving dictionary dictExistingNodes to file");
                    LogError(a.Message);
                    LogError(a.StackTrace);
                }
                #endregion
                break;   // default behaviour
            }

            Console.WriteLine("Press <Enter> to quit program.");
            Console.ReadLine();
        }

        private static void CreateNodes(String filename, Ontology thisOntology, Dictionary<string, List<string>> dictExistingNodes,
            Dictionary<string, uint> dictDirectoryEntries, Dictionary<string, long> dictVertexIDs, Action<string> LogMessage, Action<string> LogError)
        {
            LogMessage("Begin CreateNodes(" + filename + ")");
            try
            {
                using (StreamReader srNodes = new StreamReader(filename))
                {
                    #region if input stream is empty --> do error handling
                    if (srNodes == null)
                    {
                        LogError("Error reading Nodes file: '" + filename + "'");
                        return;
                    }
                    #endregion

                    #region init local vars
                    int iCurrentLevel = -1;
                    int iCurrentTripleLevel = 0;
                    String strCurrentTriple;
                    Triple currentTriple = null;

                    Triple selectedTriple = null;

                    uint lineCount = 0;
                    uint instanceCount = 0;
                    #endregion

                    #region for each line
                    while ((strCurrentTriple = srNodes.ReadLine()) != null)
                    {
                        #region some debug info
                        if (lineCount % 100 == 0)
                        {
                            Console.Write(".");
                        }
                        if (lineCount % 10000 == 0)
                        {
                            LogMessage("CreateNodes: lineCount=" + lineCount + " instanceCount=" + instanceCount);
                            GC.Collect();
                            GC.Collect();
                        }
                        if (instanceCount > Properties.Settings.Default.InsertLimit)
                        {
                            LogMessage("Quit execution due to InsertLimit setting");
                            break;
                        }
                        lineCount++;
                        #endregion

                        currentTriple = NTripleParser.Split(strCurrentTriple, LogError);

                        #region some sample data for help
                        /* currentTriple.Subject               currentTriple.Predicate                           currentTriple.TripleObject
<http://dbpedia.org/resource/Autism> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/2002/07/owl#Thing> .
<http://dbpedia.org/resource/Autism> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://dbpedia.org/ontology/Disease> .
<http://dbpedia.org/resource/Alabama> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.w3.org/2002/07/owl#Thing> .
<http://dbpedia.org/resource/Alabama> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://dbpedia.org/ontology/Place> .
<http://dbpedia.org/resource/Alabama> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://dbpedia.org/ontology/PopulatedPlace> .
<http://dbpedia.org/resource/Alabama> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://dbpedia.org/ontology/AdministrativeRegion> . 
                         */
                        #endregion

                        #region new concept: per uniqe subject, only one INSERT is done, the one with the highest level within the ontology
                        if (selectedTriple == null)
                            selectedTriple = currentTriple;

                        #region execute gql statement (includes redundancy check for several triple lines regarding one class (eg. Thing, Species, Animal, Mammal --> only mammal is inserted)
                        iCurrentTripleLevel = thisOntology.GetOClassLevel(currentTriple.TripleObject);
                        if (selectedTriple.Subject.Equals(currentTriple.Subject))
                        {
                            // check level 
                            if (iCurrentLevel < iCurrentTripleLevel)
                            {
                                // replace existing with new gql command
                                selectedTriple = currentTriple;
                                iCurrentLevel = iCurrentTripleLevel;
                            } // else do nothing 
                        }
                        else
                        {
                            if (!SaveTriple(selectedTriple, dictExistingNodes, dictDirectoryEntries, dictVertexIDs, LogError))
                            {
                                break;
                            }
                            instanceCount++;

                            // reset values
                            selectedTriple = currentTriple;
                            iCurrentLevel = iCurrentTripleLevel;
                        }
                        #endregion

                        #endregion

                    }  // end while
                    #endregion

                    #region finally - save last line
                    if (selectedTriple != null)
                    {
                        SaveTriple(selectedTriple, dictExistingNodes, dictDirectoryEntries, dictVertexIDs, LogError);
                    }
                    #endregion
                }
            }
            catch (Exception e)
            {
                LogError("Error creating instance file");
                LogError(e.Message);
                LogError(e.StackTrace);
            }
            LogMessage("End CreateNodes(" + filename + ")");
        }

        private static void EvaluateMappingbasedProperties(String filename, String lang, Dictionary<string, List<string>> dictExistingNodes, Dictionary<string, uint> dictDirectoryEntries, Ontology thisOntology, Action<string> LogMessage, Action<string> LogError)
        {
            LogMessage("Begin EvaluateMappingbasedProperties(" + filename + ", " + lang + ")");
            try
            {
                using (StreamReader srNodes = new StreamReader(filename))
                {
                    #region if input stream is empty --> do error handling
                    if (srNodes == null)
                    {
                        LogError("Error reading Nodes file: '" + filename + "'");
                        return;
                    }
                    #endregion

                    #region init local vars
                    ODataItem currentDataItem = null;
                    String strCurrentTriple;
                    Triple currentTriple;
                    uint lineCount = 0;
                    uint updateCount = 0;
                    #endregion

                    #region for each triple (line)
                    while ((strCurrentTriple = srNodes.ReadLine()) != null)
                    {

                        #region some debug info
                        /*
                        if (lineCount % 1000 == 0)
                        {
                            Console.Write(".");
                        }*/
                        if (lineCount % 100000 == 0)
                        {
                            LogMessage("EvaluateMappingbasedProperties('" + lang + "'): lineCount=" + lineCount + " updateCount=" + updateCount);
                            GC.Collect();
                            GC.Collect();
                        }
                        lineCount++;
                        #endregion

                        currentTriple = NTripleParser.Split(strCurrentTriple, LogError);

                        #region some sample data for help
                        /* currentTriple.Subject               currentTriple.Predicate                           currentTriple.TripleObject
                    <http://dbpedia.org/resource/Alabama> <http://xmlns.com/foaf/0.1/name> "State of Alabama"@en .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/demonym> "Alabamian or Alabaman"@en .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/capital> <http://dbpedia.org/resource/Montgomery%2C_Alabama> .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/largestCity> <http://dbpedia.org/resource/Birmingham%2C_Alabama> .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/areaTotal> "1.35765E11"^^<http://www.w3.org/2001/XMLSchema#double> .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/areaLand> "1.31426E11"^^<http://www.w3.org/2001/XMLSchema#double> .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/areaWater> "4.338E9"^^<http://www.w3.org/2001/XMLSchema#double> .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/maximumElevation> "734.0"^^<http://www.w3.org/2001/XMLSchema#double> .
                    <http://dbpedia.org/resource/Alabama> <http://dbpedia.org/ontology/minimumElevation> "0.0"^^<http://www.w3.org/2001/XMLSchema#double> .                        
                        */
                        #endregion

                        #region clarify __coord> etc. handling
                        if (currentTriple.Subject.Contains("__"))
                        {
                            // TODO find better handling 
                            continue;
                        }
                        #endregion

                        if (currentDataItem == null)
                        {
                            #region create new ODataItem
                            if (CheckExistenceOfVertex(currentTriple.Subject, dictExistingNodes))
                            // if (!dictInstances.ContainsKey(currentTriple.Subject))
                            {
                                // Console.WriteLine("What's wrong here?");
                                // ignore uninserted data
                                currentDataItem = new ODataItem(new OClass("0815"));
                            }
                            else
                            {
                                // string instanceType = dictInstances[currentTriple.Subject];
                                string instanceType = GetVertexType(currentTriple.Subject, dictExistingNodes, LogError); // qr.FirstOrDefault().GetProperty<long>("TypeID"));
                                OClass instanceOClass = thisOntology.GetOClass(instanceType);
                                currentDataItem = instanceOClass.CreateODataItem();
                                currentDataItem.bToInsert = true;
                            }
                            currentDataItem.Subject = currentTriple.Subject;
                            #endregion
                        }
                        else
                        {
                            #region multiple rows can be aggregated to a single update statement (as long the currentSubject didn't change)
                            if (!currentTriple.Subject.Equals(currentDataItem.Subject))
                            {
                                #region save previous Instance - EXECUTE QUERY
                                if (CheckExistenceOfVertex(currentDataItem.Subject, dictExistingNodes)
                                    && !currentDataItem.IsEmpty())
                                {
                                    SaveODataItem(currentDataItem, dictExistingNodes, dictDirectoryEntries, lang, LogError);
                                    updateCount++;
                                }
                                #endregion

                                #region re-init all vars for next UPDATE ...  #region create new ODataItem
                                if (!CheckExistenceOfVertex(currentTriple.Subject, dictExistingNodes))
                                // !dictInstances.ContainsKey(currentTriple.Subject))
                                {
                                    // Console.WriteLine("What's wrong here?");
                                    // ignore uninserted data
                                    currentDataItem = new ODataItem(new OClass("0815"));
                                }
                                else
                                {

                                    string instanceType = GetVertexType(currentTriple.Subject, dictExistingNodes, LogError); //dictInstances[currentTriple.Subject];
                                    OClass instanceOClass = thisOntology.GetOClass(instanceType);
                                    currentDataItem = instanceOClass.CreateODataItem();
                                    currentDataItem.bToInsert = true;
                                }
                                currentDataItem.Subject = currentTriple.Subject;
                                #endregion
                            }
                            #endregion
                        }

                        #region temporary workaround - don't insert, when data had not been created before
                        if (!CheckExistenceOfVertex(currentTriple.Subject, dictExistingNodes)) // !dictInstances.ContainsKey(currentTriple.Subject))
                        {
                            continue;
                        }
                        #endregion

                        #region add current line to dictionary (or previously gql-string)
                        currentDataItem.Add(currentTriple.Predicate, currentTriple.TripleObject);

                        #endregion
                    }

                    #endregion

                    #region save last line
                    if (!currentDataItem.IsEmpty())
                    {
                        SaveODataItem(currentDataItem, dictExistingNodes, dictDirectoryEntries, lang, LogError);
                        updateCount++;
                    }
                    #endregion
                }  // using (StreamReader srNodes = new StreamReader(filename))
            }  // try
            catch (Exception e)
            {
                LogError("Error evaluating " + filename);
                LogError(e.Message);
                LogError(e.StackTrace);
            }
            LogMessage("End EvaluateMappingbasedProperties(" + filename + ", " + lang + ")");
        }

        private static void EvaluateAttributes(String filename, String KindOfText, String lang, Dictionary<string, List<string>> dictExistingNodes, Dictionary<string, uint> dictDirectoryEntries, Action<string> LogMessage, Action<string> LogError)
        {
            LogMessage("Begin EvaluateAttributes('" + KindOfText + "', '" + lang + "')");
            try
            {
                using (StreamReader srNodes = new StreamReader(filename))
                {
                    #region if input stream is empty --> do error handling
                    if (srNodes == null)
                    {
                        LogError("Error reading Nodes file: '" + filename + "'");
                        return;
                    }
                    #endregion

                    #region init local vars
                    String strCurrentTriple;
                    Triple currentTriple;
                    uint lineCount = 0;
                    uint insertCount = 0;
                    uint updateCount = 0;
                    //ODataItem currentItem = null;
                    #endregion

                    #region read each line
                    while ((strCurrentTriple = srNodes.ReadLine()) != null)
                    {
                        currentTriple = NTripleParser.Split(strCurrentTriple, LogError);

                        #region if Instance is already existing, do update for e.g. ShortAbstract
                        if (CheckExistenceOfVertex(currentTriple.Subject, dictExistingNodes)) //  .ContainsKey(currentTriple.Subject))
                        {
                            AddAttribute(currentTriple.Subject, KindOfText, lang, currentTriple.TripleObject, dictExistingNodes, dictDirectoryEntries, LogError);
                            updateCount++;
                        }
                        #endregion

                        #region otherwise, do insert for new "Thing"
                        else
                        {
                            /* todo define
                            if (insertCount <= Properties.Settings.Default.InsertLimit)
                            {
                                sbShortAbstractGql.Append("INSERT INTO httpwwww3org200207owlThing VALUES (ShortAbstract='");
                                sbShortAbstractGql.Append(GqlStringExtension.ReplaceStringLimiter(currentTriple.TripleObject));
                                if (Properties.Settings.Default.UUIDHandling)
                                    sbShortAbstractGql.Append("', UUID = '");
                                else
                                    sbShortAbstractGql.Append("', Name = '");
                                sbShortAbstractGql.Append(currentTriple.Subject);
                                sbShortAbstractGql.Append("')");
                                insertCount++;

                                QueryResult qr = Execute(sbShortAbstractGql.ToString());
                                if (qr != null && qr.FirstOrDefault().GetProperty<String>("UUID") != null)
                                {
                                    if (Properties.Settings.Default.UUIDHandling)
                                        Execute("INSERT INTO Instance VALUES(UUID='" + GqlStringExtension.ReplaceStringLimiter(currentTriple.Subject) + "', RefersToEntry=SETOFUUIDS('" + qr.FirstOrDefault().GetProperty<String>("UUID").ToString() + "'))");
                                    else
                                        Execute("INSERT INTO Instance VALUES(Name='" + GqlStringExtension.ReplaceStringLimiter(currentTriple.Subject) + "', RefersToEntry=SETOFUUIDS('" + qr.FirstOrDefault().GetProperty<String>("UUID").ToString() + "'))");
                                }
                            }*/
                        }
                        #endregion

                        #region some debug info
                        lineCount++;
                        /*if (lineCount % 100 == 0)
                        {
                            Console.Write(".");
                        }
                        */
                        if (lineCount % 10000 == 0)
                        {
                            LogMessage("EvaluateAttributes('" + KindOfText + "', '" + lang + "'): lineCount=" + lineCount + " insertCount=" + insertCount + " updateCount=" + updateCount);
                            GC.Collect();
                            GC.Collect();
                        }
                        #endregion
                    }
                    #endregion
                }
            }
            catch (Exception e)
            {
                LogError("Error evaluating: " + filename);
                LogError(e.Message);
                LogError(e.StackTrace);
            }
            LogMessage("End EvaluateAttributes('" + KindOfText + "', '" + lang + "')");
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

        private static bool CheckExistenceOfVertex(String vertexId, Dictionary<string, List<string>> dictExistingNodes)
        {
            if (dictExistingNodes.ContainsKey(vertexId))
                // File.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(vertexId)))
                return true;
            else
                return false;
        }

        private static String GetVertexType(String vertexId, Dictionary<string, List<string>> dictExistingNodes, Action<string> LogError)
        {
            if (dictExistingNodes.ContainsKey(vertexId))
            // File.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(vertexId)))
            {
                try
                {
                    string[] allLines = File.ReadAllLines(dictExistingNodes[vertexId][0]);
                    // Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(vertexId));
                    char[] sepChars = { '=' };
                    string[] token = null;
                    foreach (String line in allLines)
                    {
                        token = line.Split(sepChars);
                        if (token.Length < 2)
                            LogError("Invalid line: " + token);
                        else
                        {
                            if (token[0].Trim().Equals(vertexId))
                            {
                                return token[1];
                            }
                        }
                    }
                }
                catch (Exception a)
                {
                    LogError(a.Message);
                    LogError(a.StackTrace);
                }
            }
            return "http://www.w3.org/2002/07/owl#Thing";
        }

        private static bool SaveTriple(Triple selectedTriple, Dictionary<string, List<string>> dictExistingNodes, Dictionary<string, uint> dictDirectoriesEntries,
            Dictionary<string, long> dictVertexIDs, Action<String> LogError)
        {
            StreamWriter sw = null;
            try
            {
                if (!dictExistingNodes.ContainsKey(selectedTriple.Subject))
                //if (!File.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(selectedTriple.Subject)))
                {
                    ushort sDirs = 0;
                    String strPathname = null;
                    DirectoryInfo dirInfo = null;

                    /*
                    foreach (String hashDirName in Directory.EnumerateDirectories(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir))
                    {
                        dirInfo = new DirectoryInfo(hashDirName);
                        if (dirInfo.GetFiles().Length < Properties.Settings.Default.DataDirMaxEntries)
                        {
                            strPathname = Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar
                                + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar
                                + dirInfo.Name + Path.DirectorySeparatorChar;
                            break;
                        }
                        else strPathname = null;

                        sDirs++;
                    }
                    */
                    foreach (String dirname in dictDirectoriesEntries.Keys)
                    {
                        if (dictDirectoriesEntries[dirname] < Properties.Settings.Default.DataDirMaxEntries)
                        {
                            strPathname = dirname;
                            break;
                        }
                        sDirs++;
                    }

                    if (strPathname == null)
                    {
                        if (Directory.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar
                                + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar
                                + sDirs))
                        {
                            LogError("Directory already exists! " + Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar
                                + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar
                                + sDirs + Path.DirectorySeparatorChar);
                            return false;
                        }
                        else
                        {
                            strPathname = Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar
                                + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar
                                + sDirs + Path.DirectorySeparatorChar;
                            Directory.CreateDirectory(strPathname);

                            dictDirectoriesEntries.Add(strPathname, 0);
                        }
                    }
                    sw = File.CreateText(strPathname + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(selectedTriple.Subject));
                    sw.WriteLine("VertexID=" + dictVertexIDs[selectedTriple.TripleObject].ToString());

                    List<string> lData = new List<string>();
                    lData.Add(strPathname + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(selectedTriple.Subject));
                    lData.Add(dictVertexIDs[selectedTriple.TripleObject].ToString());
                    lData.Add(selectedTriple.TripleObject);
                    dictExistingNodes.Add(selectedTriple.Subject, lData);
                    dictDirectoriesEntries[strPathname]++;
                    dictVertexIDs[selectedTriple.TripleObject]++;
                }
                else
                {
                    sw = File.AppendText(dictExistingNodes[selectedTriple.Subject][0]);
                    // Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(selectedTriple.Subject));
                }
                sw.WriteLine(selectedTriple.Subject + "=" + selectedTriple.TripleObject);
                sw.Flush();
                sw.Close();
            }
            catch (Exception a)
            {
                LogError("Error writing Node-File to FS: " + selectedTriple.Subject);
                LogError(a.Message);
                LogError(a.StackTrace);
                return false;
            }
            return true;
        }

        private static void AddAttribute(String subject, String attribute_key, String lang, String attribute_value, Dictionary<string, List<string>> dictExistingNodes, Dictionary<string, uint> dictDirectoryEntries, Action<string> LogError)
        {
            if (!dictExistingNodes.ContainsKey(subject))
            {
                LogError("Missing node2file link in dictionary: " + subject);
            }
            else
            //File.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(subject)))
            {
                try
                {

                    StreamWriter fs = File.AppendText(dictExistingNodes[subject][0]);
                    // Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(subject));
                    fs.WriteLine(attribute_key + "_" + lang + "=" + attribute_value);
                    fs.Flush();
                    fs.Close();
                }
                catch (Exception a)
                {
                }
            }
        }

        private static void SaveODataItem(ODataItem currentDataItem, Dictionary<string, List<string>> dictExistingFiles, Dictionary<string, uint> dictDirectoryEntries, string lang, Action<string> LogError)
        {
            if (dictExistingFiles.ContainsKey(currentDataItem.Subject))
            //File.Exists(Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(currentDataItem.Subject)))
            {
                try
                {
                    StreamWriter fs = File.AppendText(dictExistingFiles[currentDataItem.Subject][0]);
                    //Properties.Settings.Default.WorkDir + Path.DirectorySeparatorChar + Properties.Settings.Default.DataDir + Path.DirectorySeparatorChar + RemoveEvilCharactersForFile(currentDataItem.Subject));
                    fs.WriteLine("Name_" + lang + "=" + currentDataItem.Subject);
                    foreach (String key in currentDataItem.updateProperties.Keys)
                    {
                        foreach (String listItem in currentDataItem.updateProperties[key])
                        {
                            // fs.WriteLine(GqlStringExtension.RemoveEvilCharacters(key) + "_" +lang + "=" + listItem);
                            fs.WriteLine(key + "_" + lang + "=" + listItem);
                        }
                    }
                    fs.Flush();
                    fs.Close();
                }
                catch (Exception a)
                {
                }
            }
            else
                LogError("missing file: " + RemoveEvilCharactersForFile(currentDataItem.Subject));
        }
    }
}