// <copyright file="ActionNames.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    /// <summary>
    /// Identifying names for input actions.
    /// </summary>
    internal struct ActionNames
    {
        /// <summary>
        /// Toggle the overlay on or off.
        /// </summary>
        internal const string ToggleAction = "ToggleOverlay";

        /// <summary>
        /// Raise the overlay one step.
        /// </summary>
        internal const string MoveUpAction = "MoveUp";

        /// <summary>
        /// Lower the overlay one step.
        /// </summary>
        internal const string MoveDownAction = "MoveDown";

        /// <summary>
        /// Raise the overlay multiple steps at once.
        /// </summary>
        internal const string MoveUpLargeAction = "MoveUpLarge";

        /// <summary>
        /// Lower the overlay multiple steps at once.
        /// </summary>
        internal const string MoveDownLargeAction = "MoveDownLarge";

        /// <summary>
        /// Move the overlay north one step.
        /// </summary>
        internal const string MoveNorthAction = "MoveNorth";

        /// <summary>
        /// Move the overlay south one step.
        /// </summary>
        internal const string MoveSouthAction = "MoveSouth";

        /// <summary>
        /// Move the overlay east one step.
        /// </summary>
        internal const string MoveEastAction = "MoveEast";

        /// <summary>
        /// Move the overlay west one step.
        /// </summary>
        internal const string MoveWestAction = "MoveWest";

        /// <summary>
        /// Move the overlay north multiple steps at once.
        /// </summary>
        internal const string MoveNorthLargeAction = "MoveNorthLarge";

        /// <summary>
        /// Move the overlay south multiple steps at once.
        /// </summary>
        internal const string MoveSouthLargeAction = "MoveSouthLarge";

        /// <summary>
        /// Move the overlay east multiple steps at once.
        /// </summary>
        internal const string MoveEastLargeAction = "MoveEastLarge";

        /// <summary>
        /// Move the overlay west multiple steps at once.
        /// </summary>
        internal const string MoveWestLargeAction = "MoveWestLarge";

        /// <summary>
        /// Rotate the overlay left (counter-clockwise) one step.
        /// </summary>
        internal const string RotateLeftAction = "RotateLeft";

        /// <summary>
        /// Rotate the overlay right (clockwise) one step.
        /// </summary>
        internal const string RotateRightAction = "RotateRight";

        /// <summary>
        /// Rotate the overlay left (counter-clockwise) multiple steps at once.
        /// </summary>
        internal const string RotateLeftLargeAction = "RotateLeftLarge";

        /// <summary>
        /// Rotate the overlay right (clockwise) multiple steps at once.
        /// </summary>
        internal const string RotateRightLargeAction = "RotateRightLarge";

        /// <summary>
        /// Increase the overlay size one step.
        /// </summary>
        internal const string IncreaseSizeAction = "IncreaseSize";

        /// <summary>
        /// Decrease the overlay size one step.
        /// </summary>
        internal const string DecreaseSizeAction = "DecreaseSize";

        /// <summary>
        /// Increase the overlay size multiple steps at once.
        /// </summary>
        internal const string IncreaseSizeLargeAction = "IncreaseSizeLarge";

        /// <summary>
        /// Decrease the overlay size multiple steps at once..
        /// </summary>
        internal const string DecreaseSizeLargeAction = "DecreaseSizeLarge";
    }
}
