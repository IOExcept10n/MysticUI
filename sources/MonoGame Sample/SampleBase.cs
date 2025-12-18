// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using CommunityToolkit.Mvvm.Input;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample
{
    internal class SampleBase(Game game, IcyConfiguration configuration, Canvas canvas, string name) : DrawableGameComponent(game)
    {
        public IcyConfiguration UIConfiguration { get; } = configuration;

        public Canvas Canvas { get; } = canvas;

        public string Name { get; } = name;

        protected void RegisterCommand(string binding, Action reaction) => UIConfiguration.Input.Events.RegisterCommand(new RelayCommand(reaction), KeyGesture.Parse(binding, null));
    }
}