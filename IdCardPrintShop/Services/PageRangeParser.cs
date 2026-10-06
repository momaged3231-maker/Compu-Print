using System;
using System.Collections.Generic;
using System.Linq;

namespace IdCardPrintShop.Services
{
    public static class PageRangeParser
    {
        /// <summary>
        /// Parses a human-readable page range string (e.g. "1-3, 7, 10-12") into 0-based page indices.
        /// Returns all pages (0 .. totalPages - 1) if input is empty, null, or "all".
        /// </summary>
        public static List<int> Parse(string? rangeStr, int totalPages)
        {
            if (totalPages <= 0) return new List<int>();

            if (string.IsNullOrWhiteSpace(rangeStr) ||
                rangeStr.Trim().Equals("all", StringComparison.OrdinalIgnoreCase) ||
                rangeStr.Trim().Equals("الكل", StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Range(0, totalPages).ToList();
            }

            var result = new List<int>();
            var tokens = rangeStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawToken in tokens)
            {
                var token = rawToken.Trim();
                if (string.IsNullOrEmpty(token)) continue;

                if (token.Contains('-'))
                {
                    var parts = token.Split('-');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0].Trim(), out int start) &&
                        int.TryParse(parts[1].Trim(), out int end))
                    {
                        int min = Math.Min(start, end);
                        int max = Math.Max(start, end);

                        for (int p = min; p <= max; p++)
                        {
                            if (p >= 1 && p <= totalPages)
                            {
                                int idx = p - 1;
                                if (!result.Contains(idx))
                                {
                                    result.Add(idx);
                                }
                            }
                        }
                    }
                }
                else if (int.TryParse(token, out int singlePage))
                {
                    if (singlePage >= 1 && singlePage <= totalPages)
                    {
                        int idx = singlePage - 1;
                        if (!result.Contains(idx))
                        {
                            result.Add(idx);
                        }
                    }
                }
            }

            // Fallback to all pages if no valid numbers could be parsed
            return result.Count > 0 ? result : Enumerable.Range(0, totalPages).ToList();
        }

        /// <summary>
        /// Formats a list of 0-based page indices into a clean, human-readable range string.
        /// </summary>
        public static string Format(IEnumerable<int> indices, int totalPages)
        {
            var list = indices.OrderBy(i => i).ToList();
            if (list.Count == 0 || list.Count == totalPages) return "All";

            var ranges = new List<string>();
            int i = 0;
            while (i < list.Count)
            {
                int start = list[i];
                int end = start;

                while (i + 1 < list.Count && list[i + 1] == end + 1)
                {
                    end++;
                    i++;
                }

                if (start == end)
                {
                    ranges.Add((start + 1).ToString());
                }
                else
                {
                    ranges.Add($"{start + 1}-{end + 1}");
                }
                i++;
            }

            return string.Join(", ", ranges);
        }
    }
}
