﻿using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Wikibase
{

    /// <summary>
    /// Represents an item of coarse-grained modification information on <see cref="Entity"/>.
    /// </summary>
    public sealed class EntityEditEntry
    {
        private EntityEditEntryState _State;

        public EntityEditEntry(string propertyName, object value) : this(propertyName, value, EntityEditEntryState.Updated)
        {
        }

        public EntityEditEntry(string propertyName, object value, EntityEditEntryState state)
        {
            PropertyName = propertyName;
            Value = value;
            State = state;
        }

        public EntityEditEntry()
        {
            
        }

        /// <summary>
        /// The CLR property name of the changed value.
        /// </summary>
        /// <remarks>
        /// This is usually a property name in <see cref="Entity"/>.
        /// </remarks>
        public string PropertyName { get; set; }

        /// <summary>
        /// The new item, updated existing item, or for the deletion,
        /// the item that has enough information to determine the item to remove.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// The operation performed on this entry.
        /// </summary>
        public EntityEditEntryState State
        {
            get { return _State; }
            set
            {
                if (value != EntityEditEntryState.Updated && value != EntityEditEntryState.Removed)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _State = value;
            }
        }

    }

    /// <summary>
    /// Used to mark how an item changes in <see cref="EntityEditEntry"/>.
    /// </summary>
    public enum EntityEditEntryState
    {
        /// <summary>
        /// Either the entry is a new item, or the value inside the item has been changed.
        /// </summary>
        Updated = 0,

        /// <summary>
        /// The entry represents an item to be removed.
        /// </summary>
        Removed = 1,
    }
}
