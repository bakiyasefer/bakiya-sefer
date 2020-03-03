using System;
using System.Collections.Generic;

namespace FullInspector.Internal {
    /// <summary>
    /// Manages pooling instances of a given type.
    /// </summary>
    public class fiFactory<T> where T : new() {
        /// <summary>
        /// The reusable instances that are available.
        /// </summary>
        private Stack<T> _reusable = new Stack<T>();

        /// <summary>
        /// Function called to reset an instance to it's default state.
        /// </summary>
        private Action<T> _reset;

        /// <summary>
        /// Create a new factory with the given reset function.
        /// </summary>
        public fiFactory(Action<T> reset) {
            _reset = reset;
        }

        /// <summary>
        /// Returns an instance of the given type. Recycles an old type if possible.
        /// </summary>
        public T GetInstance() {
            if (_reusable.Count == 0) {
                return new T();
            }

            return _reusable.Pop();
        }

        /// <summary>
        /// Reuse the given object instance at a later date.
        /// </summary>
        public void ReuseInstance(T instance) {
            _reset(instance);
            _reusable.Push(instance);
        }
    }
}