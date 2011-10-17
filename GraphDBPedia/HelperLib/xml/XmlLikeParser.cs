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

namespace de.sones.solutions.lib.xml
{
    public class XmlParser
    {
        #region my little private own XML parser ...
        enum Modes { TEXT, ELEMENT, ATTRIBUTE_KEY, ATTRIBUTE_VALUE };
        public static XmlElement loadXml(StreamReader streamReader, Action<String> logError)
        {
            XmlElement rootElement = null;
            XmlElement currentElement = null;
            XmlElement newElement = null;
            XmlAttribute currentAttribute = null;

            String strBlock = "";
            var currentMode = Modes.TEXT;
            Boolean bIgnoreChar = false;
            Char cIgnoreChar = ' ';
            int iCurrent = 0;

            char c = '0';
            while (streamReader.Peek() >= 0)
            {
                c = (char)streamReader.Read();

                if (bIgnoreChar)
                {
                    if (c == cIgnoreChar)
                    {
                        bIgnoreChar = false;
                        strBlock = "";
                    } // else go on reading ....
                }
                else
                {
                    switch (currentMode)
                    {
                        case Modes.ATTRIBUTE_KEY:
                            {
                                switch (c)
                                {
                                    case '?':   // xml definition
                                        {
                                            currentMode = Modes.TEXT;
                                            bIgnoreChar = true;
                                            cIgnoreChar = '>';
                                            break;
                                        }
                                    case '/':   // break element without text
                                        {
                                            currentElement = currentElement.parentNode;
                                            currentMode = Modes.TEXT;
                                            bIgnoreChar = true;
                                            cIgnoreChar = '>';
                                            break;
                                        }
                                    case '=':
                                        {
                                            currentAttribute = new XmlAttribute();
                                            currentAttribute.strKey = strBlock;
                                            // logConsole((iCurrent) + " " + currentElement.ElementName + ".addAttribute(" + currentAttribute.strKey + ")");
                                            currentElement.addNode(currentAttribute);
                                            strBlock = "";

                                            // goto value string
                                            bIgnoreChar = true;
                                            cIgnoreChar = '"';
                                            currentMode = Modes.ATTRIBUTE_VALUE;
                                            break;
                                        }
                                    default:
                                        {
                                            strBlock += c;
                                            break;
                                        }
                                }
                                break;
                            }
                        case Modes.ATTRIBUTE_VALUE:
                            {
                                switch (c)
                                {
                                    case '"':
                                        {
                                            currentAttribute.strValue = strBlock;
                                            strBlock = "";
                                            currentMode = Modes.ELEMENT;
                                            // atribute done
                                            break;
                                        }
                                    default:
                                        {
                                            strBlock += c;
                                            break;
                                        }
                                }
                                break;
                            }
                        case Modes.ELEMENT:
                            {
                                switch (c)
                                {
                                    case '?': { break; } // ignore question mark
                                    case ' ':
                                        {
                                            if (strBlock.Trim().Length > 0)
                                            {
                                                newElement = new XmlElement(currentElement, strBlock.Trim());
                                                if (currentElement != null)
                                                {
                                                    currentElement.addNode(newElement);
                                                    // logConsole((iCurrent) + " " + currentElement.ElementName+".addElement("+newElement.ElementName+")");
                                                }
                                                else rootElement = newElement;
                                                currentElement = newElement;
                                                strBlock = "";
                                            }
                                            currentMode = Modes.ATTRIBUTE_KEY;
                                            break;
                                        }
                                    case '>':
                                        {
                                            if (strBlock.Trim().Length > 0)
                                            {
                                                newElement = new XmlElement(currentElement, strBlock.Trim());
                                                if (currentElement != null)
                                                {
                                                    currentElement.addNode(newElement);
                                                    // logConsole((iCurrent) + " " + currentElement.ElementName + ".addElement(" + newElement.ElementName + ")");
                                                }
                                                else rootElement = newElement;
                                                currentElement = newElement;
                                                strBlock = "";
                                            }
                                            currentMode = Modes.TEXT;
                                            break;
                                        }
                                    case '/':
                                        {
                                            currentElement = currentElement.parentNode;
                                            currentMode = Modes.TEXT;
                                            bIgnoreChar = true;
                                            cIgnoreChar = '>';
                                            break;
                                        }
                                    default:
                                        {
                                            strBlock += c;
                                            break;
                                        }
                                }
                                break;
                            }
                        default:
                            {
                                switch (c)
                                {
                                    case '<':
                                        {
                                            if (currentElement != null)
                                            {
                                                if (strBlock.Trim().Length > 0)
                                                {
                                                    currentElement.addNode(new XmlText(strBlock));
                                                }
                                                // logConsole((iCurrent) + " " + currentElement.ElementName + ".addText("+strBlock+")");
                                            }
                                            strBlock = "";
                                            currentMode = Modes.ELEMENT;
                                            break;
                                        }
                                    default:
                                        {
                                            strBlock += c;
                                            break;
                                        }
                                }
                                break;
                            }
                    }
                }
                iCurrent++;
            }

            return rootElement;
        }
        #endregion
    }

