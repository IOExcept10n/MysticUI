// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample
{
    internal class SamplesRunner(Game game)
    {
        private readonly List<SampleBase> samples = [];

        private int selection = 0;

        public int Selection
        {
            get => selection;
            set
            {
                if (value != selection)
                {
                    int count = game.Components.OfType<SampleBase>().Count();
                    selection = value;
                    if (value >= count)
                    {
                        selection = 0;
                    }
                    else if (value < 0)
                    {
                        selection = count - 1;
                    }
                    OnSelectionUpdate();
                }
            }
        }

        public SampleBase CurrentSample => samples[selection];

        public void OnSelectionUpdate()
        {
            for (int i = 0; i < samples.Count; i++)
            {
                samples[i].Enabled = samples[i].Visible = i == Selection;
            }
        }

        public void Prepare(IEnumerable<SampleBase> samples)
        {
            foreach (var sample in samples)
            {
                game.Components.Add(sample);
                this.samples.Add(sample);
            }
            OnSelectionUpdate();
        }
    }
}