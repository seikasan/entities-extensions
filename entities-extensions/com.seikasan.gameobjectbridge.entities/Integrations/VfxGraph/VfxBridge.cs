using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

namespace GameObjectBridge.Entities.VfxGraph
{
    public enum VfxCommandKind
    {
        SetFloat,
        SetVector3,
        SetBool,
        SendEvent,
        Play,
        Stop
    }

    /// <summary>
    /// Burstから書き込めるunmanagedな要求です。Unity APIはメインスレッドで1回だけ実行します。
    /// </summary>
    public struct VfxCommand : IBufferElementData
    {
        public VfxCommandKind Kind;
        public int TargetId;
        public int NameId;
        public float3 Value;
        public static VfxCommand SetFloat(
            int id,
            float value,
            int targetId = 0) => new VfxCommand
            {
                TargetId = targetId,
                Kind = VfxCommandKind.SetFloat,
                NameId = id,
                Value = new float3(
                    value,
                    0,
                    0)
            };
        public static VfxCommand SetVector3(
            int id,
            float3 value,
            int targetId = 0) => new VfxCommand
            {
                TargetId = targetId,
                Kind = VfxCommandKind.SetVector3,
                NameId = id,
                Value = value
            };
        public static VfxCommand SetBool(
            int id,
            bool value,
            int targetId = 0) => new VfxCommand
            {
                TargetId = targetId,
                Kind = VfxCommandKind.SetBool,
                NameId = id,
                Value = new float3(
                    value ? 1 : 0,
                    0,
                    0)
            };
        public static VfxCommand SendEvent(int id, int targetId = 0) => new VfxCommand
        {
            TargetId = targetId,
            Kind = VfxCommandKind.SendEvent,
            NameId = id
        };
        public static VfxCommand Play(int targetId = 0) => new VfxCommand
        {
            TargetId = targetId,
            Kind = VfxCommandKind.Play
        };
        public static VfxCommand Stop(int targetId = 0) => new VfxCommand
        {
            TargetId = targetId,
            Kind = VfxCommandKind.Stop
        };
    }

    public struct VfxBridgeTarget : IBufferElementData
    {
        public int TargetId;
        public UnityObjectRef<VisualEffect> Effect;
        public UnityObjectRef<EntityGameObjectBridge> Bridge;
        public ulong Version;
    }

    public static class VfxBridgeExtensions
    {
        /// <summary>
        /// 既存のVisualEffectを設定します。再設定すると未処理のコマンドを破棄します。
        /// </summary>
        public static void ConfigureVisualEffect(this EntityGameObjectBridge bridge, VisualEffect effect = null)
        {
            bridge.ConfigureVisualEffect(0, effect);
        }

        /// <summary>
        /// 対象IDごとにVisualEffectを登録します。同じIDの再設定では、その対象の要求だけを破棄します。
        /// </summary>
        public static void ConfigureVisualEffect(
            this EntityGameObjectBridge bridge,
            int targetId,
            VisualEffect effect = null)
        {
            if (bridge == null ||
                !bridge.IsBound)
            {
                throw new ArgumentException("有効なGameObject Bridgeの関連付けが必要です。", nameof(bridge));
            }

            if (targetId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetId), "対象IDには0以上の値を指定してください。");
            }

            if (effect == null)
            {
                effect = bridge.GetComponent<VisualEffect>();
            }

            if (effect == null)
            {
                throw new MissingComponentException("VisualEffectコンポーネントが必要です。");
            }

            var manager = bridge.World.EntityManager;
            if (!manager.HasBuffer<VfxBridgeTarget>(bridge.Entity))
            {
                manager.AddBuffer<VfxBridgeTarget>(bridge.Entity);
            }

            if (!manager.HasBuffer<VfxCommand>(bridge.Entity))
            {
                manager.AddBuffer<VfxCommand>(bridge.Entity);
            }

