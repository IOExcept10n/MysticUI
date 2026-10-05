// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Icy.Configuration;
using Icy.Design;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Edits a markup page live through <see cref="MarkupEditor"/>, shows the markup text every edit produces, and
    /// applies whole-text edits through <see cref="DesignDocument.ApplyText"/>, showing whether the page is in sync.
    /// The manual check for the markup design document (Phases 10.1 and 10.2).
    /// </summary>
    public static class DesignDemo
    {
        /// <summary>
        /// The page the demo edits.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel x:Name="content" Orientation="Vertical">
                <TextBlock x:Name="title" FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Edited live, saved as markup</TextBlock>
                <StackPanel x:Name="row" Orientation="Horizontal">
                  <Border x:Name="first" Background="LightCoral" Width="100" Height="60" Margin="0,0,8,0"/>
                  <Border Background="LightSkyBlue" Width="100" Height="60" Margin="0,0,8,0"/>
                </StackPanel>
              </StackPanel>
            </Border>
            """;

        // A configuration has a single load-observer slot, so one session per configuration.
        private static readonly ConditionalWeakTable<IcyConfiguration, DesignSession> Sessions = new();

        /// <summary>
        /// Builds the demo: the tracked page, a toolbar of edits, a status line, and the live markup text.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The demo's root element.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;

            // Real games attach a session only in development builds. The demo keeps one attached for the app's
            // whole life, so every page loaded after this one is tracked too; the hosts build this demo last.
            DesignSession session = Sessions.GetValue(configuration, DesignSession.Attach);
            UIElement page = new MarkupLoader(configuration).Load(Markup, nameof(DesignDemo));
            DesignDocument document = session.FindDocument(page, out _)!;
            MarkupEditor editor = document.Editor;
            MarkupNameScope names = MarkupNameScope.GetScope(page)!;

            NodeId IdOf(string name)
            {
                session.FindDocument(names.Find(name)!, out NodeId id);
                return id;
            }

            var status = new TextBlock { Text = "Ready", Margin = new Thickness(0, 6, 0, 6) };
            var markupView = new TextBlock { FontSize = 12, Text = document.Text };
            document.Changed += (_, _) => markupView.Text = document.Text;

            int inserted = 0;
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            toolbar.Children.Add(CreateButton("Bigger title", status, () =>
            {
                var title = (TextBlock)names.Find("title")!;
                return editor.SetAttribute(IdOf("title"), "FontSize", (title.FontSize + 2).ToString(CultureInfo.InvariantCulture));
            }));
            toolbar.Children.Add(CreateButton("Insert button", status, () =>
                editor.InsertElement(IdOf("row"), 0, $"<Button Padding=\"12,6\" Margin=\"0,0,8,0\">New {++inserted}</Button>")));
            toolbar.Children.Add(CreateButton("Move first box", status, () =>
                editor.MoveElement(IdOf("first"), IdOf("content"), 1)));
            toolbar.Children.Add(CreateButton("Undo", status, editor.UndoStack.Undo));
            toolbar.Children.Add(CreateButton("Redo", status, editor.UndoStack.Redo));
            toolbar.Children.Add(CreateTextButton("Text: retitle", status, document, () =>
                document.Text.Replace("Edited live, saved as markup", "Changed through ApplyText", StringComparison.Ordinal)));
            toolbar.Children.Add(CreateTextButton("Text: break", status, document, () =>
            {
                // Drop the root's end tag: well-formedness breaks, the page keeps showing the last valid markup.
                int end = document.Text.LastIndexOf("</Border>", StringComparison.Ordinal);
                return end < 0 ? document.Text : document.Text[..end];
            }));
            toolbar.Children.Add(CreateTextButton("Text: restore", status, document, () => Markup));

            var root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20),
            };
            root.Children.Add(page);
            root.Children.Add(toolbar);
            root.Children.Add(status);
            root.Children.Add(markupView);
            return root;
        }

        private static Button CreateButton(string label, TextBlock status, Func<EditResult> action)
        {
            var button = new Button
            {
                Content = new TextBlock { Text = label },
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 8, 0),
            };
            button.Click += (_, _) =>
            {
                EditResult result = action();
                status.Text = result.Succeeded ? "OK" : $"Failed: {result.Error?.Message}";
            };
            return button;
        }

        private static Button CreateTextButton(string label, TextBlock status, DesignDocument document, Func<string> nextText)
        {
            var button = new Button
            {
                Content = new TextBlock { Text = label },
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 8, 0),
            };
            button.Click += (_, _) =>
            {
                document.ApplyText(nextText());
                status.Text = document.IsInSync
                    ? "In sync"
                    : $"Out of sync: {(document.LiveErrors.Count > 0 ? document.LiveErrors[0].Message : "the markup has errors")}";
            };
            return button;
        }
    }
}