    #region xml parser helper classes - data definition
    public interface XmlNode
    {
    }

    public class XmlElement : XmlNode
    {
        public XmlElement parentNode = null;
        List<XmlNode> children = new List<XmlNode>();
        public String ElementName = "";
        public XmlElement(XmlElement parent, String name)
        {
            parentNode = parent;
            ElementName = name;
        }

        // encapsulate list functions (for better debugging)
        public void addNode(XmlNode node)
        {
            children.Add(node);
        }
        public List<XmlNode> getAllChildren()
        {
            return children;
        }
        public List<XmlElement> getChildElements()
        {
            List<XmlElement> childElem = new List<XmlElement>();
            foreach (XmlNode node in children)
            {
                if (node is XmlElement)
                    childElem.Add(((XmlElement)node));
            }
            return childElem;
        }
        public List<XmlElement> getChildElements(String strElementName)
        {
            List<XmlElement> childElem = new List<XmlElement>();
            foreach (XmlNode node in children)
            {
                if (node is XmlElement && ((XmlElement)node).ElementName.Equals(strElementName))
                    childElem.Add(((XmlElement)node));
            }
            return childElem;
        }
        public List<XmlAttribute> getAttributes()
        {
            List<XmlAttribute> attributes = new List<XmlAttribute>();
            foreach (XmlNode node in children)
            {
                if (node is XmlAttribute)
                    attributes.Add(((XmlAttribute)node));
            }
            return attributes;
        }
        public XmlAttribute getAttribute(String strKey, String strDefaultReturn)
        {
            List<XmlAttribute> attributes = new List<XmlAttribute>();
            foreach (XmlNode node in children)
            {
                if (node is XmlAttribute && ((XmlAttribute)node).strKey.Equals(strKey))
                    return (XmlAttribute)node;
            }
            XmlAttribute defaultReturn = new XmlAttribute();
            defaultReturn.strKey = strKey;
            defaultReturn.strValue = strDefaultReturn;
            return defaultReturn;
        }
        public String getText(String strDefaultReturn)
        {
            foreach (XmlNode node in children)
            {
                if (node is XmlText)
                    return ((XmlText)node).strText;
            }
            return "";
        }

        public override String ToString()
        {
            String strData = "<" + ElementName + "";
            foreach (XmlNode attribute in children)
            {
                if (attribute is XmlAttribute)
                    strData += " " + attribute.ToString();
            }
            strData += ">";
            foreach (XmlNode element in children)
            {
                if (!(element is XmlAttribute))
                    strData += element.ToString();
            }
            strData += "</" + ElementName + ">";
            return strData;
        }
    }

    public class XmlText : XmlNode
    {
        public String strText = "";
        public XmlText(String text)
        {
            strText = text;
        }

        public override String ToString()
        {
            return strText;
        }
    }

    public class XmlAttribute : XmlNode
    {
        public String strKey = "";
        public String strValue = "";

        public XmlAttribute()
        {
        }

        public override String ToString()
        {
            return strKey + "=\"" + strValue + "\"";
        }
    }
    #endregion
}