            var targets = manager.GetBuffer<VfxBridgeTarget>(bridge.Entity);
            var commands = manager.GetBuffer<VfxCommand>(bridge.Entity);
            if (targets.Length > 0 &&
                (targets[0].Bridge.Value != bridge ||
                    targets[0].Version != bridge.BindingVersion))
            {
                targets.Clear();
                commands.Clear();
            }

            var data = new VfxBridgeTarget
            {
                TargetId = targetId,
                Effect = effect,
                Bridge = bridge,
                Version = bridge.BindingVersion
            };
            int index = -1;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].TargetId == targetId)
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
            {
                targets[index] = data;
            }
            else
            {
                targets.Add(data);
            }

            for (int i = commands.Length - 1; i >= 0; i--)
            {
                if (commands[i].TargetId == targetId)
                {
                    commands.RemoveAt(i);
                }
            }

            bridge.World.GetOrCreateSystemManaged<VfxBridgeSystem>();
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(GameObjectBridgeSystem))]
    public partial class VfxBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (targets, commands, entity) in SystemAPI.Query<DynamicBuffer<VfxBridgeTarget>, DynamicBuffer<VfxCommand>>().WithOptions(EntityQueryOptions.IncludeDisabledEntities).WithEntityAccess())
            {
                if (!EntityGameObjectBridge.TryGet(
                    World,
                    entity,
                    out var bridge))
                {
                    ecb.RemoveComponent<VfxBridgeTarget>(entity);
                    ecb.RemoveComponent<VfxCommand>(entity);
                    continue;
                }

                for (int i = targets.Length - 1; i >= 0; i--)
                {
                    var target = targets[i];
                    if (target.Bridge.Value != bridge ||
                        target.Version != bridge.BindingVersion ||
                        target.Effect.Value == null)
                    {
                        targets.RemoveAt(i);
                    }
                }

                if (targets.Length == 0)
                {
                    ecb.RemoveComponent<VfxBridgeTarget>(entity);
                    ecb.RemoveComponent<VfxCommand>(entity);
                    continue;
                }

                if (EntityManager.HasComponent<Disabled>(entity))
                {
                    continue;
                }

                foreach (var command in commands)
                {
                    VisualEffect effect = null;
                    foreach (var target in targets)
                    {
                        if (target.TargetId == command.TargetId)
                        {
                            effect = target.Effect.Value;
                            break;
                        }
                    }

                    if (effect == null)
                    {
                        Debug.LogWarning("GameObject Bridge: 未登録の対象IDに送られたVFXコマンドを破棄しました。");
                        continue;
                    }

                    switch (command.Kind)
                    {
                        case VfxCommandKind.SetFloat:
                            if (effect.HasFloat(command.NameId))
                            {
                                effect.SetFloat(command.NameId, command.Value.x);
                            }
                            else
                            {
                                InvalidProperty(effect);
                            }

                            break;
                        case VfxCommandKind.SetVector3:
                            if (effect.HasVector3(command.NameId))
                            {
                                effect.SetVector3(command.NameId, command.Value);
                            }
                            else
                            {
                                InvalidProperty(effect);
                            }

                            break;
                        case VfxCommandKind.SetBool:
                            if (effect.HasBool(command.NameId))
                            {
                                effect.SetBool(command.NameId, command.Value.x != 0);
                            }
                            else
                            {
                                InvalidProperty(effect);
                            }

                            break;
                        case VfxCommandKind.SendEvent:
                            effect.SendEvent(command.NameId);
                            break;
                        case VfxCommandKind.Play:
                            effect.Play();
                            break;
                        case VfxCommandKind.Stop:
                            effect.Stop();
                            break;
                        default:
                            Debug.LogWarning("GameObject Bridge: 不明なVFXコマンドを破棄しました。", effect);
                            break;
                    }
                }

                commands.Clear();
            }

            ecb.Playback(EntityManager);
        }

        private static void InvalidProperty(VisualEffect effect) => Debug.LogWarning("GameObject Bridge: 指定したプロパティが存在しないか、型が一致しないため、VFXプロパティの設定要求を破棄しました。", effect);
    }
}
