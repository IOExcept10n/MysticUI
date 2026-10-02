// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using CommunityToolkit.Diagnostics;
using Icy.Data.Bindings;
using Icy.UI;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents the application-wide UI scaling defaults.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <see cref="Canvas"/> built against the owning <see cref="IcyConfiguration"/> combines these values into its
    /// <see cref="Canvas.EffectiveScale"/>. A canvas may override <see cref="Mode"/>, <see cref="ReferenceSize"/> and
    /// <see cref="ReferenceFit"/> individually. <see cref="UserScale"/> is global, so a single options slider affects every canvas.
    /// </para>
    /// <para>
    /// Changes take effect on each canvas's next <see cref="Canvas.Render"/> call.
    /// </para>
    /// </remarks>
    public class ScalingConfiguration : ObservableDispatcherObject
    {
        private UIScaleMode mode = UIScaleMode.Dpi;
        private Size referenceSize = new(1920, 1080);
        private ReferenceFit referenceFit = ReferenceFit.Fit;
        private float userScale = 1f;

        /// <summary>
        /// Gets or sets how the base scale factor is chosen. Defaults to <see cref="UIScaleMode.Dpi"/>.
        /// </summary>
        public UIScaleMode Mode
        {
            get => mode;
            set => SetProperty(ref mode, value);
        }

        /// <summary>
        /// Gets or sets the resolution the UI is designed for, used by <see cref="UIScaleMode.ReferenceResolution"/>.
        /// Defaults to 1920×1080.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
        public Size ReferenceSize
        {
            get => referenceSize;
            set
            {
                Guard.IsGreaterThan(value.Width, 0);
                Guard.IsGreaterThan(value.Height, 0);
                SetProperty(ref referenceSize, value);
            }
        }

        /// <summary>
        /// Gets or sets how <see cref="ReferenceSize"/> is fitted to the viewport. Defaults to <see cref="UI.ReferenceFit.Fit"/>.
        /// </summary>
        public ReferenceFit ReferenceFit
        {
            get => referenceFit;
            set => SetProperty(ref referenceFit, value);
        }

        /// <summary>
        /// Gets or sets the user's UI-scale preference, multiplied on top of the base factor. Defaults to <c>1</c>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a finite number greater than zero.</exception>
        public float UserScale
        {
            get => userScale;
            set
            {
                if (!float.IsFinite(value) || value <= 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value), value, "The user scale must be a finite number greater than zero.");
                SetProperty(ref userScale, value);
            }
        }
    }
}
