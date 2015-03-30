﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Holds the unlocalized invariant column caption.  This column caption string is used as the key
    /// for <see cref="DataSchemaLocalizer" />
    /// </summary>
    public struct ColumnCaption
    {
        public ColumnCaption(string invariantColumnCaption) : this()
        {
            InvariantCaption = invariantColumnCaption;
            IsLocalizable = true;
        }

        public string InvariantCaption { get; private set; }
        public bool IsLocalizable { get; private set; }

        public override string ToString()
        {
            return InvariantCaption;
        }

        /// <summary>
        /// Constructs an unlocalizable, where the caption will appear the same in all languages.
        /// This is the sort of caption that appears when the name of the column is something that the
        /// user defined, such as an annotation column.
        /// </summary>
        public static ColumnCaption UnlocalizableCaption(string caption)
        {
            return new ColumnCaption(caption)
            {
                IsLocalizable = false,
            };
        }
    }

    public enum ColumnCaptionType
    {
        invariant,
        localized,
    }
}
