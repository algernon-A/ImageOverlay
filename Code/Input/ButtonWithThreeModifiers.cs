// <copyright file="ButtonWithThreeModifiers.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Layouts;
    using UnityEngine.InputSystem.Utilities;

    /// <summary>
    /// Three-modifier input binding composite.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Unity InputSystem")]
    [DisplayStringFormat("{modifier1}+{modifier2}+{modifier3}+{button}")]
    public class ButtonWithThreeModifiers : InputBindingComposite<float>
    {
        /// <summary>
        /// First modifier.
        /// </summary>
        [InputControl(layout = "Button")]
        public int modifier1;

        /// <summary>
        /// Second modifier.
        /// </summary>
        [InputControl(layout = "Button")]
        public int modifier2;

        /// <summary>
        /// Third modifier.
        /// </summary>
        [InputControl(layout = "Button")]
        public int modifier3;

        /// <summary>
        /// Action button..
        /// </summary>
        [InputControl(layout = "Button")]
        public int button;

        private bool _timelessModifiers;

        /// <summary>
        /// Reads the current input value.
        /// </summary>
        /// <param name="context">Active input binding context.</param>
        /// <returns>Current input value.</returns>
        public override float ReadValue(ref InputBindingCompositeContext context)
        {
            // Check modifiers.
            if (AreModifiersPressed(ref context))
            {
                return context.ReadValue<float>(button);
            }

            // If we got here then the action wasn't pressed; return zero.
            return 0f;
        }

        /// <summary>
        /// Evaluates the action result magnitude.
        /// </summary>
        /// <param name="context">Active input binding context.</param>
        /// <returns>Result magnitude.</returns>
        public override float EvaluateMagnitude(ref InputBindingCompositeContext context) => ReadValue(ref context);

        /// <summary>
        /// Invoked to finish setup.
        /// </summary>
        /// <param name="context">Active input binding context.</param>
        protected override void FinishSetup(ref InputBindingCompositeContext context)
        {
            // Set override.
            if (!_timelessModifiers)
            {
                _timelessModifiers = !InputSystem.settings.shortcutKeysConsumeInput;
            }
        }

        /// <summary>
        /// Checks to see if the modifier keys have been pressed.
        /// </summary>
        /// <param name="context">Active input binding context.</param>
        /// <returns><c>true</c> if all required modifier keys are pressed, <c>false</c> otherwise.</returns>
        private bool AreModifiersPressed(ref InputBindingCompositeContext context)
        {
            // Get raw result.
            bool result = context.ReadValueAsButton(modifier1) && context.ReadValueAsButton(modifier2) && context.ReadValueAsButton(modifier3);

            // Check for timing.
            if (result && !_timelessModifiers)
            {
                // Pressed first override not active - ensure that all modifiers were pressed before the action key.
                double actionTime = context.GetPressTime(button);
                if (context.GetPressTime(modifier1) <= actionTime && context.GetPressTime(modifier2) <= actionTime)
                {
                    return context.GetPressTime(modifier3) <= actionTime;
                }

                // If we got here the time check failed - return false.
                return false;
            }

            // Timing not relevant - return raw result.
            return result;
        }
    }
}
