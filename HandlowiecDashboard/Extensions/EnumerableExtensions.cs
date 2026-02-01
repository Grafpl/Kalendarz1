using System;
using System.Collections.Generic;
using System.Linq;

namespace Kalendarz1.HandlowiecDashboard.Extensions
{
    public static class EnumerableExtensions
    {
        public static TResult MaxOrDefault<TSource, TResult>(
            this IEnumerable<TSource> source, 
            Func<TSource, TResult> selector, 
            TResult defaultValue = default)
        {
            if (source == null || !source.Any()) return defaultValue;
            return source.Max(selector);
        }
    }
}
