using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameObjectBridge.Entities.Cinemachine
{
    internal static class CinemachineBridgeSceneRegistry
    {
        private static readonly List<CinemachineBridgeSetup> Sources = new();
        private static readonly List<CinemachineBridgeTarget> Targets = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Sources.Clear();
            Targets.Clear();
        }

        internal static void RegisterSource(CinemachineBridgeSetup source)
        {
            if (!Sources.Contains(source))
            {
                Sources.Add(source);
            }
            Refresh(source.SceneCameraId.Trim());
        }

        internal static void UnregisterSource(CinemachineBridgeSetup source)
        {
            Sources.Remove(source);
            Refresh(source.SceneCameraId.Trim());
        }

        internal static void RegisterTarget(CinemachineBridgeTarget target)
        {
            if (!Targets.Contains(target))
            {
                Targets.Add(target);
            }
            Refresh(target.RegisteredId);
        }

        internal static void UnregisterTarget(CinemachineBridgeTarget target)
        {
            Targets.Remove(target);
            target.ClearBinding();
            Refresh(target.RegisteredId);
        }

        private static void Refresh(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            Sources.RemoveAll(source => source == null);
            Targets.RemoveAll(target => target == null);

            int sourceCount = 0;
            int targetCount = 0;
            CinemachineBridgeSetup sourceMatch = null;
            CinemachineBridgeTarget targetMatch = null;

            foreach (var source in Sources)
            {
                if (string.Equals(source.SceneCameraId.Trim(), id, StringComparison.Ordinal)
                    && source.Bridge != null && source.Bridge.IsBound)
                {
                    sourceCount++;
                    sourceMatch = source;
                }
            }

            foreach (var target in Targets)
            {
                if (!string.Equals(target.RegisteredId, id, StringComparison.Ordinal))
                {
                    continue;
                }

                target.ClearBinding();
                if (target.isActiveAndEnabled)
                {
                    targetCount++;
                    targetMatch = target;
                }
            }

            if (sourceCount == 1 && targetCount == 1)
            {
                targetMatch.Assign(sourceMatch.Bridge, sourceMatch.Follow, sourceMatch.LookAt);
            }
            else if (sourceCount > 1 || targetCount > 1)
            {
                Debug.LogWarning(
                    $"Cinemachine Bridge: カメラID「{id}」が重複しています。" +
                    $"（表示対象: {sourceCount}件、カメラ: {targetCount}件）自動追跡を設定しません。");
            }
            // 一致するIDがない場合は待機します。表示対象とカメラはどちらが先に登録されても構いません。
        }
    }
}
