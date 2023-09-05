using MysticUI.Extensions;
using System.ComponentModel;
using System.Xml.Linq;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the XML template for the any data representation.
    /// </summary>
    public class DataTemplate : IDisposable
    {
        private bool disposedValue;

        /// <summary>
        /// Type of the data context used in the template.
        /// </summary>
        [Category("Integration")]
        public Type? ContextType { get; set; }

        /// <summary>
        /// Options for the template compilation.
        /// </summary>
        [Category("Miscellaneous")]
        public TemplateCompilationOptions CompilationOptions { get; set; }

        [Browsable(false)]
        internal XElement TemplateContent { get; set; } = null!;

        /// <summary>
        /// Creates the object from the template using provided context.
        /// </summary>
        /// <typeparam name="T">Type of the created object.</typeparam>
        /// <param name="dataContext">Data context for the template.</param>
        /// <returns>Result of the item creation.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public T Instantiate<T>(object? dataContext = null) where T : class
        {
            if (ContextType != null && dataContext != null && !dataContext.GetType().IsAssignableFrom(ContextType))
            {
                throw new ArgumentException($"Data context type should be assignable from context type.");
            }
            if (TemplateContent == null)
            {
                throw new InvalidOperationException($"Template content should be set to instantiate a template.");
            }
            return LayoutSerializer.Default.LoadLayout<T>(TemplateContent, dataContext).NeverNull();
        }

        /// <summary>
        /// Creates the object from the template using provided context.
        /// </summary>
        /// <param name="dataContext">Data context for the template.</param>
        /// <returns>Result of the item creation.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public object Instantiate(object? dataContext = null)
        {
            if (ContextType != null && dataContext != null && !dataContext.GetType().IsAssignableFrom(ContextType))
            {
                throw new ArgumentException($"Data context type should be assignable from context type.");
            }
            if (TemplateContent == null)
            {
                throw new InvalidOperationException($"Template content should be set to instantiate a template.");
            }
            return LayoutSerializer.Default.LoadLayout(TemplateContent, dataContext).NeverNull();
        }

        /// <summary>
        /// Defines the options for the template compilation.
        /// </summary>
        public enum TemplateCompilationOptions
        {
            /// <summary>
            /// Do not compile the template, parse the XML tree every time when instantiate.
            /// </summary>
            NoCompile,

            /// <summary>
            /// Compiles the template after the first template call.
            /// </summary>
            LazyCompilation,

            /// <summary>
            /// Compiles the template after loading.
            /// </summary>
            Compiled
        }

        /// <summary>
        /// Releases all resources related to the template.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }
                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}