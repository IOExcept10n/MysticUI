// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.CompilerServices;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Provides the shared value-precedence machinery for <see cref="IPropertyReference"/> implementations,
    /// so both reflected instance properties and attached properties support styling, visual states, and
    /// animation identically.
    /// </summary>
    /// <typeparam name="TTarget">Target type that owns the property.</typeparam>
    /// <typeparam name="TValue">Property type to access values with.</typeparam>
    internal abstract class PropertyReferenceBase<TTarget, TValue> : IPropertyReference<TValue>
    {
        private const int TierCount = 3;

        private readonly ConditionalWeakTable<object, PrecedenceEntry> precedenceEntries = [];

        /// <inheritdoc/>
        public abstract string Category { get; }

        /// <inheritdoc/>
        public abstract PropertyMetadata Metadata { get; }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public abstract Type OwnerType { get; }

        /// <inheritdoc/>
        public abstract Type PropertyType { get; }

        /// <inheritdoc/>
        public abstract ValidateValueCallback? ValidationCallback { get; }

        /// <inheritdoc/>
        public abstract object? GetRawValue(object target);

        /// <inheritdoc/>
        public abstract TValue GetValue(object target);

        /// <inheritdoc/>
        public abstract void SetRawValue(object target, object? value);

        /// <inheritdoc/>
        public abstract void SetValue(object target, TValue value);

        /// <inheritdoc/>
        public void NotifyLocalValueChanged(object target)
        {
            PrecedenceEntry entry = precedenceEntries.GetValue(target, t => new PrecedenceEntry(GetRawValue(t)));

            // Reentrant call from ApplyWinningValue itself (a tier's SetRawValue triggers the same PropertyChanged
            // hook this method is called from) - not a genuine local assignment, ignore it.
            if (entry.IsApplyingTier)
                return;

            entry.BaseValue = GetRawValue(target);
            entry.HasLocalValue = true;
        }

        /// <inheritdoc/>
        public void SetTierValue(object target, PropertyValuePrecedence tier, object? value)
        {
            PrecedenceEntry entry = precedenceEntries.GetValue(target, t => new PrecedenceEntry(GetRawValue(t)));
            entry.TierValues[(int)tier] = value;
            entry.TierActive[(int)tier] = true;
            ApplyWinningValue(target, entry);
        }

        /// <inheritdoc/>
        public void ClearTierValue(object target, PropertyValuePrecedence tier)
        {
            if (!precedenceEntries.TryGetValue(target, out PrecedenceEntry? entry))
                return;
            entry.TierValues[(int)tier] = null;
            entry.TierActive[(int)tier] = false;
            ApplyWinningValue(target, entry);
        }

        /// <summary>
        /// Clears a directly-assigned (local) value on <paramref name="target"/>, if one is currently in effect,
        /// letting the highest active precedence tier (or the pre-tier fallback value) take over again.
        /// </summary>
        /// <param name="target">The object to clear the local value on.</param>
        public void ClearLocalValue(object target)
        {
            if (!precedenceEntries.TryGetValue(target, out PrecedenceEntry? entry) || !entry.HasLocalValue)
                return;
            entry.HasLocalValue = false;
            ApplyWinningValue(target, entry);
        }

        private void ApplyWinningValue(object target, PrecedenceEntry entry)
        {
            object? winner = entry.BaseValue;

            // A local value always wins outright over every tier; only fall through to tier resolution
            // once no local value is currently in effect.
            if (!entry.HasLocalValue)
            {
                for (int i = 0; i < TierCount; i++)
                {
                    if (entry.TierActive[i])
                        winner = entry.TierValues[i];
                }
            }

            entry.IsApplyingTier = true;
            try
            {
                SetRawValue(target, winner);
            }
            finally
            {
                entry.IsApplyingTier = false;
            }
        }

        private sealed class PrecedenceEntry(object? baseValue)
        {
            public object?[] TierValues { get; } = new object?[TierCount];

            public bool[] TierActive { get; } = new bool[TierCount];

            public object? BaseValue { get; set; } = baseValue;

            public bool HasLocalValue { get; set; }

            public bool IsApplyingTier { get; set; }
        }
    }
}
