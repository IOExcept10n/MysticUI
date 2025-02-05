// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Icy.Threading;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Represents an object that supports general bindings as target.
    /// </summary>
    public abstract class BindableObject : DispatcherObject, IBindingTarget
    {
        private readonly List<IBinding> bindings = [];

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <inheritdoc/>
        public IReadOnlyCollection<IBinding> Bindings => bindings;

        /// <inheritdoc/>
        public void Bind(IBinding binding)
        {
            binding.SetTarget(this);
            bindings.Add(binding);
        }

        /// <inheritdoc/>
        public bool Unbind(IBinding binding)
        {
            if (bindings.Remove(binding))
            {
                binding.Dispose();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void UnbindAll()
        {
            bindings.ForEach(x => x.Dispose());
            bindings.Clear();
        }

        /// <inheritdoc/>
        public int UnbindWhere(Func<IBinding, bool> predicate)
        {
            int count = 0;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (predicate(bindings[i]))
                {
                    bindings[i].Dispose();
                    bindings.RemoveAt(i--);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Handles the property update process.
        /// </summary>
        /// <param name="propertyName">Name of the calling property.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new(propertyName));
        }
    }
}
