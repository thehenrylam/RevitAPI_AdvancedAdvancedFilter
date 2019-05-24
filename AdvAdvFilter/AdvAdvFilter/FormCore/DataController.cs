﻿namespace AdvAdvFilter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;

    /// <summary>
    /// DataController acts as a layer between the ModelessForm and the Revit Software
    ///     to make it more convenient to update and retrieve data, and provides
    ///     information if the update has made any significant change to the data.
    /// </summary>
    class DataController
    {
        #region Fields

        private List<ElementId> allElements;
        private List<ElementId> selElements;

        #endregion Fields

        #region Parameters

        public List<ElementId> AllElements
        {
            get { return this.allElements; }
        }

        public List<ElementId> SelElements
        {
            get { return this.selElements; }
        }

        #endregion Parameters

        public DataController()
        {
            this.allElements = null;
            this.selElements = null;
        }

        public bool UpdateAllElements(List<ElementId> newAllElements)
        {
            bool listChanged = false;

            if ((newAllElements == null) && (this.allElements == null))
            {
                listChanged = false;
            }
            else if (((newAllElements == null) && (this.allElements != null))
                || ((newAllElements != null) && (this.allElements == null)))
            {
                listChanged = true;
            }
            else
            {
                // listChanged = (!newAllElements.All(this.allElements.Contains));
                listChanged = (!newAllElements.SequenceEqual(this.allElements));
            }

            if (listChanged)
                this.allElements = newAllElements;

            return listChanged;
        }

        public bool UpdateSelElements(List<ElementId> newSelElements)
        {
            bool listChanged = (!newSelElements.All(this.selElements.Contains));

            if (listChanged)
                this.selElements = newSelElements;

            return listChanged;
        }





    }
}
