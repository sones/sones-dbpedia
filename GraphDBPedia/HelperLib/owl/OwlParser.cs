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
using de.sones.solutions.lib.xml;

namespace de.sones.solutions.lib.owl
{
    public class OwlParser
    {
        public static Ontology CreateOntologyFromXml(XmlElement eXmlRoot, Action<string> errorAction)
        {
            Ontology onti = null;
            XmlElement eRootElement = null;

            #region <rdf:RDF>
            try
            {
                eRootElement = eXmlRoot.getChildElements("rdf:RDF").First();                        // <rdf:RDF
                onti = new Ontology();
                onti.Xmlns = eRootElement.getAttribute("xmlns", onti.Xmlns).strValue;               //   xmlns = "http://www.lehigh.edu/~zhp2/2004/0401/univ-bench.owl#"
                onti.XmlBase = eRootElement.getAttribute("xml:base", onti.XmlBase).strValue;        //   xml:base = "http://www.lehigh.edu/~zhp2/2004/0401/univ-bench.owl"
                onti.XmlnsRdf = eRootElement.getAttribute("xmlns:rdf", onti.XmlnsRdf).strValue;     //   xmlns:rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#"
                onti.XmlnsRdfs = eRootElement.getAttribute("xmlns:rdfs", onti.XmlnsRdfs).strValue;  //   xmlns:rdfs="http://www.w3.org/2000/01/rdf-schema#"
                onti.XmlnsOwl = eRootElement.getAttribute("xmlns:owl", onti.XmlnsOwl).strValue;     //   xmlns:owl="http://www.w3.org/2002/07/owl#"
            }
            catch (Exception a) { }
            if (eRootElement == null)
            {
                if (errorAction != null)
                    errorAction("invalid structure: Missing root:element <rdf:RDF ..>");
                return onti;
            }
            #endregion

            #region <owl:ontology>
            try
            {
                XmlElement eOntology = eRootElement.getChildElements("owl:Ontology").First();  // <owl:Ontology 
                onti.About = eOntology.getAttribute("rdf:about", onti.About).strValue;         //    rdf:about="">

                // not mandatory
                try
                {  // <rdfs:comment>An university ontology for benchmark tests</rdfs:comment>
                    onti.Comment = eOntology.getChildElements("rdfs:comment").First().getText(onti.Comment);
                }
                catch (Exception aa) { }

                // not mandatory
                try
                {  // <rdfs:label>Univ-bench Ontology</rdfs:label>
                    onti.Label = eOntology.getChildElements("rdfs:label").First().getText(onti.Label);
                }
                catch (Exception aa) { }

                // <owl:versionInfo>univ-bench-ontology-owl, ver April 1, 2004</owl:versionInfo>
                onti.VersionInfo = eOntology.getChildElements("owl:versionInfo").First().getText(onti.VersionInfo);
            }
            catch (Exception a)
            {
                if (errorAction != null)
                    errorAction("invalid structure: Missing param in <owl:ontology>." + a.Message + "\r\n" + a.StackTrace);
                return onti;
            }
            #endregion

            #region <owl:class>
            List<XmlElement> eClasses = eRootElement.getChildElements("owl:Class");
            OClass currentClass;
            String id;
            foreach (XmlElement xmlCurrentClass in eClasses)
            {
                try
                {
                    currentClass = new OClass();
                    try
                    {
                        currentClass.ID = xmlCurrentClass.getAttribute("rdf:ID", currentClass.ID).strValue;  // <owl:Class rdf:ID="AdministrativeStaff">    
                    }
                    catch (Exception aa) { }

                    try
                    {
                        if (currentClass.ID == null)
                        {
                            currentClass.ID = xmlCurrentClass.getAttribute("rdf:about", currentClass.ID /*About*/).strValue; // <owl:Class rdf:about="http://dbpedia.org/ontology/Cycad">
                        }
                    }
                    catch (Exception aa) { }

                    if (currentClass.ID == null)
                    {
                        if (errorAction != null)
                            errorAction("Error adding <owl:class>: Missing rdf:ID or rdf:about. " + xmlCurrentClass);
                        continue;
                    }

                    try  // not mandatory: <rdfs:label>administrative staff worker</rdfs:label>
                    {
                        currentClass.Label = xmlCurrentClass.getChildElements("rdfs:label").First().getText(currentClass.Label);
                    }
                    catch (Exception aa) { }

                    try  // not mandatory: <rdfs:comment xml:lang="en">a group of sports teams that compete against each other in Cricket</rdfs:comment>
                    {
                        XmlElement eClassComment = xmlCurrentClass.getChildElements("rdfs:comment").First();
                        currentClass.Comment = eClassComment.getText(currentClass.Comment);
                        currentClass.CommentLang = eClassComment.getAttribute("xml:lang", currentClass.CommentLang).strValue;
                    }
                    catch (Exception aa) { }

                    try  // not mandatory : // <rdfs:subClassOf rdf:resource="#Employee" />
                    {
                        id = xmlCurrentClass
                            .getChildElements("rdfs:subClassOf").First()
                            .getAttribute("rdf:resource", "").strValue;

                        if (id.StartsWith("#"))   // ? todo clarify
                            id = id.Substring(1);

                        currentClass.IsSubClassOf = new OClass(id /*, id*/);
                    }
                    catch (Exception aa) { }

                    try
                    {
                        XmlElement eIntersection = xmlCurrentClass.getChildElements("owl:intersectionOf").First();  // <owl:intersectionOf rdf:parseType="Collection">
                        XmlElement eISectClass = eIntersection.getChildElements("owl:Class").First();               //   <owl:Class rdf:about="#Person" />
                        string strIsectAbout = eISectClass.getAttribute("rdf:about", "").strValue;
                        if (strIsectAbout.StartsWith("#"))
                            strIsectAbout = strIsectAbout.Substring(1);  // todo clarify
                        currentClass.IsSubClassOf = new OClass(/*null,*/ strIsectAbout);

                        OProperty odProp = new OProperty();
                        XmlElement eRestriction = eIntersection.getChildElements("owl:Restriction").First();        // <owl:Restriction>
                        XmlElement eOnProperty = eRestriction.getChildElements("owl:onProperty").First();           // <owl:onProperty rdf:resource="#headOf" />
                        odProp.ID = eOnProperty.getAttribute("rdf:resource", "").strValue;

                        XmlElement eSomeValuesFrom = eRestriction.getChildElements("owl:someValuesFrom").First();    // <owl:someValuesFrom> 
                        XmlElement eValuesClass = eSomeValuesFrom.getChildElements("owl:Class").First();            // <owl:Class rdf:about="#Department" />
                        odProp.Domain = eValuesClass.getAttribute("rdf:about", "").strValue;
                        currentClass.AddDatatype(odProp);
                    }
                    catch (Exception aa) { }

                    if (!onti.AddOntologyClass(currentClass))
                    {
                        if (errorAction != null)
                            errorAction("Didn't add <owl:class> " + currentClass + " xml=" + xmlCurrentClass);
                        continue;
                    }
                    // onti.ValidateClasses();
                }
                catch (Exception a)
                {
                    Console.WriteLine("the following OntologyClass had not been imported due to errors.");
                    Console.WriteLine(xmlCurrentClass.ToString());
                    Console.WriteLine(a.Message);
                    Console.WriteLine(a.StackTrace);
                    Console.WriteLine();
                }
            }
            #endregion

            #region <owl:DatatypeProperty>
            List<XmlElement> lDatatypes = eRootElement.getChildElements("owl:DatatypeProperty");
            OProperty currentDatatype;
            foreach (XmlElement xmlCurrentElement in lDatatypes)
            {
                try
                {
                    currentDatatype = new OProperty();

                    try
                    {
                        currentDatatype.ID = xmlCurrentElement.getAttribute("rdf:ID", currentDatatype.ID).strValue;  // <owl:DatatypeProperty rdf:ID="age">
                    }
                    catch (Exception aa) { }

                    try
                    {
                        if (currentDatatype.ID == null)
                            currentDatatype.ID = xmlCurrentElement.getAttribute("rdf:about", currentDatatype.ID).strValue; // <owl:DatatypeProperty rdf:about="http://dbpedia.org/ontology/dateOfBurial">
                    }
                    catch (Exception aa) { }

                    if (currentDatatype.ID == null)
                    {
                        if (errorAction != null)
                            errorAction("Error adding <owl:DatatypeProperty>: Missing rdf:ID or rdf:about. " + xmlCurrentElement);
                        continue;
                    }

                    try
                    {
                        currentDatatype.Domain = xmlCurrentElement
                            .getChildElements("rdfs:domain").First()
                            .getAttribute("rdf:resource", currentDatatype.Domain).strValue;
                    }
                    catch (Exception a) { }

                    try  // not mandatory: <rdfs:range rdf:resource="http://www.w3.org/2001/XMLSchema#date"></rdfs:range>
                    {
                        // if (currentDatatype.Domain == null)
                        // {
                        currentDatatype.Range = xmlCurrentElement
                            .getChildElements("rdfs:range").First()
                            .getAttribute("rdf:resource", currentDatatype.Domain).strValue;
                        // }
                    }
                    catch (Exception aa) { }

                    /*
                    if (currentDatatype.Domain == null) 
                    {
                        errorAction("Error adding <owl:DatatypeProperty>: Neither Range or Domain is set in xml:" + xmlCurrentElement);
                        continue;
                    }*/

                    try  // not mandatory: <rdfs:label>is age</rdfs:label>
                    {
                        currentDatatype.Label = xmlCurrentElement.getChildElements("rdfs:label").First().getText(currentDatatype.Label);
                    }
                    catch (Exception aa) { }


                    if (!onti.AddDatatype(currentDatatype))
                    {
                        if (errorAction != null)
                            errorAction("Didn't add <owl:DatatypeProperty> " + currentDatatype);
                        continue;
                    }
                    /*
                    else
                    {
                        Console.WriteLine("added datatype: " + currentDatatype.ToString());
                    }*/
                }
                catch (Exception a)
                {
                    Console.WriteLine("the following datatype had not been imported due to errors.");
                    Console.WriteLine(xmlCurrentElement.ToString());
                    Console.WriteLine(a.Message);
                    Console.WriteLine(a.StackTrace);
                    Console.WriteLine();
                }
            }
            #endregion

            #region <owl:ObjectProperty rdf:about="http://dbpedia.org/ontology/similar">
            List<XmlElement> lObjectProperties = eRootElement.getChildElements("owl:ObjectProperty");
            OProperty currentObjectProperty;
            foreach (XmlElement xmlCurrentObjectProperty in lObjectProperties)
            {
                try
                {
                    currentObjectProperty = new OProperty();

                    try
                    {
                        currentObjectProperty.ID = xmlCurrentObjectProperty.getAttribute("rdf:ID", currentObjectProperty.ID).strValue;  // <owl:ObjectProperty rdf:ID="degreeFrom">
                    }
                    catch (Exception aa) { }

                    try
                    {
                        if (currentObjectProperty.ID == null)
                        {
                            currentObjectProperty.ID = xmlCurrentObjectProperty.getAttribute("rdf:about", currentObjectProperty.ID).strValue; // <owl:ObjectProperty rdf:about="http://dbpedia.org/ontology/similar">
                        }
                    }
                    catch (Exception aa) { }

                    if (currentObjectProperty.ID == null)
                    {
                        if (errorAction != null)
                            errorAction("Error adding <owl:ObjectProperty>: Missing rdf:ID or rdf:about. " + xmlCurrentObjectProperty);
                        continue;
                    }

                    try
                    {
                        currentObjectProperty.Domain = xmlCurrentObjectProperty
                            .getChildElements("rdfs:domain").First()
                            .getAttribute("rdf:resource", currentObjectProperty.Domain).strValue;
                    }
                    catch (Exception aa)
                    {
                        if (errorAction != null)
                            errorAction("Error adding ObjectProperty: <rdfs:domain> is not set in xml:" + xmlCurrentObjectProperty);
                        continue;
                    }

                    try
                    {
                        currentObjectProperty.Range = xmlCurrentObjectProperty
                            .getChildElements("rdfs:range").First()
                            .getAttribute("rdf:resource", currentObjectProperty.Range).strValue;
                    }
                    catch (Exception aa)
                    {
                        if (errorAction != null)
                            errorAction("Error adding ObjectProperty: <rdfs:range> is not set in xml:" + xmlCurrentObjectProperty);
                        continue;
                    }

                    try  // not mandatory: <rdfs:label>is age</rdfs:label>
                    {
                        currentObjectProperty.Label = xmlCurrentObjectProperty.getChildElements("rdfs:label").First().getText(currentObjectProperty.Label);
                    }
                    catch (Exception aa) { }

                    try  // not mandatory: <owl:inverseOf rdf:resource="#hasAlumnus"/>
                    {
                        currentObjectProperty.InverseOf = xmlCurrentObjectProperty.getChildElements("rdfs:label").First().getText(currentObjectProperty.InverseOf);
                    }
                    catch (Exception aa) { }

                    try  // not mandatory: <rdfs:subPropertyOf rdf:resource="#memberOf" />
                    {
                        currentObjectProperty.SubPropertyOf = xmlCurrentObjectProperty
                            .getChildElements("rdfs:subPropertyOf").First()
                            .getAttribute("", currentObjectProperty.SubPropertyOf).strValue;
                    }
                    catch (Exception aa) { }

                    if (!onti.AddObjectProperty(currentObjectProperty))
                    {
                        if (errorAction != null)
                            errorAction("Didn't add <owl:ObjectProperty> " + currentObjectProperty);
                    }
                }
                catch (Exception a)
                {
                    Console.WriteLine("the following ObjectProperty had not been imported due to errors.");
                    Console.WriteLine(xmlCurrentObjectProperty.ToString());
                    Console.WriteLine(a.Message);
                    Console.WriteLine(a.StackTrace);
                    Console.WriteLine();
                }
            }
            #endregion

            return onti;
        }
    }
}
