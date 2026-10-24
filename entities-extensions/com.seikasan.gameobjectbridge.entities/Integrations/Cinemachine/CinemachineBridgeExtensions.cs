using System;
using Unity.Cinemachine;
using Unity.Entities;

namespace GameObjectBridge.Entities.Cinemachine
{
    /// <summary>
    /// Cinemachine 3の追従先には通常のTransformを使用します。独自のCamera制御や全体の更新設定は追加しません。
    /// </summary>
    public static class CinemachineBridgeExtensions
    {
        public static void FollowEntity(this CinemachineCamera camera, EntityGameObjectBridge bridge)
        {
            Validate(camera, bridge);
            camera.Follow = bridge.transform;
        }

        public static void LookAtEntity(this CinemachineCamera camera, EntityGameObjectBridge bridge)
        {
            Validate(camera, bridge);
            camera.LookAt = bridge.transform;
        }

        /// <summary>
        /// ManualUpdateに設定したBrainに対し、Cameraの評価前にEntityの出力を同期します。
        /// </summary>
        public static void ManualUpdateAfterBridge(this CinemachineBrain brain, World world)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain), "CinemachineBrainがnullです。");
            }

            if (world == null ||
                !world.IsCreated)
            {
                throw new ArgumentException("有効なWorldを指定してください。", nameof(world));
            }

            if (brain.UpdateMethod != CinemachineBrain.UpdateMethods.ManualUpdate)
            {
                throw new InvalidOperationException("このメソッドを呼び出す前に、CinemachineBrainのUpdate MethodをManual Updateに設定してください。");
            }

            world.GetOrCreateSystemManaged<GameObjectBridgeSystem>().Update();
            brain.ManualUpdate();
        }

        static void Validate(CinemachineCamera camera, EntityGameObjectBridge bridge)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera), "CinemachineCameraがnullです。");
            }

            if (bridge == null ||
                !bridge.IsBound)
            {
                throw new ArgumentException("有効なGameObject Bridgeの関連付けが必要です。", nameof(bridge));
            }

            if (bridge.Direction != TransformSyncDirection.EntityToGameObject)
            {
                throw new ArgumentException("Cinemachineの追従先にはEntityToGameObject方向の同期が必要です。", nameof(bridge));
            }
        }
    }
}
