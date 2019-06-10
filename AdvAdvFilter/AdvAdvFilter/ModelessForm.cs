﻿namespace AdvAdvFilter
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;
    using Autodesk.Revit.DB.Events;
    using Autodesk.Revit.UI.Events;

    // using Color = System.Drawing.Color;
    using Point = System.Drawing.Point;
    using Panel = System.Windows.Forms.Panel;
    using FilterMode = AdvAdvFilter.Common.FilterMode;
    using Request = AdvAdvFilter.Common.Request;

    public partial class ModelessForm : System.Windows.Forms.Form
    {
        #region Fields

        #region Fields: Modeless Form

        // Essential API data that is needed
        private ExternalCommandData commandData;
        private UIApplication uiApp;
        private UIDocument uiDoc;
        private Document doc;

        // Element protection
        private ElementProtectionUpdater elementProtectionUpdater;

        // Application of Revit?
        private UIControlledApplication uiCtrlApp;

        // Events and Handlers
        private EventHandler eventHandler;
        private ExternalEvent externalEvent;
        private ManualResetEvent resetEvent;

        private int counterOfAPI = 0;
        private string counterOfAPIstring = string.Empty;

        private bool haltIdlingHandler = false;
        private bool firstStartup = true;

        #endregion Fields: Modeless Form

        #region Fields: Controllers

        // Controls the inner workings of the form and revit 
        DataController dataController;
        RevitController revitController;
        RequestHandler requestHandler;
        // Controls elements visible in modeless form
        ElementSelectionController selectionController;
        OptionController optionController;
        ActionController actionController;

        #endregion Fields: Controllers

        #endregion Fields

        #region Essential Form Methods

        public ModelessForm(
            ExternalCommandData commandData,
            UIControlledApplication uiCtrlApp,
            ExternalEvent externalEvent,
            EventHandler eventHandler,
            ElementProtectionUpdater elementProtectionUpdater,
            List<Element> elementList,
            Main.SetSelection selectionTool
            )
        {
            InitializeComponent();

            //selectionTool(new List<ElementId>());

            // Stop IdlingHandler from executing during initialization
            this.haltIdlingHandler = true;

            // Get all data needed for use on the modeless form
            this.commandData = commandData;
            this.uiApp = this.commandData.Application;
            this.uiDoc = this.uiApp.ActiveUIDocument;
            this.doc = this.uiDoc.Document;
            this.uiCtrlApp = uiCtrlApp;
            this.externalEvent = externalEvent;
            this.eventHandler = eventHandler;
            this.elementProtectionUpdater = elementProtectionUpdater;

            // Initialize Front-end Controllers
            this.selectionController = new ElementSelectionController(
                                            ElementSelectionPanel,
                                            ElementSelectionLabel,
                                            ElementSelectionTreeView);
            OptionVisibilityController optionVisibility = new OptionVisibilityController(
                                            OptionVisibilityPanel,
                                            OptionVisibilityCheckBox);
            OptionFilterController optionFilter = new OptionFilterController(
                                            OptionFilterPanel,
                                            OptionFilterRadioButton0,
                                            OptionFilterRadioButton1,
                                            OptionFilterRadioButton2);
            this.optionController = new OptionController(OptionPanel, optionVisibility, optionFilter);

            ActionAxisController xAxis = new ActionAxisController(
                ActionShiftPanel0, ActionShiftLabel0, ActionShiftTextBox0);

            ActionAxisController yAxis = new ActionAxisController(
                ActionShiftPanel1, ActionShiftLabel1, ActionShiftTextBox1);

            ActionAxisController zAxis = new ActionAxisController(
                ActionShiftPanel2, ActionShiftLabel2, ActionShiftTextBox2);

            ActionModeController mode = new ActionModeController(
                ActionModePanel, ActionModeRadioButton0, ActionModeRadioButton1);

            List<ActionAxisController> xyz = new List<ActionAxisController>() { xAxis, yAxis, zAxis };

            this.actionController = new ActionController(
                ActionPanel, ActionResetButton, ActionShiftButton, ActionPromptLabel, xyz, mode);

            // Initialize Back-end Controllers
            this.revitController = new RevitController(commandData);
            this.dataController = new DataController(this.doc);
            this.requestHandler = new RequestHandler(
                this.revitController,
                this.dataController,
                this.selectionController,
                this.actionController);

            // Get elementList, not sure what to use it for
            elementList = elementList.Where(e => null != e.Category && e.Category.HasMaterialQuantities).ToList();

            // Execute method ModelessForm_FormClosed when the form is closing
            this.FormClosing += this.ModelessForm_FormClosed;

        }

        private void ModelessForm_FormClosed(object sender, FormClosingEventArgs e)
        {
            this.haltIdlingHandler = true;
            e.Cancel = true;
            this.ExecuteWithAPIContext(this.APIClose, null);
        }

        private void ModelessForm_Load(object sender, EventArgs e)
        {
            // Stop IdlingHandler from executing during initialization
            this.haltIdlingHandler = true;

            this.requestHandler.ResetAll();

            this.requestHandler.AddRequest(Request.UpdateTreeView);

            this.requestHandler.AddRequest(Request.UpdateTreeViewSelection);

            // Set up Options
            List<System.EventHandler> visibilityHandler = new List<System.EventHandler>() { OptionVisibilityCheckBox_CheckedChanged };
            List<System.EventHandler> filterHandler = new List<System.EventHandler>()
            {
                OptionFilterRadioButton0_CheckedChanged,
                OptionFilterRadioButton1_CheckedChanged,
                OptionFilterRadioButton2_CheckedChanged
            };
            this.optionController.Reset(visibilityHandler, filterHandler);

            // Initialize action conditions
            bool hideUnselected = this.optionController.GetVisibilityState();
            if (hideUnselected)
            {
                requestHandler.HideUnselected(true);
            }
            else
            {
                requestHandler.ShowAll(true);
            }

            FilterMode filterMode = this.optionController.GetFilterState();
            switch (filterMode)
            {
                case FilterMode.Project:
                    requestHandler.FilterByProject();
                    break;
                case FilterMode.View:
                    requestHandler.FilterByView();
                    break;
                case FilterMode.Selection:
                    requestHandler.FilterBySelection();
                    break;
                default:
                    throw new InvalidEnumArgumentException("Error: filterMode is not Project, View, or Selection in this.optionController.GetFilterState()");
            }

            // Begin first start up loop (where TreeStructure gets all its elements from the document)
            this.firstStartup = true;

            // Set up actionController
            this.actionController.Reset();

            // Resume IdlingHandler
            this.haltIdlingHandler = false;
        }

        #endregion Essential Form Methods

        #region EventHandlers

        #region EventHandlers: ElementSelection

        private void ElementSelectionTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            // .. Don't know what to use it for yet, keeping it here just in case
        }

        private void ElementSelectionTreeView_BeforeCollapse(object sender, TreeViewCancelEventArgs e)
        {
            BranchTreeNode branch = e.Node as BranchTreeNode;

            if (branch != null)
            {
                if (branch.Parent == null) // This is for "CategoryType" Only
                {
                    selectionController.ToggleCollapse(branch);
                    e.Cancel = true;
                }
            }
        }

        private void ElementSelectionTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action == TreeViewAction.Unknown)
                return;

            this.haltIdlingHandler = true;

            selectionController.UpdateAfterCheck(e.Node as AdvTreeNode);

            selectionController.UpdateTotals();

            requestHandler.AddRequest(Request.SelectElementIds);

            this.haltIdlingHandler = false;
        }

        #endregion EventHandlers: ElementSelection

        #region EventHandlers: Option

        private void OptionVisibilityCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            this.haltIdlingHandler = true;

            if (OptionVisibilityCheckBox.Checked)
            {
                requestHandler.HideUnselected();
            }
            else
            {
                requestHandler.ShowAll();
            }

            this.haltIdlingHandler = false;
        }

        private void OptionFilterRadioButton0_CheckedChanged(object sender, EventArgs e)
        {
            this.haltIdlingHandler = true;

            FilterMode filterMode = optionController.GetFilterState();
            OptionFilterRadioButton_ChangeFilter(filterMode);

            this.haltIdlingHandler = false;
        }

        private void OptionFilterRadioButton1_CheckedChanged(object sender, EventArgs e)
        {
            this.haltIdlingHandler = true;

            FilterMode filterMode = optionController.GetFilterState();
            OptionFilterRadioButton_ChangeFilter(filterMode);

            this.haltIdlingHandler = false;
        }

        private void OptionFilterRadioButton2_CheckedChanged(object sender, EventArgs e)
        {
            this.haltIdlingHandler = true;

            FilterMode filterMode = optionController.GetFilterState();
            OptionFilterRadioButton_ChangeFilter(filterMode);

            this.haltIdlingHandler = false;
        }

        private void OptionFilterRadioButton_ChangeFilter(FilterMode filterMode)
        {
            if (requestHandler.FilterBy == filterMode)
                return;

            switch (filterMode)
            {
                case FilterMode.Project:
                    requestHandler.FilterByProject();
                    break;
                case FilterMode.View:
                    requestHandler.FilterByView();
                    break;
                case FilterMode.Selection:
                    requestHandler.FilterBySelection();
                    break;
                default:
                    throw new InvalidEnumArgumentException("Error: filterMode is not Project, View, or Selection in this.optionController.GetFilterState()");
            }
        }

        #endregion EventHandlers: Option

        #region EventHandlers: Action

        private void ActionShiftButton_Click(object sender, EventArgs e)
        {
            this.haltIdlingHandler = true;

            requestHandler.AddRequest(Request.ShiftSelected);

            this.haltIdlingHandler = false;
        }

        private void ActionResetButton_Click(object sender, EventArgs e)
        {
            this.actionController.Reset();
        }

        private void ActionShiftTextBox0_TextChanged(object sender, EventArgs e)
        {
            ActonShiftTextBox_HandleTextChanged(ActionShiftTextBox0);
        }

        private void ActionShiftTextBox1_TextChanged(object sender, EventArgs e)
        {
            ActonShiftTextBox_HandleTextChanged(ActionShiftTextBox1);
        }

        private void ActionShiftTextBox2_TextChanged(object sender, EventArgs e)
        {
            ActonShiftTextBox_HandleTextChanged(ActionShiftTextBox2);
        }

        private void ActonShiftTextBox_HandleTextChanged(System.Windows.Forms.TextBox textBox)
        {
            bool parseSuccessful;
            string text = textBox.Text;

            parseSuccessful = Int32.TryParse(text, out int value);

            if (parseSuccessful)
                textBox.ForeColor = System.Drawing.Color.Black;
            else
                textBox.ForeColor = System.Drawing.Color.Red;

            if (this.actionController.IsAllAxisEmpty()
                && this.actionController.IsModeDefault())
            {
                this.actionController.DisableShift();
                this.actionController.DisableDefaults();
            }
            else
            {
                this.actionController.EnableDefaults();

                if (actionController.TryGetAllInputs(out List<int> coords, out bool shift))
                {
                    this.actionController.EnableShift();
                }
                else
                {
                    this.actionController.DisableShift();
                }
            }
        }

        #endregion EventHandlers: Action

        #endregion EventHandlers

        #region Supplementary Form Methods

        #endregion Supplementary Form Methods

        #region API CALLS

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        /// <summary>
        /// The event while the app is idling (checks what is selected)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public void UIAppEvent_IdlingEventHandler(object sender, IdlingEventArgs args)
        {
            try
            {
                args.SetRaiseWithoutDelay();

                if (!this.haltIdlingHandler)
                {
                    // This is to get the essential objects to use the Revit API
                    UIApplication uiApp = sender as UIApplication;
                    UIDocument uiDoc = uiApp.ActiveUIDocument;
                    Document activeDoc = uiDoc.Document;

                    if (this.firstStartup)
                    {
                        this.firstStartup = false;

                        List<ElementId> AllElementIds = revitController.GetAllElementIds(FilterMode.Project);                        
                        dataController.SetAllElements(AllElementIds);
                    }

                    Request request;
                    FilterMode filter;

                    filter = requestHandler.FilterBy;

                    int viewChanged = revitController.UpdateView();
                    if (viewChanged == 1)
                    {
                        request = Request.UpdateTreeView;
                    }
                    else
                    {
                        request = requestHandler.GetRequest();
                    }

                    request = requestHandler.ProcessRequest(request);

                    switch (request)
                    {
                        case Request.UpdateTreeView:

                            dataController.SetMode(filter);

                            // Update the treeView element within the form
                            this.BeginInvoke(new Action(() =>
                            {
                                // selectionController.UpdateTreeViewStructure(dataController.AllElements, revitController);
                                selectionController.UpdateTreeViewStructure_New(dataController.ElementTree.SubSet, dataController.ElementTree);
                            }));

                            break;
                        case Request.UpdateTreeViewSelection:

                            ICollection<ElementId> currentSelected = this.uiDoc.Selection.GetElementIds();
                            this.BeginInvoke(new Action(() =>
                            {
                                bool updateSucceeded = selectionController.UpdateSelectedLeaves(currentSelected);
                                if (updateSucceeded)
                                {
                                    requestHandler.FailureListRemove(request);
                                }
                                else
                                {
                                    requestHandler.AttemptRecovery(request);
                                }
                            }));

                            break;
                        case Request.SelectElementIds:

                            revitController.MakeNewSelection(dataController.SelElements);

                            //List<ElementId> elementIds
                            //    = revitController.GetAllElementIds(filter);
                            //dataController.UpdateAllElements(elementIds);

                            dataController.SetMode(filter);

                            // Hide all elements 
                            if (requestHandler.UnselectedHidden)
                            {
                                revitController.HideUnselectedElementIds(
                                    dataController.SelElements,
                                    dataController.AllElements);
                            }
                            else
                            {
                                revitController.ShowSelectedElementIds(dataController.SelElements);
                            }

                            break;
                        case Request.ShiftSelected:

                            List<ElementId> movElementIds = dataController.MovElements;
                            List<int> coords = dataController.Coords;
                            bool copyAndShift = dataController.CopyAndShift;

                            // Copy and Move the Elements
                            revitController.CopyAndMoveElements(movElementIds, coords, copyAndShift);

                            break;
                        case Request.Nothing:
                            // Do absolutely nothing
                            break;
                        default:

                            // If the request isn't any other request (even Nothing), then prompt user with warning message
                            TaskDialog.Show("Debug - SelectionChanged_UIAppEvent_WhileIdling",
                                "Warning: Handler given invalid request");

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorReport.Report(ex);
            }
        }

        #region Idling API Handler Methods

        private void HandleHideUnselectedElementIds(List<ElementId> selElementIds)
        {
            // revitController.HideUnselectedElementIds(selElementIds);

            ICollection<ElementId> ids = new FilteredElementCollector(this.doc).OfClass(typeof(FamilyInstance)).ToElementIds();

            List<ElementId> hideIds = new List<ElementId>();
            foreach (var id in ids)
            {
                if (!selElementIds.Contains(id))
                {
                    hideIds.Add(id);
                }
            }

            using (var tran = new Transaction(doc, "Test"))
            {
                tran.Start();

                View3D view = revitController.UiDoc.ActiveView as View3D;
                if (view != null)
                {
                    view.HideElements(hideIds);
                }

                tran.Commit();
            }

            /*
            List<ElementId> docElementIds = GetDocumentElementIds(FilterMode.Project);
            Element element;

            foreach (ElementId eId in docElementIds)
            {
                if (selElementIds.Contains(eId))
                {
                    selElementIds.Remove(eId);
                    continue;
                }

                element = this.doc.GetElement(eId);
                if (element.CanBeHidden())
            }
            */

        }

        #endregion Idling API Handler Methods

        public void AppEvent_DocChangedEventHandler(object sender, DocumentChangedEventArgs args)
        {
            this.haltIdlingHandler = true;

            ICollection<ElementId> addedElements =  args.GetAddedElementIds();
            ICollection<ElementId> deletedElements = args.GetDeletedElementIds();

            // Add elements to TreeStructure
            this.dataController.AddToAllElements(addedElements.ToList());
            // Remove elements to TreeStructure
            this.dataController.RemoveFromAllElements(deletedElements.ToList());

            requestHandler.AddRequest(Request.UpdateTreeView);

            this.haltIdlingHandler = false;
            return;
        }

        #region Not Need Attention

        /// <summary>
        /// executes the function passed and the object parameter
        /// </summary>
        /// <param name="actionToExecute"> the action executed </param>
        /// <param name="args"> the event arguments </param>
        /// <param name="extendWait"> if we are extending the wait </param>
        private void ExecuteWithAPIContext(Action<UIApplication, object> actionToExecute, object args = null, bool extendWait = false)
        {
            this.counterOfAPI += 1;
            this.counterOfAPIstring += actionToExecute.Method.Name + "\n";

            this.haltIdlingHandler = true;
            bool executed = false;

            // Prevent user from stealing focus from Revit
            this.SetStyle(ControlStyles.Selectable, false);

            this.resetEvent = new ManualResetEvent(false);

            this.eventHandler.SetActionAndRaise(actionToExecute, this.resetEvent, args);

            // Try to force execution of event.
            // Adapted from Jo Ye, ACE DevBlog.
            // http://adndevblog.typepad.com/aec/2013/07/tricks-to-force-trigger-idling-event.html
            SetForegroundWindow(this.Handle);

            SetForegroundWindow(Autodesk.Windows.ComponentManager.ApplicationWindow);

            Cursor.Position = new System.Drawing.Point(Cursor.Position.X + 1, Cursor.Position.Y + 1);
            Cursor.Position = new System.Drawing.Point(Cursor.Position.X - 1, Cursor.Position.Y - 1);

            if (extendWait)
            {
                executed = this.resetEvent.WaitOne();
            }
            else
            {
                executed = this.resetEvent.WaitOne(1000);

                for (int i = 0; (i < 4 && !executed); i++)
                {
                    SetForegroundWindow(this.Handle);

                    SetForegroundWindow(Autodesk.Windows.ComponentManager.ApplicationWindow);

                    Cursor.Position = new Point(Cursor.Position.X + 1, Cursor.Position.Y + 1);
                    Cursor.Position = new Point(Cursor.Position.X - 1, Cursor.Position.Y - 1);

                    executed = this.resetEvent.WaitOne(1000);

                    if (this.eventHandler.Executing)
                    {
                        executed = this.resetEvent.WaitOne();
                        break;
                    }
                }
            }

            if (!executed)
                throw new TimeoutException(string.Format("Execution of {0} failed after multiple attempts.\n", actionToExecute.Method.Name));

            this.SetStyle(ControlStyles.Selectable, false);

            this.resetEvent.Reset();

            this.Focus();

            this.haltIdlingHandler = false;
        }

        /// <summary>
        /// Closes the Form
        /// </summary>
        /// <param name="uiApp"> the app </param>
        /// <param name="args"> the event args </param>
        private void APIClose(UIApplication uiApp, object args)
        {
            try
            {
                //// if closing the form then use this:
                this.resetEvent.Set();
                this.elementProtectionUpdater.ProtectedElementIds.Clear();

                // terminate event handler for SelectionChanged_UIAppEvent_WhileIdling
                Main.UiCtrlApp.Idling -= Main.ActiveModelessForm.UIAppEvent_IdlingEventHandler;

                this.Invoke(new Action(() =>
                {
                    Application.ExitThread();
                }));
            }
            catch (Exception ex)
            {
                ErrorReport.Report(ex);
            }
        }

        /// <summary>
        /// modifications to the form
        /// </summary>
        /// <param name="uiApp"> the app </param>
        /// <param name="args"> the event args </param>
        private void APIEdit(UIApplication uiApp, object args)
        {
            try
            {
                //// for any modifications to the form
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                Document activeDoc = uiDoc.Document;
                this.doc = activeDoc;
                Element elem = args as Element;

                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(new Color(0, 255, 0));
                ogs.SetCutFillColor(new Color(0, 255, 0));
                ogs.SetProjectionFillColor(new Color(0, 255, 0));
                ogs.SetProjectionLineColor(new Color(0, 255, 0));

                using (Transaction framePanelTransaction = new Transaction(activeDoc, "t1"))
                {
                    framePanelTransaction.Start("t1");

                    activeDoc.ActiveView.SetElementOverrides(elem.Id, ogs);

                    //// refresh everything:
                    activeDoc.Regenerate();
                    framePanelTransaction.Commit();
                }
            }
            catch (Exception ex)
            {
                ErrorReport.Report(ex);
            }
        }

        /// <summary>
        /// highlights the elements in the list passed in the args parameter
        /// </summary>
        /// <param name="uiApp"> the app </param>
        /// <param name="args"> the event args </param>
        private void APIHighlight(UIApplication uiApp, object args)
        {
            try
            {
                UIDocument uiDoc = uiApp.ActiveUIDocument;
                List<ElementId> elementsToHighlight = args as List<ElementId>;

                if (elementsToHighlight.Count > 0)
                {
                    uiDoc.Selection.SetElementIds(elementsToHighlight);
                }

                this.resetEvent.Set();
            }
            catch (Exception ex)
            {
                ErrorReport.Report(ex);
            }
        }

        #endregion

        #endregion

    }
}
