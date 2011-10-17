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
    public class ODataItem
    {
        OClass currentClass { get; set; }
        public String Subject { get; set; }
        public Dictionary<string, List<string>> updateProperties;
        public List<string> allowedProperties;
        public bool bToInsert = false;
        bool bEmpty = true;


        public ODataItem(OClass originClass)
        {
            currentClass = originClass;
            allowedProperties = new List<string>();
            allowedProperties = currentClass.GetAttributeNames();
            updateProperties = new Dictionary<string, List<string>>();
            foreach (string prop in allowedProperties)
            {
                updateProperties.Add(prop, new List<string>());
            }
        }

        public bool Add(String propertyName, String propertyValue)
        {

            if (!allowedProperties.Contains(propertyName))
            {
                return false;
            }
            else
            {
                updateProperties[propertyName].Add(propertyValue);
                bEmpty = false;
                return true;
            }
        }

        public bool IsEmpty()
        {
            return bEmpty;
        }

        public string BuildUpdateGql(Ontology thisOntology, /*Dictionary<string, string> dictInstances,*/ bool bUUIDHandling)
        {
            #region init vars
            StringBuilder sbUpdateGql = new StringBuilder("UPDATE ");
            bool bFirst = true;
            string strTripleListBeginLimiter = null;
            string strTripleObjectBeginLimiter = null;
            string strTripleObjectEndLimiter = null;
            string strTripleListEndLimiter = null;
            string strCompareAttString = null;
            #endregion

            sbUpdateGql.Append(GqlStringExtension.RemoveEvilCharacters(currentClass.ID));
            sbUpdateGql.Append(" SET (");

            foreach (string key in updateProperties.Keys)
            {
                OProperty currentProp = currentClass.GetProperty(key);

                if (currentProp == null)
                {
                    continue;
                }
                else
                {
                    bool bMultipleValues = true;
                    if (updateProperties[key].Count > 0)
                    {
                        #region prerequisites for value gql (pre/post limiter, data type specific handling)
                        switch (currentProp.Range /* currentTriple.Datatype */)
                        {
                            #region simple string types
                            case "string":
                            case "http://www.w3.org/2001/XMLSchema#string":
                            case "http://www.w3.org/2001/XMLSchema#date":
                            case "http://www.w3.org/2001/XMLSchema#anyURI":
                            #endregion
                            #region workaround simple types
                            case "http://dbpedia.org/datatype/valvetrain":
                            case "http://dbpedia.org/datatype/fuelType":
                            #endregion
                                #region set limiters
                                {
                                    strTripleListBeginLimiter = "'";
                                    strTripleObjectBeginLimiter = "";
                                    strTripleObjectEndLimiter = "";
                                    strTripleListEndLimiter = "'";
                                    bMultipleValues = false;
                                    break;
                                }
                                #endregion

                            #region simple number, etc. types
                            case "http://www.w3.org/2001/XMLSchema#integer":
                            case "http://www.w3.org/2001/XMLSchema#nonNegativeInteger":
                            case "http://www.w3.org/2001/XMLSchema#positiveInteger":
                            case "http://www.w3.org/2001/XMLSchema#double":
                            case "http://www.w3.org/2001/XMLSchema#float":
                            case "http://www.w3.org/2001/XMLSchema#gYear":
                            case "http://www.w3.org/2001/XMLSchema#boolean":
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
                                #region set limiter
                                {
                                    strTripleListBeginLimiter = "";
                                    strTripleObjectBeginLimiter = "";
                                    strTripleObjectEndLimiter = "";
                                    strTripleListEndLimiter = "";
                                    bMultipleValues = false;
                                    break;
                                }
                                #endregion
                            default:
                                #region set limiter
                                {
                                    if (bUUIDHandling)
                                    {
                                        strCompareAttString = "SETOFUUIDS(";
                                        strTripleListBeginLimiter = "SETOFUUIDS(";
                                        strTripleObjectBeginLimiter = "'";
                                        // strTripleObjectBeginLimiter = "REF(Name='";
                                        strTripleObjectEndLimiter = "'";
                                        strTripleListEndLimiter = ")";
                                    }
                                    else
                                    {
                                        strCompareAttString = "SETOF(";
                                        strTripleListBeginLimiter = "SETOF(";
                                        strTripleObjectBeginLimiter = "Name='";
                                        // strTripleObjectBeginLimiter = "REF(Name='";
                                        strTripleObjectEndLimiter = "'";
                                        strTripleListEndLimiter = ")";
                                    }
                                    bMultipleValues = true;
                                    break;
                                }
                                #endregion
                        }
                        #endregion

                        #region add values to sbUpdateValues
                        StringBuilder sbUpdateValues = new StringBuilder();
                        bool bFirstInList = true;
                        string newValue = null;
                        foreach (string value in updateProperties[key])
                        {
                            if (strTripleListBeginLimiter.Equals(strCompareAttString)
                                //&& !dictInstances.ContainsKey(value)
                                )
                                continue;

                            if (bFirstInList) bFirstInList = false;
                            else sbUpdateValues.Append(", ");

                            sbUpdateValues.Append(strTripleObjectBeginLimiter);

                            if (strTripleListBeginLimiter.Equals(strCompareAttString)
                                && value.Contains("__"))  // workaround for "__coord" workaround
                            {
                                newValue = value.Substring(0, value.IndexOf("__"));
                            }
                            else newValue = value;

                            if (currentProp.Range.Equals("http://www.w3.org/2001/XMLSchema#boolean"))
                            {
                                if (value.ToLower().Equals("true"))
                                    newValue = "1";
                                else newValue = "0";
                            }
                            else if (currentProp.Range.Contains("nteger"))  // integer workaround
                            {
                                if (value.Contains('.'))
                                    newValue = value.Substring(0, value.IndexOf('.'));
                            }
                            else
                                newValue = GqlStringExtension.ReplaceStringLimiter(newValue);

                            sbUpdateValues.Append(newValue);

                            sbUpdateValues.Append(strTripleObjectEndLimiter);

                            if (!bMultipleValues) break;
                        }
                        #endregion

                        #region add next attribute to sbUpdateGql (only if values are filled)
                        if (sbUpdateValues.Length > 0)
                        {
                            #region add key to gql (e.g. "Name=")
                            if (!bFirst)
                                sbUpdateGql.Append(", ");
                            else bFirst = false;

                            sbUpdateGql.Append(GqlStringExtension.RemoveEvilCharacters(key));
                            sbUpdateGql.Append("=");
                            #endregion
                            sbUpdateGql.Append(strTripleListBeginLimiter);
                            sbUpdateGql.Append(sbUpdateValues.ToString());
                            sbUpdateGql.Append(strTripleListEndLimiter);
                        }
                        #endregion

                    }
                }
            }

            if (bUUIDHandling)
            {
                sbUpdateGql.Append(") WHERE UUID = '");
            }
            else
            {
                sbUpdateGql.Append(") WHERE Name = '");
            }
            sbUpdateGql.Append(Subject);
            sbUpdateGql.Append("'");

            if (bFirst)
                return "";
            else
                return sbUpdateGql.ToString();
        }
    }
}
