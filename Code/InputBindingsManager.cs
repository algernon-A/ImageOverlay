// <copyright file="InputBindingsManager.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System.Collections.Generic;
    using Colossal.Logging;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Management of input bindings.
    /// </summary>
    internal class InputBindingsManager
    {
        private readonly ILog _log;
        private readonly Dictionary<string, InputAction> _actions;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputBindingsManager"/> class.
        /// </summary>
        private InputBindingsManager()
        {
            _log = Mod.Instance.Log;
            _actions = new ();
        }

        /// <summary>
        /// Action callback delegate.
        /// </summary>
        internal delegate void Callback();

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        internal static InputBindingsManager Instance { get; private set; }

        /// <summary>
        /// Ensures an active instance.
        /// </summary>
        internal static void Ensure() => Instance ??= new ();

        /// <summary>
        /// Creates a new input action.
        /// </summary>
        /// <param name="actionName">Action name (must be unique).</param>
        /// <param name="path">Action path.</param>
        /// <param name="modifiers">Action modifiers (empty or <c>null</c> if none).</param>
        /// <param name="callback">Action callback delegate.</param>
        internal void AddAction(string actionName, string path, List<string> modifiers, Callback callback)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                _log.Error("attempt to add null or empty action name");
                return;
            }

            if (_actions.ContainsKey(actionName))
            {
                _log.Error($"attempt to add duplicate action key for {actionName}");
                return;
            }

            // Create action.
            _log.Info($"adding action key for {actionName}");
            InputAction newAction = new (actionName, InputActionType.Button);

            // Callback.
            if (callback is not null)
            {
                newAction.performed += (c) => callback();
            }

            // Check for and implement any modifiers.
            switch (modifiers?.Count ?? 0)
            {
                default:
                case 0:
                    newAction.AddBinding(path);
                    break;
                case 1:
                    newAction.AddCompositeBinding("ButtonWithOneModifier").With("modifier", modifiers[0]).With("button", path);
                    break;
                case 2:
                    newAction.AddCompositeBinding("ButtonWithTwoModifiers").With("modifier1", modifiers[0]).With("modifier2", modifiers[1]).With("button", path);
                    break;
                case 3:
                    newAction.AddCompositeBinding("ButtonWithThreeModifiers").With("modifier1", modifiers[0]).With("modifier2", modifiers[1]).With("modifier3", modifiers[2]).With("button", path);
                    break;
            }

            // Add to dictionary.
            _actions.Add(actionName, newAction);
        }

        /// <summary>
        /// Enables all input actions.
        /// </summary>
        internal void EnableActions()
        {
            foreach (InputAction action in _actions.Values)
            {
                action.Enable();
            }
        }

        /// <summary>
        /// Disables all input actions.
        /// </summary>
        internal void DisableActions()
        {
            foreach (InputAction action in _actions.Values)
            {
                action.Disable();
            }
        }
    }
}
