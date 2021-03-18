using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using GraphDataStore = UnityEditor.ShaderGraph.DataStore<UnityEditor.ShaderGraph.GraphData>;
using BlackboardItem = UnityEditor.ShaderGraph.Internal.ShaderInput;
using BlackboardItemController = UnityEditor.ShaderGraph.Drawing.ShaderInputViewController;

namespace UnityEditor.ShaderGraph.Drawing
{
    class MoveShaderInputAction : IGraphDataAction
    {
        void MoveShaderInput(GraphData graphData)
        {
            AssertHelpers.IsNotNull(graphData, "GraphData is null while carrying out MoveShaderInputAction");
            AssertHelpers.IsNotNull(shaderInputReference, "ShaderInputReference is null while carrying out MoveShaderInputAction");
            graphData.owner.RegisterCompleteObjectUndo("Move Graph Input");
            switch (shaderInputReference)
            {
                case AbstractShaderProperty property:
                    graphData.MoveProperty(property, newIndexValue);
                    break;
                case ShaderKeyword keyword:
                    graphData.MoveKeyword(keyword, newIndexValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Action<GraphData> modifyGraphDataAction =>  MoveShaderInput;

        // Reference to the shader input being modified
        internal ShaderInput shaderInputReference { get; set; }

        internal int newIndexValue { get; set; }
    }

    class BlackboardCategoryController : SGViewController<CategoryData, BlackboardCategoryViewModel>
    {
        internal SGBlackboardCategory blackboardCategoryView => m_BlackboardCategoryView;
        SGBlackboardCategory m_BlackboardCategoryView;
        Dictionary<string, BlackboardItemController> m_BlackboardItemControllers = new Dictionary<string, ShaderInputViewController>();
        SGBlackboard blackboard { get; set; }


        internal BlackboardCategoryController(CategoryData categoryData, BlackboardCategoryViewModel categoryViewModel, GraphDataStore dataStore)
            : base(categoryData, categoryViewModel, dataStore)
        {
            m_BlackboardCategoryView = new SGBlackboardCategory(categoryViewModel);

            blackboard = categoryViewModel.parentView as SGBlackboard;
            if (blackboard == null)
                return;

            blackboard.Add(m_BlackboardCategoryView);
            // These make sure that the drag indicators are disabled whenever a drag action is cancelled without completing a drop
            blackboard.RegisterCallback<MouseUpEvent>(evt =>
            {
                m_BlackboardCategoryView.OnDragActionCanceled();
            });
            blackboard.hideDragIndicatorAction += m_BlackboardCategoryView.OnDragActionCanceled;

            foreach (var shaderInput in dataStore.State.properties)
            {
                if (IsInputInCategory(shaderInput))
                    InsertBlackboardRow(shaderInput);
            }

            foreach (var shaderInput in dataStore.State.keywords)
            {
                if (IsInputInCategory(shaderInput))
                    InsertBlackboardRow(shaderInput);
            }
        }

        protected override void RequestModelChange(IGraphDataAction changeAction)
        {
            DataStore.Dispatch(changeAction);
        }

        // Called by GraphDataStore.Subscribe after the model has been changed
        protected override void ModelChanged(GraphData graphData, IGraphDataAction changeAction)
        {
            // If categoryData associated with this controller is removed by an operation, destroy controller and views associated
            if (graphData.DoesCategoryExist(ViewModel.associatedCategoryGuid) == false)
            {
                this.Destroy();
                return;
            }

            switch (changeAction)
            {
                // If newly added input doesn't belong to any of the user-made categories, add it to the appropriate default category
                case AddShaderInputAction addBlackboardItemAction:
                    if (IsInputInCategory(addBlackboardItemAction.shaderInputReference))
                    {
                        var blackboardRow = FindBlackboardRow(addBlackboardItemAction.shaderInputReference);
                        // Rows should auto-expand when an input is first added
                        // blackboardRow.expanded = true;
                        var propertyView = blackboardRow.Q<BlackboardPropertyView>();
                        if (addBlackboardItemAction.addInputActionType == AddShaderInputAction.AddActionSource.AddMenu)
                            propertyView.OpenTextEditor();
                    }
                    break;
                case DeleteShaderInputAction deleteShaderInputAction:
                    foreach (var shaderInput in deleteShaderInputAction.shaderInputsToDelete)
                    {
                        if (IsInputInCategory(shaderInput))
                            RemoveBlackboardRow(shaderInput);
                    }
                    break;
                case HandleUndoRedoAction handleUndoRedoAction:
                    foreach (var shaderInput in graphData.removedInputs)
                        if (IsInputInCategory(shaderInput))
                            RemoveBlackboardRow(shaderInput);

                    foreach (var shaderInput in graphData.addedInputs)
                        if (IsInputInCategory(shaderInput))
                            InsertBlackboardRow(shaderInput);

                    break;
                case CopyShaderInputAction copyShaderInputAction:
                    if (IsInputInCategory(copyShaderInputAction.copiedShaderInput))
                        InsertBlackboardRow(copyShaderInputAction.copiedShaderInput, copyShaderInputAction.insertIndex);
                    break;
                case AddItemToCategoryAction addItemToCategoryAction:
                    if (addItemToCategoryAction.categoryGuid == ViewModel.associatedCategoryGuid)
                    {
                        InsertBlackboardRow(addItemToCategoryAction.itemToAdd);
                    }
                    else
                    {
                        // If the added input has been added to a category other than this one, and it used to belong to this category,
                        // Then cleanup the controller and view that used to represent that input
                        foreach (var key in m_BlackboardItemControllers.Keys)
                        {
                            var blackboardItemController = m_BlackboardItemControllers[key];
                            if (blackboardItemController.Model == addItemToCategoryAction.itemToAdd)
                            {
                                blackboardItemController.Destroy();
                                break;
                            }
                        }
                    }
                    break;
            }

            // If a non-named category ever reaches 0 children, remove it from the blackboard
            if (Model.childCount == 0 && Model.IsNamedCategory() == false)
                this.Destroy();
        }

        internal bool IsInputInCategory(ShaderInput shaderInput)
        {
            return Model != null && Model.IsItemInCategory(shaderInput);
        }

        internal SGBlackboardRow FindBlackboardRow(ShaderInput shaderInput)
        {
            m_BlackboardItemControllers.TryGetValue(shaderInput.objectId, out var associatedController);
            return associatedController?.BlackboardItemView;
        }

        // Creates controller, view and view model for a blackboard item and adds the view to the specified index in the category
        // By default adds it to the end of the list if no insertionIndex specified
        internal SGBlackboardRow InsertBlackboardRow(BlackboardItem shaderInput, int insertionIndex = -1)
        {
            // If no index specified, add to end of category
            if (insertionIndex == -1)
                insertionIndex = m_BlackboardItemControllers.Count;

            var shaderInputViewModel = new ShaderInputViewModel()
            {
                model = shaderInput,
                parentView = blackboardCategoryView,
            };
            var blackboardItemController = new BlackboardItemController(shaderInput, shaderInputViewModel, DataStore);

            m_BlackboardItemControllers.TryGetValue(shaderInput.objectId, out var existingItemController);
            if (existingItemController == null)
            {
                m_BlackboardItemControllers.Add(shaderInput.objectId, blackboardItemController);
                blackboardCategoryView.Insert(insertionIndex, blackboardItemController.BlackboardItemView);
                return blackboardItemController.BlackboardItemView;
            }
            else
            {
                AssertHelpers.Fail("Tried to add blackboard item that already exists to category.");
                return null;
            }
        }

        internal void RemoveBlackboardRow(BlackboardItem shaderInput)
        {
            BlackboardItemController associatedBlackboardItemController = null;
            m_BlackboardItemControllers.TryGetValue(shaderInput.objectId, out associatedBlackboardItemController);

            if (associatedBlackboardItemController != null)
            {
                associatedBlackboardItemController.Destroy();
                m_BlackboardItemControllers.Remove(shaderInput.objectId);
            }
            else
            {
                Debug.Log("ERROR: Failed to find associated blackboard item controller for shader input that was just deleted. Cannot clean up view associated with input.");
            }
        }

        public override void Destroy()
        {
            m_BlackboardCategoryView?.RemoveFromHierarchy();
            foreach (var key in m_BlackboardItemControllers.Keys)
            {
                var blackboardItemController = m_BlackboardItemControllers[key];
                blackboardItemController.Destroy();
            }
        }
    }
}
