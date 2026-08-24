// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Finds the closest spelling of a name among a set of known ones, so that markup errors can suggest a fix.
    /// </summary>
    /// <remarks>
    /// Typos in property and type names are by far the most common markup error and the cheapest to make
    /// actionable - <c>Unknown property 'Bakground'. Did you mean 'Background'?</c> costs one edit-distance pass
    /// over a handful of candidates and saves a round of squinting at the file.
    /// </remarks>
    internal static class NameSuggestion
    {
        /// <summary>
        /// Finds the candidate closest to <paramref name="name"/>, if any is close enough to be worth suggesting.
        /// </summary>
        /// <param name="name">The name that failed to resolve.</param>
        /// <param name="candidates">The names that would have resolved.</param>
        /// <returns>The nearest candidate, or <see langword="null"/> when none is a plausible correction.</returns>
        public static string? Nearest(string name, IEnumerable<string> candidates)
        {
            // Allow roughly one edit per three characters, so short names need a near-exact match while longer ones
            // tolerate a slip or two. Anything looser starts suggesting unrelated names, which is worse than silence.
            int budget = Math.Max(1, name.Length / 3);
            string? best = null;
            int bestDistance = int.MaxValue;

            foreach (string candidate in candidates)
            {
                if (Math.Abs(candidate.Length - name.Length) > budget)
                    continue;

                int distance = Distance(name, candidate, budget);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return bestDistance <= budget ? best : null;
        }

        /// <summary>
        /// Formats a <c>Did you mean …?</c> clause for the nearest candidate, or nothing when there isn't one.
        /// </summary>
        /// <param name="name">The name that failed to resolve.</param>
        /// <param name="candidates">The names that would have resolved.</param>
        /// <returns>A sentence to append to an error message, or an empty string.</returns>
        public static string Clause(string name, IEnumerable<string> candidates)
        {
            string? nearest = Nearest(name, candidates);
            return nearest == null ? string.Empty : $" Did you mean '{nearest}'?";
        }

        /// <summary>
        /// Computes the Levenshtein distance between two names, case-insensitively, giving up once it exceeds
        /// <paramref name="budget"/>.
        /// </summary>
        private static int Distance(string left, string right, int budget)
        {
            // Two rolling rows rather than a full matrix - candidate sets here are small, but so is the win from
            // allocating one array per comparison.
            int[] previous = new int[right.Length + 1];
            int[] current = new int[right.Length + 1];

            for (int j = 0; j <= right.Length; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                int rowBest = current[0];
                for (int j = 1; j <= right.Length; j++)
                {
                    int substitution = previous[j - 1] + (char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1);
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
                    rowBest = Math.Min(rowBest, current[j]);
                }

                if (rowBest > budget)
                    return int.MaxValue;

                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }
    }
}
