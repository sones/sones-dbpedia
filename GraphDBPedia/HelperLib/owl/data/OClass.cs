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
    public class OClass : IEnumerable<OClass> // : IComparable
    {
        public OClass()
        {
        }

        public OClass(string identifier/*, string about*/)
        {
            if (identifier != null)
                ID = identifier;
            /*            if (about != null)
                            About = about;
            */
        }

        public String ID { get; set; }
        //  public String About { get; set; }
        public String Label { get; set; }
        public String Comment { get; set; }
        public String CommentLang { get; set; }
        public OClass IsSubClassOf { get; set; }

        List<OProperty> lDatatypes;
        public void AddDatatype(OProperty od)
        {
            if (lDatatypes == null)
                lDatatypes = new List<OProperty>();
            lDatatypes.Add(od);
        }

        List<OProperty> lObjectProperties;
        public void AddObjectProperty(OProperty op)
        {
            if (lObjectProperties == null)
                lObjectProperties = new List<OProperty>();
            lObjectProperties.Add(op);
        }

        public OProperty GetProperty(string strProperty)
        {
            if (IsSubClassOf != null)
            {
                OProperty parentProp = IsSubClassOf.GetProperty(strProperty);
                if (parentProp != null)
                    return parentProp;
            }

            if (lDatatypes != null)
            {
                foreach (OProperty current in lDatatypes)
                {
                    if (current.ID.Equals(strProperty))
                        return current;
                }
            }

            if (lObjectProperties != null)
            {
                foreach (OProperty current in lObjectProperties)
                {
                    if (current.ID.Equals(strProperty))
                        return current;
                }
            }

            return null;
        }

        public enum PropertyType { Property, Edge, None };
        public PropertyType HasProperty(String strProperty)
        {
            if (IsSubClassOf != null)
            {
                PropertyType parentPropType = IsSubClassOf.HasProperty(strProperty);
                if (parentPropType != PropertyType.None)
                    return parentPropType;
            }

            if (lDatatypes != null)
            {
                foreach (OProperty current in lDatatypes)
                {
                    if (strProperty.StartsWith(current.ID))
                        return PropertyType.Property;
                }
            }

            if (lObjectProperties != null)
            {
                foreach (OProperty current in lObjectProperties)
                {
                    if (strProperty.StartsWith(current.ID))
                        return PropertyType.Edge;
                }
            }

            return PropertyType.None;
        }

        public List<string> GetAttributeNames()
        {
            List<string> attributeNames = new List<string>();

            if (lDatatypes != null)
            {
                foreach (OProperty current in lDatatypes)
                {
                    attributeNames.Add(current.ID);
                }
            }

            if (lObjectProperties != null)
            {
                foreach (OProperty current in lObjectProperties)
                {
                    attributeNames.Add(current.ID);
                }
            }

            return attributeNames;
        }

        List<OClass> lOClassChildren;
        public bool AddOClassChild(OClass oClassChild)
        {
            if (lOClassChildren == null)
                lOClassChildren = new List<OClass>();

            if (this.Equals(oClassChild.IsSubClassOf))
            {
                lOClassChildren.Add(oClassChild);
                oClassChild.IsSubClassOf = this;
                return true;
            }
            else
            {
                bool bRet = false;
                foreach (OClass oa in lOClassChildren)
                {
                    bRet = oa.AddOClassChild(oClassChild);
                    if (bRet) break;
                }
                return bRet;
            }
        }

        public String GetAllOClassesGql(String[] postFixes)
        {
            if (postFixes == null || postFixes.Length == 0)
            {
                postFixes = new String[1];
                postFixes[0] = "";
            }

            StringBuilder sbGql = new StringBuilder();
            sbGql.Append(GqlStringExtension.RemoveEvilCharacters(this.ID));

            if (this.IsSubClassOf != null)
            {
                sbGql.Append(" EXTENDS ");
                sbGql.Append(GqlStringExtension.RemoveEvilCharacters(this.IsSubClassOf.ID) /* strExtends */ );
            }

            StringBuilder sbAttributes = new StringBuilder(" ATTRIBUTES (");
            bool bFirst = true;

            if (lDatatypes != null)
            {
                foreach (OProperty datatype in this.lDatatypes)
                {
                    foreach (String postFix in postFixes)
                    {
                        if (bFirst) bFirst = false;
                        else sbAttributes.Append(", ");

                        #region mapping from owl to GraphDB datatypes
                        switch (datatype.Range)
                        {
                            #region http://www.w3.org/2001/* datatypes
                            case "http://www.w3.org/2001/XMLSchema#string":
                                {
                                    sbAttributes.Append("String");
                                    break;
                                }
                            case "http://www.w3.org/2001/XMLSchema#double":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://www.w3.org/2001/XMLSchema#nonNegativeInteger":
                            case "http://www.w3.org/2001/XMLSchema#positiveInteger":
                            case "http://www.w3.org/2001/XMLSchema#integer":
                                {
                                    sbAttributes.Append("Int64");
                                    break;
                                }
                            case "http://www.w3.org/2001/XMLSchema#date":
                                {
                                    sbAttributes.Append("DateTime");
                                    break;
                                }
                            case "http://www.w3.org/2001/XMLSchema#float":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://www.w3.org/2001/XMLSchema#boolean":
                                {
                                    sbAttributes.Append("Boolean");
                                    break;
                                }
                            case "http://www.w3.org/2001/XMLSchema#anyURI":
                                {
                                    sbAttributes.Append("String");
                                    break;
                                }
                            case "http://www.w3.org/2001/XMLSchema#gYear":
                                {
                                    sbAttributes.Append("Int64");
                                    break;
                                }
                            #endregion

                            #region http://dbpedia.org/datatype area units
                            case "http://dbpedia.org/datatype/squareKilometre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/squareMetre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            #endregion

                            #region http://dbpedia.org/datatype speed units
                            case "http://dbpedia.org/datatype/kilometrePerSecond":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/kilometrePerHour":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            #endregion

                            #region http://dbpedia.org/datatype density units
                            case "http://dbpedia.org/datatype/kilogramPerCubicMetre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/gramPerKilometre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            #endregion

                            #region http://dbpedia.org/datatype time units
                            case "http://dbpedia.org/datatype/day":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/hour":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/minute":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/second":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            #endregion

                            #region http://dbpedia.org/datatype/ volume units
                            case "http://dbpedia.org/datatype/cubicMetre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/cubicKilometre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/cubicCentimetre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            #endregion

                            #region http://dbpedia.org/datatype distance units
                            case "http://dbpedia.org/datatype/kilometre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/metre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/centimetre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/millimetre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            #endregion

                            #region http://dbpedia.org/datatype/ various others
                            case "http://dbpedia.org/datatype/inhabitantsPerSquareKilometre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/kelvin":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/cubicMetrePerSecond":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/kilogram":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/megabyte":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/litre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/valvetrain":
                                {
                                    sbAttributes.Append("String");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/engineConfiguration":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/fuelType":
                                {
                                    sbAttributes.Append("String");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/kilowatt":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            case "http://dbpedia.org/datatype/newtonMetre":
                                {
                                    sbAttributes.Append("Double");
                                    break;
                                }
                            #endregion

                            default:
                                {
                                    sbAttributes.Append(datatype.Range);
                                    break;
                                }
                        }
                        // sbAttributes.Append(datatype.Range /*Domain*/);
                        #endregion

                        sbAttributes.Append(" ");
                        sbAttributes.Append(GqlStringExtension.RemoveEvilCharacters(datatype.ID + postFix));
                    }
                }
            }

            if (lObjectProperties != null)
            {
                foreach (OProperty objectProp in this.lObjectProperties)
                {
                    foreach (String postFix in postFixes)
                    {
                        if (bFirst) bFirst = false;
                        else sbAttributes.Append(", ");

                        #region mapping from owl to GraphDB datatypes
                        /*
                    switch (objectProp.Range) 
                    {
                        default: 
                            {
                                sbAttributes.Append(objectProp.Range);
                                break;
                            }
                    }*/
                        sbAttributes.Append("SET<");
                        sbAttributes.Append(GqlStringExtension.RemoveEvilCharacters(objectProp.Range));
                        sbAttributes.Append("> ");
                        #endregion

                        sbAttributes.Append(GqlStringExtension.RemoveEvilCharacters(objectProp.ID + postFix));
                    }
                }
            }
            sbAttributes.Append(")");

            if (!bFirst)
            {
                sbGql.Append(sbAttributes);
            }

            return sbGql.ToString();
        }

        public bool RemoveOClassChild(OClass childClassToRemove)
        {
            #region if class-to-remove is a child
            if (lOClassChildren.Contains(childClassToRemove))
            {
                return lOClassChildren.Remove(childClassToRemove);
            }
            #endregion

            #region if class-to-remove is nested into tree structure - walk through and delete within
            foreach (OClass rootClass in lOClassChildren)
            {
                if (rootClass.ContainsOntologyClass(childClassToRemove))
                {
                    return rootClass.RemoveOClassChild(childClassToRemove);
                }
            }
            #endregion

            return false;
        }

        public OClass GetOntologyClass(OClass refOClass)
        {
            if (this.Equals(refOClass)) return this;
            if (lOClassChildren == null) return null;

            OClass retClass;
            foreach (OClass rootClass in lOClassChildren)
            {
                retClass = rootClass.GetOntologyClass(refOClass);
                if (retClass != null)
                    return retClass;
            }
            return null;
        }

        public int GetOntologyClassLevel(OClass refOClass)
        {
            if (this.Equals(refOClass))
                return 0;
            if (lOClassChildren == null) return -1;

            int iLevel;
            foreach (OClass rootClass in lOClassChildren)
            {
                iLevel = rootClass.GetOntologyClassLevel(refOClass);
                if (iLevel >= 0)
                {
                    iLevel++;
                    return iLevel;
                }
            }
            return -1;
        }

        public bool ContainsOntologyClass(OClass compareClass)
        {
            if (this.Equals(compareClass))
                return true;

            if (lOClassChildren == null)
                return false;

            foreach (OClass child in lOClassChildren)
            {
                if (child.ContainsOntologyClass(compareClass))
                {
                    return true;
                }
            }

            return false;
        }

        public List<OClass> GetAllClasses()
        {
            List<OClass> allClasses = new List<OClass>();
            if (lOClassChildren != null)
            {
                foreach (OClass current in lOClassChildren)
                {
                    foreach (OClass currentClass in current.GetAllClasses())
                    {
                        allClasses.Add(currentClass);
                    }
                }
            }
            allClasses.Add(this);
            return allClasses;
        }

        public override string ToString()
        {
            StringBuilder sbOClass = new StringBuilder(this.ID);
            sbOClass.Append("[children=");
            sbOClass.Append(CountChildren());
            sbOClass.Append("]");

            return sbOClass.ToString();
        }

        public override bool Equals(object obj)
        {
            OClass compareTo = (OClass)obj;
            if (this.ID != null && compareTo.ID != null && this.ID.Equals(compareTo.ID)) return true;
            //             else if (this.About != null && compareTo.About != null && this.About.Equals(compareTo.About)) return true;
            else return base.Equals(obj);
        }

        private int CountChildren()
        {
            int iCount = 1;

            if (lOClassChildren != null)
            {
                foreach (OClass child in lOClassChildren)
                {
                    iCount += child.CountChildren();
                }
            }
            return iCount;
        }

        public ODataItem CreateODataItem()
        {
            return new ODataItem(this);
        }

        #region IEnumerable<OClass> Members

        public IEnumerator<OClass> GetEnumerator()
        {
            yield return this;

            if (lOClassChildren == null || lOClassChildren.Count == 0)
            {
                yield break;
            }
            else
            {
                foreach (OClass current in lOClassChildren)
                {
                    foreach (var c in current)
                    {
                        yield return c;
                    }
                }
            }
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
