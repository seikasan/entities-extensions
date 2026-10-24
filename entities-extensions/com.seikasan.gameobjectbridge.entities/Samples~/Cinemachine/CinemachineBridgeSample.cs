using GameObjectBridge.Entities.Cinemachine;
using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GameObjectBridge.Entities.Samples
{
    /// <summary>
    /// 空のシーンで使用します。Camera、Cinemachine Camera、Entity駆動の追従対象を生成します。
    /// </summary>
    public sealed class CinemachineBridgeSample : MonoBehaviour
    {
        World world;
        Entity entity;
        GameObject cameraObject, virtualCameraObject;
        void Start()
        {
            world = World.DefaultGameObjectInjectionWorld;

            if (world == null ||
                !world.IsCreated)
            {
                Debug.LogError("初期化済みのEntities Worldが必要です。", this);

                return;
            }

            entity = world.EntityManager.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            world.EntityManager.SetComponentData(entity, LocalTransform.Identity);
            world.EntityManager.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Entity camera target";
            var bridge = EntityGameObjectBridge.Bind(
                world,
                entity,
                target,
                destroyGameObjectOnUnbind: true);
            cameraObject = new GameObject("Bridge output camera");
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<CinemachineBrain>().UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            virtualCameraObject = new GameObject("Bridge Cinemachine camera");
            var camera = virtualCameraObject.AddComponent<CinemachineCamera>();
            virtualCameraObject.AddComponent<CinemachineFollow>().FollowOffset = new Vector3(
                0,
                3,
                -6);
            virtualCameraObject.AddComponent<CinemachineRotationComposer>();
            camera.FollowEntity(bridge);
            camera.LookAtEntity(bridge);
        }

        void Update()
        {
            if (world != null &&
                world.IsCreated &&
                world.EntityManager.Exists(entity))
            {
                world.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(
                    Mathf.Sin(Time.time) * 3,
                    0,
                    0)));
            }
        }

        void OnDestroy()
        {
            if (world != null &&
                world.IsCreated &&
                world.EntityManager.Exists(entity))
            {
                world.EntityManager.DestroyEntity(entity);
            }

            if (virtualCameraObject != null)
            {
                Destroy(virtualCameraObject);
            }

            if (cameraObject != null)
            {
                Destroy(cameraObject);
            }
        }
    }
}
