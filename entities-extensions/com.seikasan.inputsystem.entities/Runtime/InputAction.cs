using System;
using System.Diagnostics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using ManagedAction = UnityEngine.InputSystem.InputAction;
using Hash128 = Unity.Entities.Hash128;

namespace InputSystem.Entities
{
    public enum InputValueType : byte
    {
        Float,
        Vector2,
        Vector3,
        Vector4,
        Quaternion,
        Integer
    }

    /// <summary>
    /// 直近の収集更新時点のActionのコピーです。Burst Jobから安全に読み取れます。
    /// </summary>
    public struct InputAction
    {
        private float4 _value;
        private InputValueType _valueType;
        private byte _flags;

        public Hash128 id { get; private set; }
        public InputActionPhase phase { get; private set; }
        public bool enabled => phase != InputActionPhase.Disabled;
        public bool inProgress => phase == InputActionPhase.Started ||
            phase == InputActionPhase.Performed;
        public bool triggered => WasPerformedThisFrame();

        /// <summary>
        /// Actionの値を指定した型で返します。
        /// 値の型がfloatのActionでboolを指定すると押下状態を返します。
        /// </summary>
        public T ReadValue<T>()
            where T : unmanaged
        {
            bool valid = (_valueType == InputValueType.Float &&
                (typeof(T) == typeof(float) ||
                    typeof(T) == typeof(bool))) ||
                (_valueType == InputValueType.Integer &&
                    typeof(T) == typeof(int)) ||
                (_valueType == InputValueType.Vector2 &&
                    (typeof(T) == typeof(float2) ||
                        typeof(T) == typeof(Vector2))) ||
                (_valueType == InputValueType.Vector3 &&
                    (typeof(T) == typeof(float3) ||
                        typeof(T) == typeof(Vector3))) ||
                (_valueType == InputValueType.Vector4 &&
                    (typeof(T) == typeof(float4) ||
                        typeof(T) == typeof(Vector4))) ||
                (_valueType == InputValueType.Quaternion &&
                    (typeof(T) == typeof(quaternion) ||
                        typeof(T) == typeof(Quaternion)));

            if (!valid)
            {
                ThrowValueTypeMismatch();
                return default;
            }

            if (typeof(T) == typeof(bool))
            {
                bool pressed = IsPressed();
                return UnsafeUtility.As<bool, T>(ref pressed);
            }

            return UnsafeUtility.As<float4, T>(ref _value);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void ThrowValueTypeMismatch()
        {
            throw new InvalidOperationException("ReadValue<T> must use the action's generated value type.");
        }

        public bool IsPressed() => (_flags & 1) != 0;
        public bool WasPressedThisFrame() => (_flags & 2) != 0;
        public bool WasReleasedThisFrame() => (_flags & 4) != 0;
        public bool WasPerformedThisFrame() => (_flags & 8) != 0;
        public bool WasCompletedThisFrame() => (_flags & 16) != 0;

        /// <summary>
        /// 生成Systemから使用するmanagedな収集処理です。Burstから呼び出さないでください。
        /// </summary>
        public static InputAction Capture(
            ManagedAction action,
            InputValueType valueType,
            Hash128 actionId)
        {
            var result = new InputAction
            {
                id = actionId,
                phase = action.phase,
                _valueType = valueType
            };

            switch (valueType)
            {
                case InputValueType.Float:
                    result._value.x = action.ReadValue<float>();
                    break;
                case InputValueType.Integer:
                    int integer = action.ReadValue<int>();
                    result._value.x = UnsafeUtility.As<int, float>(ref integer);
                    break;
                case InputValueType.Vector2:
                    Vector2 v2 = action.ReadValue<Vector2>();
                    result._value = new float4(
                        v2.x,
                        v2.y,
                        0,
                        0);
                    break;
                case InputValueType.Vector3:
                    Vector3 v3 = action.ReadValue<Vector3>();
                    result._value = new float4(
                        v3.x,
                        v3.y,
                        v3.z,
                        0);
                    break;
                case InputValueType.Vector4:
                    Vector4 v4 = action.ReadValue<Vector4>();
                    result._value = new float4(
                        v4.x,
                        v4.y,
                        v4.z,
                        v4.w);
                    break;
                case InputValueType.Quaternion:
                    Quaternion rotation = action.ReadValue<Quaternion>();
                    result._value = new float4(
                        rotation.x,
                        rotation.y,
                        rotation.z,
                        rotation.w);
                    break;
            }

            if (action.IsPressed())
            {
                result._flags |= 1;
            }

            if (action.WasPressedThisFrame())
            {
                result._flags |= 2;
            }

            if (action.WasReleasedThisFrame())
            {
                result._flags |= 4;
            }

            if (action.WasPerformedThisFrame())
            {
                result._flags |= 8;
            }

            if (action.WasCompletedThisFrame())
            {
                result._flags |= 16;
            }

            return result;
        }
    }
}
