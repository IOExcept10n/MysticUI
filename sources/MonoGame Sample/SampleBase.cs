// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using Icy.Configuration;
using Icy.Data;
using Icy.Input.Devices;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample
{
    internal class SampleBase(Game game, IcyConfiguration configuration, string name) : DrawableGameComponent(game)
    {
        public IcyConfiguration UIConfiguration { get; } = configuration;

        public string Name { get; } = name;

        protected void RegisterCommand(string binding, Action<object> reaction) => UIConfiguration.Input.Events.RegisterCommand(new RelayCommand(reaction), KeyGesture.Parse(binding, null));
    }
}