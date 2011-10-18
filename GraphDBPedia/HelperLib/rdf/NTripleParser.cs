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

namespace de.sones.solutions.lib.rdf
{
    public static class NTripleParser
    {


        /*
1.) Subjects may take the form of a URI or a named node; 
2.) Predicates must be a URI; 
3.) Objects may be a URI, named node or a literal. 
4.) URIs are delimited with less-than and greater-than signs used as angle brackets. 
5.) Named nodes are represented by an alphanumeric string, prefixed with an underscore and colon (_:). 
6.) Literals are represented by a C-style string, optionally suffixed with a language or datatype indicator. 
7.) Language indicators are an at sign followed by an RFC 3066 language tag; 
8.) datatype indicators are a double-caret followed by a URI. 
-- 9.) Comments consist of a line beginning with a hash sign.  --> currently not implemented
         */

        enum TRIPLE_TAGS { SUBJECT, PREDICATE, OBJECT, POST_OBJECT, OBJECT_LANG, OBJECT_DATATYPE } ;

        public static Triple Split(String line, Action<string> errorAction)
        {
            Triple triple = new Triple();
            TRIPLE_TAGS CurrentTag = TRIPLE_TAGS.SUBJECT;

            // <http://en.wikipedia.org/wiki/Tony_Benn> <http://purl.org/dc/elements/1.1/title>     "Tony Benn" .
            // <http://en.wikipedia.org/wiki/Tony_Benn> <http://purl.org/dc/elements/1.1/publisher> "Wikipedia" .
            // Hallo <http://test.de/blubb> "Kawumm"@German^^<http://test.de/string> .

            #region init local vars
            int iCurrent = 0;
            StringBuilder strCurrentTag = new StringBuilder();
            bool bFirst = true;
            bool bMaskChar = false;
            char cMaskChar = '0';

            #endregion

            foreach (char currentChar in line.ToCharArray())
            {
                #region simply read char, in case it is running in masked mode
                if (bMaskChar)
                {
                    strCurrentTag.Append(currentChar);
                    if (currentChar == cMaskChar)
                        bMaskChar = false;
                }
                #endregion

                if (!bMaskChar)  // no else, because criteria might have changes within the if
                {
                    switch (CurrentTag)
                    {
                        #region SUBJECT handling
                        case TRIPLE_TAGS.SUBJECT:
                            {
                                switch (currentChar)
                                {
                                    case ' ':
                                        {
                                            if (!bFirst)
                                            {
                                                triple.Subject = strCurrentTag.ToString();
                                                strCurrentTag = new StringBuilder();
                                                CurrentTag = TRIPLE_TAGS.PREDICATE;
                                                bFirst = true;
                                            }
                                            break;
                                        }
                                    case '<':
                                        {
                                            if (!bFirst)
                                                errorAction("invalid format: Subject may not contain '<' unless its a URI.");
                                            else
                                            {
                                                // strCurrentTag.Append(currentChar);
                                                bFirst = false;
                                                bMaskChar = true;
                                                cMaskChar = '>';
                                            }
                                            break;
                                        }
                                    case '>':
                                        {
                                            triple.Subject = strCurrentTag.ToString()
                                                .Substring(0, strCurrentTag.Length - 1);   // cut >
                                            strCurrentTag = new StringBuilder();
                                            CurrentTag = TRIPLE_TAGS.PREDICATE;
                                            bFirst = true;
                                            break;
                                        }
                                    default:
                                        {
                                            strCurrentTag.Append(currentChar);
                                            bFirst = false;
                                            bMaskChar = true;
                                            cMaskChar = ' ';

                                            break;
                                        }
                                }

                                break;
                            }
                        #endregion

                        #region PREDICATE handling
                        case TRIPLE_TAGS.PREDICATE:
                            {
                                switch (currentChar)
                                {
                                    case ' ':   // ignore leading blanks
                                        {
                                            if (!bFirst)
                                            {
                                                errorAction("no blanks allowed in PREDICATE URI!");
                                                throw new Exception();
                                            }
                                            break;
                                        }
                                    case '<':
                                        {
                                            if (!bFirst)
                                                errorAction("invalid format: Subject may not contain '<' unless its a URI.");
                                            else
                                            {
                                                // strCurrentTag.Append(currentChar);
                                                bFirst = false;
                                                bMaskChar = true;
                                                cMaskChar = '>';
                                            }
                                            break;
                                        }
                                    case '>':
                                        {
                                            if (bFirst)
                                            {
                                                errorAction("PREDICATE has top begin with '<'!");
                                                throw new Exception();
                                            }
                                            else
                                            {
                                                triple.Predicate = strCurrentTag.ToString()
                                                    .Substring(0, strCurrentTag.Length - 1);   // cut >
                                                strCurrentTag = new StringBuilder();
                                                CurrentTag = TRIPLE_TAGS.OBJECT;
                                                bFirst = true;
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            if (bFirst)
                                            {
                                                errorAction("PREDICATE has top begin with '<'!");
                                                throw new Exception();
                                            }
                                            else strCurrentTag.Append(currentChar);

                                            break;
                                        }
                                }
                                break;
                            }
                        #endregion

                        #region OBJECT handling
                        case TRIPLE_TAGS.OBJECT:
                            {
                                switch (currentChar)
                                {
                                    case ' ':
                                        {
                                            if (!bFirst)
                                            {
                                                errorAction("no blanks allowed in OBJECT (except within '\"')!");
                                                throw new Exception();
                                            }
                                            break;
                                        }
                                    case '"':
                                        {
                                            if (bFirst)
                                            {
                                                // strCurrentTag.Append(currentChar);

                                                bFirst = false;
                                                bMaskChar = true;
                                                cMaskChar = '"';
                                            }
                                            else
                                            {
                                                triple.TripleObject = strCurrentTag.ToString()
                                                    .Substring(0, strCurrentTag.Length - 1); ;   // cut trailing "
                                                strCurrentTag = new StringBuilder();
                                                CurrentTag = TRIPLE_TAGS.POST_OBJECT;
                                                bFirst = true;
                                            }
                                            break;
                                        }
                                    case '<':
                                        {
                                            if (bFirst)
                                            {
                                                // strCurrentTag.Append(currentChar);
                                                bFirst = false;
                                                bMaskChar = true;
                                                cMaskChar = '>';
                                            }
                                            break;
                                        }
                                    case '>':
                                        {
                                            triple.TripleObject = strCurrentTag.ToString()
                                                .Substring(0, strCurrentTag.Length - 1); ;    // ignore > 
                                            strCurrentTag = new StringBuilder();
                                            CurrentTag = TRIPLE_TAGS.POST_OBJECT;
                                            bFirst = true;
                                            break;
                                        }
                                    default:
                                        {
                                            if (bFirst)
                                            {
                                                errorAction("OBJECT must either begin with '\"' or '<'!");
                                                throw new Exception();
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                        #endregion

                        #region optional OBJECT data (language and datatype)
                        case TRIPLE_TAGS.POST_OBJECT:
                            {
                                switch (currentChar)
                                {/*
                                    case ' ': 
                                        {
                                            if (!bFirst)
                                            {
                                                errorAction("blank's are not allowed in optional Object data.");
                                                throw new Exception();
                                            }  // otherwise ignore
                                            break;
                                        }*/
                                    case '@': // Language
                                        {
                                            CurrentTag = TRIPLE_TAGS.OBJECT_LANG;
                                            break;
                                        }
                                    case '^':
                                        {
                                            CurrentTag = TRIPLE_TAGS.OBJECT_DATATYPE;
                                            break;
                                        }
                                }
                                break;
                            }
                        #endregion

                        #region OBJECT_LANG
                        case TRIPLE_TAGS.OBJECT_LANG:
                            {
                                switch (currentChar)
                                {
                                    case ' ':
                                    case '^':
                                    case '.':
                                        {
                                            triple.Language = strCurrentTag.ToString();
                                            strCurrentTag = new StringBuilder();
                                            CurrentTag = TRIPLE_TAGS.POST_OBJECT;
                                            break;
                                        }
                                    default:
                                        {
                                            strCurrentTag.Append(currentChar);
                                            break;
                                        }
                                }
                                break;
                            }
                        #endregion

                        #region OBJECT_DATATYPE
                        case TRIPLE_TAGS.OBJECT_DATATYPE:
                            {
                                switch (currentChar)
                                {
                                    case '^':  // ignore this char
                                        {
                                            break;
                                        }
                                    case '<':
                                        {
                                            // strCurrentTag.Append(currentChar);
                                            bMaskChar = true;
                                            cMaskChar = '>';
                                            bFirst = false;
                                            break;
                                        }
                                    case '>':
                                        {
                                            triple.Datatype = strCurrentTag.ToString()
                                                .Substring(0, strCurrentTag.Length - 1);    // remove trailing >
                                            CurrentTag = TRIPLE_TAGS.POST_OBJECT;
                                            break;
                                        }
                                    default:
                                        {
                                            strCurrentTag.Append(currentChar);
                                            break;
                                        }
                                }
                                break;
                            }
                        #endregion
                    }
                }
                iCurrent++;
            }

            return triple;
        }



    }


}
