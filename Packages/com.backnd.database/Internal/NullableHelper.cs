using System;

using UnityEngine.Scripting;

namespace BACKND.Database.Internal
{
    [Preserve]
    public static class NullableHelper
    {
        // DateTime
        [Preserve]
        public static DateTime? CreateNullableDateTime(DateTime value) => value;

        // Int32
        [Preserve]
        public static int? CreateNullableInt32(int value) => value;

        // Int64
        [Preserve]
        public static long? CreateNullableInt64(long value) => value;

        // Boolean
        [Preserve]
        public static bool? CreateNullableBoolean(bool value) => value;

        // Single (float)
        [Preserve]
        public static float? CreateNullableSingle(float value) => value;

        // Double
        [Preserve]
        public static double? CreateNullableDouble(double value) => value;

        // UInt32
        [Preserve]
        public static uint? CreateNullableUInt32(uint value) => value;

        // UInt64
        [Preserve]
        public static ulong? CreateNullableUInt64(ulong value) => value;

        // Guid
        [Preserve]
        public static Guid? CreateNullableGuid(Guid value) => value;
    }
}
