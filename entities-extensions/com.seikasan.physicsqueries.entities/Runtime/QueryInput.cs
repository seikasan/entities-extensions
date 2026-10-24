using Unity.Mathematics;

namespace PhysicsQueries.Entities
{
    internal static class QueryInput
    {
        // 有限でも大きい方向ベクトルで桁あふれしないよう、縮小してから正規化します。
        internal static bool TryCast(
            float3 origin,
            float3 direction,
            float distance,
            out float3 unit,
            out float3 end)
        {
            unit = default;
            end = default;

            if (distance <= 0)
            {
                return false;
            }

            if (!TryNormalizeDirection(direction, out unit))
            {
                return false;
            }

            end = origin + unit * distance;

            // 終点の有限性確認で、入力値から伝播するNaNとInfinityもまとめて拒否します。
            return math.all(math.isfinite(end));
        }

        private static bool TryNormalizeDirection(
            float3 direction,
            out float3 unit)
        {
            unit = default;

            float lengthSquared = math.lengthsq(direction);

            if (math.abs(lengthSquared - 1f) <= 1e-6f)
            {
                unit = direction;
                return true;
            }

            if (lengthSquared >= 1e-12f &&
                lengthSquared <= 1e30f)
            {
                unit = direction * math.rsqrt(lengthSquared);
                return true;
            }

            float scale = math.cmax(math.abs(direction));

            if (scale == 0)
            {
                return false;
            }

            float3 scaled = direction / scale;
            float scaledLength = math.sqrt(math.lengthsq(scaled));

            if (scale < 1e-6f &&
                scale * scaledLength < 1e-6f)
            {
                return false;
            }

            unit = scaled / scaledLength;
            return true;
        }
    }
}
