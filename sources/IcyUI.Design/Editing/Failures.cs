// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Decides which exceptions from live mirroring are an edit's failure rather than the process's.
    /// </summary>
    internal static class Failures
    {
        /// <summary>
        /// Gets whether <paramref name="exception"/> means the process can't safely continue, so it must propagate.
        /// Anything else thrown while mirroring (a game setter's validation, say) is the edit's failure.
        /// </summary>
        public static bool IsFatal(Exception exception) =>
            exception is OutOfMemoryException or InsufficientExecutionStackException or AccessViolationException;

        /// <summary>
        /// Gets the message to report for a failure, looking through the reflection wrapper a plain CLR setter's
        /// exception arrives in.
        /// </summary>
        public static string Describe(Exception exception) =>
            (exception is TargetInvocationException { InnerException: { } inner } ? inner : exception).Message;
    }
}
