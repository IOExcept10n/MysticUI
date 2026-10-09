// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.IO;
using Icy.Configuration;
using Icy.Design;
using Icy.Design.Editor;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// The full editor as a page, the way a game's debug settings would host it: pick any page the samples have loaded,
    /// edit it in an isolated preview, and save to a scratch copy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Save writes every demo page into a temporary scratch folder, never into the repository: the demos are loaded under
    /// bare names (<c>DesignDemo</c>, ...), which the session resolves into that folder, including demos opened after this
    /// one. A file picked with "Open file" saves back to itself.
    /// </para>
    /// <para>
    /// Edits also reach the live demo pages, because the preview joins their documents.
    /// </para>
    /// </remarks>
    public static class EditorWorkspaceDemo
    {
        /// <summary>
        /// Builds the demo.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The demo's root element.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;
            DesignSession design = DesignDemo.SessionFor(configuration);

            string scratch = Directory.CreateTempSubdirectory("icy-samples-").FullName;
            design.SourcePathResolver = source => Path.IsPathRooted(source)
                ? (File.Exists(source) ? source : null)
                : Path.Combine(scratch, Path.GetFileName(source) + ".xml");
            return new EditorWorkspace
            {
                Design = design,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 0,
            };
        }
    }
}
