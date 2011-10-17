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

namespace de.sones.solutions.lib.owl.data
{
    public class Ontology
    {
        public String Xmlns { get; set; }
        public String XmlBase { get; set; }
        public String XmlnsRdf { get; set; }
        public String XmlnsRdfs { get; set; }
        public String XmlnsOwl { get; set; }

        public string About { get; set; }
        public string Comment { get; set; }
        public string Label { get; set; }
        public string VersionInfo { get; set; }


        public OClassTree ClassTree;
        // List<OClass> lOClasses = null;
        public bool AddOntologyClass(OClass newClass)
        {
            if (ClassTree == null)
                ClassTree = new OClassTree();

            if (newClass.ID == null /* && newClass.About == null*/)
                return false;

            bool bAdded = ClassTree.AddToTree(newClass);
            if (!bAdded)
                Console.WriteLine("Error! Didn't add <owl:class>: " + newClass);

            return true;
        }

        public OClass GetOClass(string strOClassId)
        {
            if (ClassTree == null) return null;
            return ClassTree.GetOntologyClass(new OClass(strOClassId));
        }

        public int GetOClassLevel(string strOClassId)
        {
            if (ClassTree == null)
                return -1;
            else
                return ClassTree.GetOntologyClassLevel(new OClass(strOClassId));
        }

        public List<OClass> GetAllClasses()
        {
            return ClassTree.GetAllClasses();
        }

        List<OProperty> lDatatypes;
        public bool AddDatatype(OProperty newDatatype)
        {
            if (newDatatype.Domain != null /* || newDatatype.Range != null*/)
            {
                string id;

                string domainOrRange = newDatatype.Domain;
                // if (domainOrRange == null) domainOrRange = newDatatype.Range;
                if (domainOrRange == null) return false;

                OClass oc = ClassTree.GetOntologyClass(new OClass(domainOrRange));
                if (oc != null)
                {
                    // if (oc.ID != null) 
                    id = oc.ID;
                    // else id = oc.About;

                    if (id.Equals(newDatatype.Domain))
                    {
                        oc.AddDatatype(newDatatype);
                    }
                }

                if (lDatatypes == null)
                    lDatatypes = new List<OProperty>();
                lDatatypes.Add(newDatatype);

                return true;
            }
            return false;
        }

        List<OProperty> lObjectProperties;
        public bool AddObjectProperty(OProperty newObjectProperty)
        {
            if (newObjectProperty.Domain != null /* || newObjectProperty.Range != null*/)
            {/*
                string domainOrRange = newObjectProperty.Domain;
                // if (domainOrRange == null) domainOrRange = newObjectProperty.Range;
                if (domainOrRange == null) return false;*/

                OClass oc = ClassTree.GetOntologyClass(new OClass(newObjectProperty.Domain));
                if (oc != null)
                {
                    oc.AddObjectProperty(newObjectProperty);
                }

                if (lObjectProperties == null)
                    lObjectProperties = new List<OProperty>();
                lObjectProperties.Add(newObjectProperty);

                return true;
            }
            return false;
        }


        /*
<owl:Ontology rdf:about="">
  <rdfs:comment>An university ontology for benchmark tests</rdfs:comment>
  <rdfs:label>Univ-bench Ontology</rdfs:label>
  <owl:versionInfo>univ-bench-ontology-owl, ver April 1, 2004</owl:versionInfo>
</owl:Ontology>
         */

    }
}
