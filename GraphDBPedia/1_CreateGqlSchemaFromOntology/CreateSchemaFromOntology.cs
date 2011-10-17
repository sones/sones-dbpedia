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
using de.sones.solutions.lib.owl.data;
using de.sones.solutions.lib.owl;
using System.IO;
using de.sones.solutions.lib.xml;

namespace sones.solutions.dbpedia.ddl
{
    class CreateSchemaFromOntology
    {
        static void Main(string[] args)
        {
            while (true)
            {
                #region extract ontology filename from command line args
                String ontologyFilename = null;
                String gqlFilename = null;
                if (args.Length != 2)
                {
                    Console.WriteLine("Wrong number of arguments!");
                    Console.WriteLine("Usage: CreateSchemaFromOntology <OntologyFilename> <GqlFilename>");
                    Console.WriteLine("  e.g. CreateSchemaFromOntology dbpedia_3.6.owl dbpedia.gql");
                    Console.WriteLine("File to import should reside at the execution directory: " + Environment.CurrentDirectory);
                }
                else
                {
                    ontologyFilename = args[0];
                    gqlFilename = args[1];
                }
                #endregion

                #region check file existence and load ontology
                Ontology ontology = null;
                try
                {
                    XmlElement rootElement = XmlParser.loadXml(new StreamReader(ontologyFilename), Console.WriteLine);
                    if (rootElement == null)
                    {
                        Console.WriteLine("Null or empty XmlStructure in file: '" + ontologyFilename + "'");
                        break;
                    }
                    ontology = OwlParser.CreateOntologyFromXml(rootElement, Console.WriteLine); // .CreateOntologyFromXml(rootElement, Console.WriteLine);

                    #region add Thing manually! - with 3 attributes (Name, ShortAbstract, LongAbstract)
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

                    ontology.AddOntologyClass(thing);
                    #endregion
                }
                catch (Exception a)
                {
                    Console.WriteLine(a.Message);
                    Console.WriteLine(a.StackTrace);

                    break;
                }
                #endregion

                #region ask for languages to add
                List<String> lLangs = new List<string>();
                Console.WriteLine("Which languages should be added to the Ontology?");
                Console.WriteLine("Enter language id and press <Enter>");
                Console.WriteLine("An empty line quits the iteration process.");
                String currentLine = null;
                bool bFirst = true;
                while ((currentLine = Console.ReadLine().Trim()) != "")
                {
                    lLangs.Add("_" + currentLine);

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

                #region create gql file
                try
                {
                    StreamWriter swGql = new StreamWriter(gqlFilename);

                    swGql.WriteLine("CREATE VERTEX TYPE httpwwww3org200301geowgs84_posSpatialThing");

                    ontology.ClassTree.GetAllOClassesGql(swGql.WriteLine, lLangs.ToArray());

                    swGql.WriteLine("CREATE VERTEX TYPE Label ATTRIBUTES (String Name)");
                    foreach (String lang in lLangs)
                    {
                        swGql.WriteLine("CREATE INDEX IDX_Thing_Name" + lang + " ON VERTEX TYPE httpwwww3org200207owlThing (Name" + lang + ") INDEXTYPE SingleValuePersistent");
                    }
                    // swGql.WriteLine("ALTER VERTEX TYPE httpwwww3org200207owlThing ADD ATTRIBUTES (String ShortAbstract_de, String ShortAbstract_en)");
                    StringBuilder sbCreateInstance = new StringBuilder();
                    sbCreateInstance.Append("CREATE VERTEX TYPE Instance ATTRIBUTES (String Name, ");
                    foreach (String lang in lLangs)
                    {
                        sbCreateInstance.Append("SET<Label> Labels");
                        sbCreateInstance.Append(lang);
                        sbCreateInstance.Append(", ");
                    }
                    sbCreateInstance.Append("SET<httpwwww3org200207owlThing> RefersToEntry)");
                    swGql.WriteLine(sbCreateInstance.ToString());

                    swGql.WriteLine("CREATE INDEX IDX_Instance_Name ON VERTEX TYPE Instance (Name) INDEXTYPE SingleValuePersistent");

                    swGql.Flush();
                    swGql.Close();
                }
                catch (Exception a)
                {
                    Console.WriteLine(a.Message);
                    Console.WriteLine(a.StackTrace);

                    break;
                }
                #endregion

                break;
            }

            Console.WriteLine("Press <Enter> to quit program.");
            Console.ReadLine();
        }
    }
}
