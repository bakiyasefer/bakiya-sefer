namespace FullInspector.Internal {
    /// <summary>
    /// Extensions to the string type.
    /// </summary>
    internal static class StringExtensions {
        /// <summary>
        /// Replaces the format item in a specified System.String with the text equivalent of the
        /// value of a corresponding System.Object instance in a specified array.
        /// </summary>
        /// <param name="format">A composite format string.</param>
        /// <param name="args">An System.Object array containing zero or more objects to
        /// format.</param>
        /// <returns>A copy of format in which the format items have been replaced by the
        /// System.String equivalent of the corresponding instances of System.Object in
        /// args.</returns>
        /// <exception cref="System.ArgumentNullException">format or args is null.</exception>
        /// <exception cref="System.FormatException">format is invalid. -or- The number indicating
        /// an argument to format is less than zero, or greater than or equal to the length of the
        /// args array.</exception>
        public static string F(this string format, params object[] args) {
            return string.Format(format, args);
        }
    }
}