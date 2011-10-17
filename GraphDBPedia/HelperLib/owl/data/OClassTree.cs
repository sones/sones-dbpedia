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

    public class OClassTree
    {
        HashSet<OClass> looseBranches;
        List<OClass> rootClasses;

        public OClassTree()
        {
            #region inital setup
            rootClasses = new List<OClass>();
            #endregion
        }

        public bool AddToTree(OClass ontologyClass)
        {
            bool bAdded = false;
            OClass currentRoot = null;

            if (ontologyClass.IsSubClassOf != null)
            {
                foreach (OClass root in rootClasses)
                {
                    if (root.ContainsOntologyClass(ontologyClass.IsSubClassOf))
                    {
                        currentRoot = root;
                        bAdded = currentRoot.AddOClassChild(ontologyClass);
                        if (bAdded) break;
                    }
                }
            }

            if (!bAdded)
            {
                #region otherwise create new root entry
                rootClasses.Add(ontologyClass);
                currentRoot = ontologyClass;
                bAdded = true;
                #endregion
            }

            if (currentRoot == null)
            {
                return false;
            }

            if (bAdded && currentRoot != null)
            {
                // cross check tree structure vs. updated tree
                List<OClass> lClassesToRemove = new List<OClass>();
                foreach (OClass otherRoot in rootClasses)
                {
                    if (!currentRoot.Equals(otherRoot) && otherRoot.IsSubClassOf != null && currentRoot.ContainsOntologyClass(otherRoot.IsSubClassOf))
                    {
                        currentRoot.AddOClassChild(otherRoot);
                        lClassesToRemove.Add(otherRoot);
                    }
                }

                foreach (OClass classToRemove in lClassesToRemove)
                {
                    this.RemoveFromTree(classToRemove);
                }
            }
            else
            {
                Console.WriteLine("should not happen.");
            }

            return bAdded;
        }

        public bool RemoveFromTree(OClass oClassToRemove)
        {
            #region if class-to-remove is a root-element
            if (rootClasses.Contains(oClassToRemove))
            {
                return rootClasses.Remove(oClassToRemove);
            }
            #endregion

            #region if class-to-remove is nested into tree structure - walk through and delete within
            foreach (OClass rootClass in rootClasses)
            {
                if (rootClass.ContainsOntologyClass(oClassToRemove))
                {
                    return rootClass.RemoveOClassChild(oClassToRemove);
                }
            }
            #endregion

            return false;
        }

        public OClass GetOntologyClass(OClass refOClass)
        {
            OClass retClass;
            foreach (OClass rootClass in rootClasses)
            {
                retClass = rootClass.GetOntologyClass(refOClass);
                if (retClass != null)
                    return retClass;
            }
            return null;
        }

        public int GetOntologyClassLevel(OClass refOClass)
        {
            int iLevel;
            foreach (OClass rootClass in rootClasses)
            {
                iLevel = rootClass.GetOntologyClassLevel(refOClass);
                if (iLevel >= 0)
                    return iLevel;
            }
            return -1;
        }

        public List<OClass> GetAllClasses()
        {
            List<OClass> classes = new List<OClass>();
            if (rootClasses != null)
            {
                foreach (OClass current in rootClasses)
                {
                    foreach (OClass currentRoot in current.GetAllClasses())
                    {
                        classes.Add(currentRoot);
                    }
                }
            }

            if (looseBranches != null)
            {
                foreach (OClass current in looseBranches)
                {
                    foreach (OClass currentLoose in current.GetAllClasses())
                    {
                        classes.Add(currentLoose);
                    }
                }
            }
            return classes;
        }


        public void GetAllOClassesGql(Action<string> ExecuteCommand, String[] postFix)
        {
            StringBuilder sbGql;
            foreach (OClass currentClass in rootClasses)
            {
                sbGql = new StringBuilder("CREATE VERTEX TYPES ");

                bool bFirst = true;
                foreach (OClass current in currentClass.ToList())
                {
                    if (bFirst) bFirst = false;
                    else sbGql.Append(", ");
                    sbGql.Append(current.GetAllOClassesGql(postFix));
                }
                ExecuteCommand(sbGql.ToString());
            }
        }
    }
}
