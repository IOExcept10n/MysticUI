// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample
{
    internal class SampleBase(Game game, IcyConfiguration configuration, string name) : DrawableGameComponent(game)
    {
        public IcyConfiguration UIConfiguration { get; } = configuration;

        public string Name { get; } = name;
    }
}