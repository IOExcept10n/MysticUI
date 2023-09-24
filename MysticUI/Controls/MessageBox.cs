using FontStashSharp.RichText;
using MysticUI.Extensions;
using Stride.Core.Mathematics;
using System.Globalization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a modal box with predefined style to show informational messages to user and get an answer to simple queries.
    /// </summary>
    public class MessageBox : DialogWindow
    {
        /// <summary>
        /// Gets the default style set to all generated buttons.
        /// </summary>
        public const string MessageBoxButtonStyleName = "MessageBoxButtonStyle";

        /// <summary>
        /// Set of buttons used in the <see cref="MessageBox"/> instance.
        /// </summary>
        public Button[] ButtonsSet { get; set; }

        /// <summary>
        /// Creates a message box with the set of buttons and without any contents.
        /// </summary>
        /// <param name="buttonsSet">Buttons to present in the <see cref="MessageBox"/></param>
        public MessageBox(IEnumerable<Button> buttonsSet)
        {
            ButtonsSet = buttonsSet.ToArray();
            var buttonsGrid = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            foreach (var button in ButtonsSet)
            {
                buttonsGrid.Children.Add(button);
                button.Click += (_, __) => Close();
            }

            Child.Children.Add(buttonsGrid);
        }

        #region Creation

        /// <summary>
        /// Creates a dialog modal message box with a title, content and buttons inside.
        /// </summary>
        /// <param name="title">The window title for the box.</param>
        /// <param name="content">Content control.</param>
        /// <param name="buttons">Buttons to interact with.</param>
        /// <returns>A <see cref="MessageBox"/> instance to work with.</returns>
        public static MessageBox CreateMessageBox(string title, Control content, IEnumerable<Button> buttons)
        {
            return new(buttons)
            {
                Title = title,
                Content = content
            };
        }

        /// <summary>
        /// Creates a dialog modal message box with a title, content and buttons inside.
        /// </summary>
        /// <param name="title">The window title for the box.</param>
        /// <param name="text">Text to present in the box.</param>
        /// <param name="buttons">Buttons to interact with.</param>
        /// <param name="textAlignment">Alignment of the text inside.</param>
        /// <returns>A <see cref="MessageBox"/> instance to work with.</returns>
        public static MessageBox CreateMessageBox(string title, string text, IEnumerable<Button> buttons, TextHorizontalAlignment textAlignment = TextHorizontalAlignment.Center)
        {
            return new(buttons)
            {
                Title = title,
                // Set the default style to get the default font.
                Content = new TextBlock()
                {
                    Text = text,
                    TextWrapping = true,
                    TextAlignment = textAlignment
                }.WithDefaultStyle()
            };
        }

        /// <summary>
        /// Creates a dialog modal message box with a title, content and buttons inside.
        /// </summary>
        /// <param name="title">The window title for the box.</param>
        /// <param name="text">Text to present in the box.</param>
        /// <param name="buttons">Button names and associated dialog results.</param>
        /// <param name="textAlignment">An alignment of the text inside.</param>
        /// <returns>A <see cref="MessageBox"/> instance to work with.</returns>
        public static MessageBox CreateMessageBox(string title, string text, ButtonDefinition[] buttons, TextHorizontalAlignment textAlignment = TextHorizontalAlignment.Center)
        {
            return new(from def in buttons select new Button() { Content = def.Title, DialogResult = def.Result, StyleName = MessageBoxButtonStyleName })
            {
                Title = title,
                Content = new TextBlock()
                {
                    Text = text,
                    TextWrapping = true,
                    TextAlignment = textAlignment
                }.WithDefaultStyle()
            };
        }

        /// <summary>
        /// Creates a dialog modal message box with a title, content and buttons inside.
        /// </summary>
        /// <param name="title">The window title for the box.</param>
        /// <param name="text">Text to present in the box.</param>
        /// <param name="buttons">Set of default button templates used in the message box.</param>
        /// <returns>A <see cref="MessageBox"/> instance to work with.</returns>
        public static MessageBox CreateMessageBox(string title, string text, PredefinedButtonTypes buttons) => CreateMessageBox(title, text, GetButtons(buttons));

        #endregion Creation

        #region Presentation

        /// <summary>
        /// Shows the <see cref="MessageBox"/> on the given <see cref="Desktop"/>, awaits for its closure and returns a result of the interaction.
        /// </summary>
        /// <param name="desktop">A desktop to present <see cref="MessageBox"/> in.</param>
        /// <param name="title">A title text to present.</param>
        /// <param name="text">A text to show the message.</param>
        /// <returns>The result of an interaction with the message box.</returns>
        /// <remarks>
        /// Note that regardless of the defined buttons, the modal box has a cancel button so the result always can get the <see cref="DialogResult.Cancel"/> value.
        /// </remarks>
        public static Task<DialogResult> ShowAsync(Desktop desktop, string title, string text) => ShowAsync(desktop, title, text, PredefinedButtonTypes.OK);

        /// <summary>
        /// Shows the <see cref="MessageBox"/> on the given <see cref="Desktop"/>, awaits for its closure and returns a result of the interaction.
        /// </summary>
        /// <param name="desktop">A desktop to present <see cref="MessageBox"/> in.</param>
        /// <param name="title">A title text to present.</param>
        /// <param name="text">A text to show the message.</param>
        /// <param name="buttons">Predefined buttons set to generate default buttons for the interaction.</param>
        /// <param name="position">The position of the box.</param>
        /// <returns>The result of an interaction with the message box.</returns>
        /// <remarks>
        /// Note that regardless of the defined buttons, the modal box has a cancel button so the result always can get the <see cref="DialogResult.Cancel"/> value.
        /// </remarks>
        public static Task<DialogResult> ShowAsync(Desktop desktop, string title, string text, PredefinedButtonTypes buttons, Point? position = null) =>
            ShowAsync(desktop, title, text, GetButtons(buttons), TextHorizontalAlignment.Center, position);

        /// <summary>
        /// Shows the <see cref="MessageBox"/> on the given <see cref="Desktop"/>, awaits for its closure and returns a result of the interaction.
        /// </summary>
        /// <param name="desktop">A desktop to present <see cref="MessageBox"/> in.</param>
        /// <param name="title">A title text to present.</param>
        /// <param name="text">A text to show the message.</param>
        /// <param name="buttons">Button names and associated dialog results.</param>
        /// <param name="textAlignment">An alignment of the text inside.</param>
        /// <param name="position">The position of the box.</param>
        /// <returns>The result of an interaction with the message box.</returns>
        /// <remarks>
        /// Note that regardless of the defined buttons, the modal box has a cancel button so the result always can get the <see cref="DialogResult.Cancel"/> value.
        /// </remarks>
        public static Task<DialogResult> ShowAsync(
            Desktop desktop,
            string title,
            string text,
            ButtonDefinition[] buttons,
            TextHorizontalAlignment textAlignment = TextHorizontalAlignment.Center,
            Point? position = null) =>
                ShowAsync(desktop, title, new TextBlock()
                {
                    Text = text,
                    TextWrapping = true,
                    TextAlignment = textAlignment
                }, from def in buttons select new Button() { Content = def.Title, DialogResult = def.Result, StyleName = MessageBoxButtonStyleName }, position);

        /// <summary>
        /// Shows the <see cref="MessageBox"/> on the given <see cref="Desktop"/>, awaits for its closure and returns a result of the interaction.
        /// </summary>
        /// <param name="desktop">A desktop to present <see cref="MessageBox"/> in.</param>
        /// <param name="title">A title text to present.</param>
        /// <param name="content">The content control to present in the window.</param>
        /// <param name="buttons">Button to place and interact with.</param>
        /// <param name="position">The position of the box.</param>
        /// <returns>The result of an interaction with the message box.</returns>
        /// <remarks>
        /// Note that regardless of the defined buttons, the modal box has a cancel button so the result always can get the <see cref="DialogResult.Cancel"/> value.
        /// </remarks>
        public static Task<DialogResult> ShowAsync(Desktop desktop, string title, Control content, IEnumerable<Button> buttons, Point? position = null)
        {
            return CreateMessageBox(title, content, buttons).ShowDialogAsync(desktop, position);
        }

        #endregion Presentation

        private static ButtonDefinition[] GetButtons(PredefinedButtonTypes buttons)
        {
            var localizer = EnvironmentSettingsProvider.EnvironmentSettings.LocalizationProvider;
            return buttons switch
            {
                PredefinedButtonTypes.OKCancel => new ButtonDefinition[] {
                        new(localizer?.Localize("Cancel") ?? "Cancel", DialogResult.Cancel),
                        new(localizer?.Localize("OK") ?? "OK", DialogResult.OK)
                    },
                PredefinedButtonTypes.YesNo => new ButtonDefinition[]
                    {
                        new(localizer?.Localize("Yes"   ) ?? "Yes", DialogResult.Yes),
                        new(localizer?.Localize("No") ?? "No", DialogResult.No)
                    },
                PredefinedButtonTypes.YesNoCancel => new ButtonDefinition[]
                    {
                        new(localizer?.Localize("Yes") ?? "Yes", DialogResult.Yes),
                        new(localizer?.Localize("No") ?? "No", DialogResult.No),
                        new(localizer?.Localize("Cancel") ?? "Cancel", DialogResult.Cancel),
                    },
                _ => new ButtonDefinition[] { new(localizer?.Localize("OK") ?? "OK", DialogResult.OK) },
            };
        }

        /// <summary>
        /// Represents a definition to generate the <see cref="MessageBox"/> <see cref="Button"/>s.
        /// </summary>
        /// <param name="Title">Title of the button.</param>
        /// <param name="Result">The result of the button clicking that will be set to the <see cref="MessageBox"/> as the result.</param>
        public record struct ButtonDefinition(string Title, DialogResult Result);

        /// <summary>
        /// Defines the default buttons set to generate buttons in the <see cref="MessageBox"/>.
        /// </summary>
        public enum PredefinedButtonTypes
        {
            /// <summary>
            /// In the <see cref="MessageBox"/> is presented an <see langword="OK"/> <see cref="Button"/>.
            /// </summary>
            OK,

            /// <summary>
            /// In the <see cref="MessageBox"/> are presented <see langword="OK"/> and <see langword="Cancel"/> <see cref="Button"/>s.
            /// </summary>
            OKCancel,

            /// <summary>
            /// In the <see cref="MessageBox"/> are presented <see langword="Yes"/> and <see langword="No"/> <see cref="Button"/>s.
            /// </summary>
            YesNo,

            /// <summary>
            /// In the <see cref="MessageBox"/> are presented <see langword="Yes"/>, <see langword="No"/> and <see langword="Cancel"/> <see cref="Button"/>s.
            /// </summary>
            YesNoCancel
        }
    }
}