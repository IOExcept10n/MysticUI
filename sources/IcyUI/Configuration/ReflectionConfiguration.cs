using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icy.Data;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IReflectionConfiguration"/> interface.
    /// </summary>
    public class ReflectionConfiguration : IReflectionConfiguration
    {
        /// <inheritdoc/>
        public IAssemblyResolver AssemblyResolver { get; set; } = new AssemblyResolver();

        /// <inheritdoc/>
        public ITypeConverter TypeConverter { get; set; } = new TypeConversionManager();
    }
}
