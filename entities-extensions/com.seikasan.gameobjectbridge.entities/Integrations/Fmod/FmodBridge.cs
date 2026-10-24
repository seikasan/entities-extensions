using System;
using System.Collections.Generic;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace GameObjectBridge.Entities.Fmod
{
    public enum FmodCommandKind
    {
        Play,
        Stop,
        SetParameter,
        SetVolume
    }

    public struct FmodCommand : IBufferElementData
    {
        public FmodCommandKind Kind;
        public int TargetId;
        public PARAMETER_ID Parameter;
        public float Value;
        public bool Flag;
        public static FmodCommand Play(int targetId = 0) => new FmodCommand
        {
            TargetId = targetId,
            Kind = FmodCommandKind.Play
        };
        public static FmodCommand Stop(bool allowFadeout = true, int targetId = 0) => new FmodCommand
        {
            TargetId = targetId,
            Kind = FmodCommandKind.Stop,
            Flag = allowFadeout
        };
        public static FmodCommand SetParameter(
            PARAMETER_ID id,
            float value,
            bool ignoreSeekSpeed = false,
            int targetId = 0) => new FmodCommand
            {
                TargetId = targetId,
                Kind = FmodCommandKind.SetParameter,
                Parameter = id,
                Value = value,
                Flag = ignoreSeekSpeed
            };
        public static FmodCommand SetVolume(float value, int targetId = 0) => new FmodCommand
        {
            TargetId = targetId,
            Kind = FmodCommandKind.SetVolume,
            Value = value
        };
    }

    /// <summary>
    /// Entity破棄や関連付けの後始末までFMODのネイティブハンドルを保持します。手動で追加・複製しないでください。
    /// </summary>
    public sealed class FmodBridgeInstance : ICleanupComponentData
    {
        public UnityObjectRef<EntityGameObjectBridge> Bridge;
        public ulong Version;
        internal readonly List<Voice> Voices = new List<Voice>();
        /// <summary>
        /// 既定の対象ID 0のInstanceです。
        /// </summary>
        public EventInstance Instance => Find(0)?.Instance ?? default;
        public bool OwnsInstance => Find(0)?.OwnsInstance ?? false;

        public bool TryGetInstance(int targetId, out EventInstance instance)
        {
            var voice = Find(targetId);
            instance = voice?.Instance ?? default;

            return voice != null &&
                instance.isValid();
        }

        internal Voice Find(int targetId)
        {
            foreach (var voice in Voices)
            {
                if (voice.TargetId == targetId)
                {
                    return voice;
                }
            }

            return null;
        }

        internal void Release()
        {
            foreach (var voice in Voices)
            {
                voice.Release();
            }

            Voices.Clear();
        }

        internal sealed class Voice
        {
            internal int TargetId;
            internal EventInstance Instance;
            internal UnityObjectRef<Transform> Source;
            internal bool OwnsInstance;
            internal Vector3 LastPosition;
            internal bool HasPosition;
            internal void Release()
            {
                if (OwnsInstance &&
                    Instance.isValid())
                {
                    Instance.stop(STOP_MODE.IMMEDIATE);
                    Instance.release();
                }

                Instance.clearHandle();
            }
        }
    }

    public static class FmodBridgeExtensions
    {
        /// <summary>
        /// 通常のRuntimeManagerを使い、既定の対象ID 0のInstanceを生成します。
        /// </summary>
        public static void ConfigureFmod(this EntityGameObjectBridge bridge, EventReference eventReference)
        {
            bridge.ConfigureFmod(0, eventReference);
        }

        /// <summary>
        /// 対象IDごとに所有するInstanceを生成します。source未指定なら表示ルートのTransformを使います。
        /// </summary>
        public static void ConfigureFmod(
            this EntityGameObjectBridge bridge,
            int targetId,
            EventReference eventReference,
            Transform source = null)
        {
            Validate(bridge, targetId);
            var instance = RuntimeManager.CreateInstance(eventReference);
            try
            {
                bridge.ConfigureFmod(
                    targetId,
                    instance,
                    true,
                    source);
            }
            catch
            {
                instance.release();

                throw;
            }
        }

        /// <summary>
        /// 既存Instanceを既定の対象ID 0に登録します。借用Instanceは解除時に停止・解放しません。
        /// </summary>
        public static void ConfigureFmod(
            this EntityGameObjectBridge bridge,
            EventInstance instance,
            bool ownsInstance = false)
        {
            bridge.ConfigureFmod(
                0,
                instance,
                ownsInstance);
        }

        /// <summary>
        /// 既存Instanceを対象IDごとに登録します。同じIDの再設定では、その対象だけを解放・置換します。
        /// </summary>
        public static void ConfigureFmod(
            this EntityGameObjectBridge bridge,
            int targetId,
            EventInstance instance,
            bool ownsInstance = false,
            Transform source = null)
        {
            Validate(bridge, targetId);

            if (!instance.isValid())
            {
                throw new ArgumentException("有効なFMODのEventInstanceを指定してください。", nameof(instance));
            }

            var manager = bridge.World.EntityManager;
            if (!manager.HasComponent<FmodBridgeInstance>(bridge.Entity))
            {
                manager.AddComponentObject(bridge.Entity, new FmodBridgeInstance());
            }

            var data = manager.GetComponentObject<FmodBridgeInstance>(bridge.Entity);
            bool rebound = data.Bridge.Value != bridge ||
                data.Version != bridge.BindingVersion;
            if (rebound)
            {
                data.Release();
            }

            foreach (var voice in data.Voices)
            {
                if (voice.Instance.handle == instance.handle)
                {
                    throw new InvalidOperationException("このFMODのEventInstanceはすでに登録されています。対象IDごとに別のインスタンスを使用してください。");
                }
            }

            var previous = data.Find(targetId);
            if (previous != null)
            {
                previous.Release();
                data.Voices.Remove(previous);
            }

            data.Bridge = bridge;
            data.Version = bridge.BindingVersion;
            data.Voices.Add(new FmodBridgeInstance.Voice
            {
                TargetId = targetId,
                Instance = instance,
                OwnsInstance = ownsInstance,
                Source = source != null ? source : bridge.transform
            });
            if (!manager.HasBuffer<FmodCommand>(bridge.Entity))
            {
                manager.AddBuffer<FmodCommand>(bridge.Entity);
            }

            var commands = manager.GetBuffer<FmodCommand>(bridge.Entity);
            for (int i = commands.Length - 1; i >= 0; i--)
            {
                if (rebound ||
                    commands[i].TargetId == targetId)
                {
                    commands.RemoveAt(i);
                }
            }

            bridge.World.GetOrCreateSystemManaged<FmodBridgeSystem>();
        }

        private static void Validate(EntityGameObjectBridge bridge, int targetId)
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
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(GameObjectBridgeSystem))]
    public partial class FmodBridgeSystem : SystemBase
    {
        private EntityQuery _instances;
        protected override void OnCreate() => _instances = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<FmodBridgeInstance>() },
            Options = EntityQueryOptions.IncludeDisabledEntities
        });
        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRW<FmodCommand>();
            // SDKのハンドルはmanagedなCleanup Componentで保持し、この境界処理だけをメインスレッドで実行します。
            using var entities = _instances.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var data = EntityManager.GetComponentObject<FmodBridgeInstance>(entity);
                if (!EntityGameObjectBridge.TryGet(
                    World,
                    entity,
                    out var bridge) ||
                    bridge != data.Bridge.Value ||
                    bridge.BindingVersion != data.Version)
                {
                    data.Release();
                    EntityManager.RemoveComponent<FmodBridgeInstance>(entity);
                    if (EntityManager.Exists(entity))
                    {
                        EntityManager.RemoveComponent<FmodCommand>(entity);
                    }

                    continue;
                }

                bool disabled = EntityManager.HasComponent<Disabled>(entity);
                for (int i = data.Voices.Count - 1; i >= 0; i--)
                {
                    var voice = data.Voices[i];
                    var source = voice.Source.Value;
                    if (!voice.Instance.isValid() ||
                        source == null)
                    {
                        voice.Release();
                        data.Voices.RemoveAt(i);
                        continue;
                    }

                    if (disabled)
                    {
                        voice.HasPosition = false;
                        continue;
                    }

                    var position = source.position;
                    float dt = World.Unmanaged.Time.DeltaTime;
                    var velocity = voice.HasPosition &&
                        dt > 1e-6f ? (position - voice.LastPosition) / dt : Vector3.zero;
                    Check(voice.Instance.set3DAttributes(source.To3DAttributes(velocity)));
                    voice.LastPosition = position;
                    voice.HasPosition = true;
                }

                if (data.Voices.Count == 0)
                {
                    EntityManager.RemoveComponent<FmodBridgeInstance>(entity);
                    if (EntityManager.Exists(entity))
                    {
                        EntityManager.RemoveComponent<FmodCommand>(entity);
                    }

                    continue;
                }

                if (disabled)
                {
                    continue;
                }

                if (!EntityManager.HasBuffer<FmodCommand>(entity))
                {
                    continue;
                }

                var commands = EntityManager.GetBuffer<FmodCommand>(entity);
                foreach (var command in commands)
                {
                    var voice = data.Find(command.TargetId);
                    if (voice == null)
                    {
                        Debug.LogWarning("GameObject Bridge: 未登録の対象IDに送られたFMODコマンドを破棄しました。");
                        continue;
                    }

                    switch (command.Kind)
                    {
                        case FmodCommandKind.Play:
                            Check(voice.Instance.start());
                            break;
                        case FmodCommandKind.Stop:
                            Check(voice.Instance.stop(command.Flag ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE));
                            break;
                        case FmodCommandKind.SetParameter:
                            Check(voice.Instance.setParameterByID(
                                command.Parameter,
                                command.Value,
                                command.Flag));
                            break;
                        case FmodCommandKind.SetVolume:
                            Check(voice.Instance.setVolume(command.Value));
                            break;
                        default:
                            Debug.LogWarning("GameObject Bridge: 不明なFMODコマンドを破棄しました。");
                            break;
                    }
                }

                commands.Clear();
            }
        }

        protected override void OnDestroy()
        {
            using var entities = _instances.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                EntityManager.GetComponentObject<FmodBridgeInstance>(entity).Release();
            }
        }

        static void Check(RESULT result)
        {
            if (result != RESULT.OK)
            {
                Debug.LogWarning("GameObject Bridge: FMODコマンドの実行に失敗しました: " + result);
            }
        }
    }
}
