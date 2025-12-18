// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.ComponentModel;
using Icy.Data.Markup;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents a dispatcher object with methods for observable properties support.
    /// </summary>
    /// <remarks>
    /// Note that this class shouldn't used directly because it doesn't contain any logic.
    /// If you want to generate properties with dispatcher checks, use <see cref="BindableObject"/> instead.
    /// </remarks>
    [ObservableObject]
    public abstract partial class ObservableDispatcherObject : DispatcherObject
    {
    }
}