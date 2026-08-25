// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using Icy.UI;

namespace Icy.Diagnostics
{
    /// <summary>
    /// Resolves a <see cref="BoxModel"/> for an arbitrary <see cref="UIElement"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="UIElement"/> itself has no generic margin/border/padding/content-box concept - only
    /// <c>ActualBounds</c>/<c>Margin</c> are universal. <c>BorderThickness</c> (for a padding box) and
    /// <c>IContainerLayout.ContentBounds</c> (for a content box) exist only on some subtypes
    /// (<see cref="UI.Border"/>, <see cref="UI.Controls.Control"/>, <see cref="UI.Panel"/>). This resolver
    /// degrades gracefully instead of assuming every element has every box, without adding any new members to
    /// those types - a plain <see cref="UIElement"/> resolves to margin/border boxes only.
    /// </remarks>
    public static class BoxModelResolver
    {
        // Rendering (and this resolver with it) isn't inherently single-threaded the way the rest of IcyUI's
        // layout/property machinery is documented to be - two Canvases on two threads could resolve box models
        // concurrently - so this cache needs real thread safety, not just a plain Dictionary.
        private static readonly ConcurrentDictionary<Type, PropertyInfo?> BorderThicknessProperties = new();

        /// <summary>
        /// Resolves <paramref name="element"/>'s box model.
        /// </summary>
        /// <param name="element">The element to resolve boxes for.</param>
        /// <returns>The element's margin and border boxes, plus its padding/content boxes when available.</returns>
        public static BoxModel Resolve(UIElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            Rectangle borderBox = new(Point.Empty, element.ActualBounds.Size);
            Rectangle marginBox = borderBox + element.Margin;

            Thickness? borderThickness = GetBorderThickness(element);
            Rectangle? paddingBox = borderThickness is { } thickness && thickness != Thickness.Zero
                ? borderBox - thickness
                : null;

            Rectangle? contentBox = element is IContainerLayout layout
                ? ToLocal(layout.ContentBounds, element.ActualBounds.Location)
                : null;

            return new(marginBox, borderBox, paddingBox, contentBox);
        }

        private static Thickness? GetBorderThickness(UIElement element)
        {
            PropertyInfo? property = BorderThicknessProperties.GetOrAdd(element.GetType(), static type =>
            {
                PropertyInfo? candidate = type.GetProperty("BorderThickness", BindingFlags.Public | BindingFlags.Instance);
                return candidate?.PropertyType == typeof(Thickness) && candidate.CanRead ? candidate : null;
            });

            return (Thickness?)property?.GetValue(element);
        }

        private static Rectangle ToLocal(Rectangle absolute, Point origin) =>
            new(absolute.X - origin.X, absolute.Y - origin.Y, absolute.Width, absolute.Height);
    }
}
