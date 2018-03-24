using System;
using System.Collections.Generic;

namespace HomingInWebservice.ServiceInterface
{
    public static class Utilities
    {
        public static IEnumerable<int> MakeRangeInt(int start, int finish, int increment)
        {
            for (var i = start; i <= finish; i += increment)
            {
                yield return i;
            }
        }

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
    }

}