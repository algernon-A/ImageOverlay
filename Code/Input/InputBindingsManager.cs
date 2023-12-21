// <copyright file="InputBindingsManager.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System.Collections.Generic;
    using Colossal.Logging;
    using Game.Input;
    using HarmonyLib;
    using UnityEngine.InputSystem;

    /// <summary>
    /// Management of input bindings.
    /// </summary>
    [HarmonyPatch]
    internal class InputBindingsManager
    {
        private const string MapName = "Shortcuts";

        private readonly ILog _log;
        private readonly Dictionary<string, InputAction> _actions;
        private readonly Dictionary<string, ProxyBinding> _bindings;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputBindingsManager"/> class.
        /// </summary>
        private InputBindingsManager()
        {
            _log = Mod.Instance.Log;
            _actions = new ();
            _bindings = new ();

            // Register custom binding.
            InputSystem.RegisterBindingComposite<ButtonWithThreeModifiers>();
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
        /// Harmony prefix for <c>InputManager.SetBindingImpl</c> to implement custom proxy handling.
        /// </summary>
        /// <param name="newBinding">New binding.</param>
        /// <param name="result">Binding result.</param>
        /// <returns><c>false</c> if the binding update was intercepted here, <c>true</c> otherwise.</returns>
        [HarmonyPatch(typeof(InputManager), "SetBindingImpl")]
        [HarmonyPrefix]
        internal static bool SetBindingImplPrefix(ProxyBinding newBinding, ref ProxyBinding result)
        {
            // Check if this is one of ours.
            if (newBinding.m_MapName.Equals(MapName) && Instance is not null && Instance._bindings.ContainsKey(newBinding.m_ActionName) && Instance._actions.TryGetValue(newBinding.m_ActionName, out InputAction selectedAction))
            {
                Instance._log.Info($"updating binding for action {newBinding.m_ActionName}");

                // Apply new binding.
                selectedAction.ChangeBinding(0).Erase();
                switch (newBinding.m_Modifiers.Count)
                {
                    default:
                    case 0:
                        selectedAction.AddBinding(newBinding.path);
                        break;
                    case 1:
                        selectedAction.AddCompositeBinding("ButtonWithOneModifier").With("modifier", newBinding.m_Modifiers[0].m_Path).With("button", newBinding.path);
                        break;
                    case 2:
                        selectedAction.AddCompositeBinding("ButtonWithTwoModifiers").With("modifier1", newBinding.m_Modifiers[0].m_Path).With("modifier2", newBinding.m_Modifiers[1].m_Path).With("button", newBinding.path);
                        break;
                    case 3:
                        selectedAction.AddCompositeBinding("ButtonWithThreeModifiers").With("modifier1", newBinding.m_Modifiers[0].m_Path).With("modifier2", newBinding.m_Modifiers[1].m_Path).With("modifier3", newBinding.m_Modifiers[2].m_Path).With("button", newBinding.path);
                        break;
                }

                // Update record.
                Instance._bindings[newBinding.m_ActionName] = newBinding;
                result = newBinding;

                return false;
            }

            return true;
        }

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
            InputActionMap actionMap = InputManager.instance.inputActions.FindActionMap(MapName);
            InputAction newAction = actionMap.AddAction(actionName, InputActionType.Button, expectedControlLayout: "Button");

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

            // Crate proxy mapping.
            ProxyActionMap proxyMap = InputManager.instance.FindActionMap(MapName);
            List<ProxyModifier> proxyModifiers = new (modifiers.Count);
            foreach (string modifier in modifiers)
            {
                proxyModifiers.Add(new ProxyModifier() { m_Path = modifier });
            }

            _bindings.Add(actionName, new ProxyBinding()
            {
                m_MapName = MapName,
                m_ActionName = actionName,
                m_CompositeName = "Keyboard",
                m_Name = "binding",
                m_IsRebindable = true,
                m_AllowModifiers = true,
                m_CanBeEmpty = true,
                m_Usage = BindingUsage.Default | BindingUsage.Overlay | BindingUsage.Tool | BindingUsage.CancelableTool | BindingUsage.Editor,
                m_Modifiers = proxyModifiers,
                group = "Keyboard",
                path = path,
            });
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
