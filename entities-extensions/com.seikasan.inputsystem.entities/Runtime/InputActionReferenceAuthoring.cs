using Unity.Entities;
using UnityEngine;
using ManagedReference = UnityEngine.InputSystem.InputActionReference;
using Hash128 = Unity.Entities.Hash128;

namespace InputSystem.Entities
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Input System for Entities/Input Action Reference")]
    public sealed class InputActionReferenceAuthoring : MonoBehaviour
    {
        public ManagedReference Action;

        private sealed class InputActionReferenceBaker : Baker<InputActionReferenceAuthoring>
        {
            public override void Bake(InputActionReferenceAuthoring authoring)
            {
                var reference = authoring.Action;
                var entity = GetEntity(TransformUsageFlags.None);

                if (reference == null)
                {
                    AddComponent(entity, default(InputActionReference));
                    return;
                }

                DependsOn(reference);

                if (reference.asset != null)
                {
                    DependsOn(reference.asset);
                }

                var action = reference.action;

                if (action == null)
                {
                    Debug.LogError("Input System for Entities: the referenced action no longer exists.", authoring);
                    AddComponent(entity, default(InputActionReference));
                    return;
                }

#if UNITY_EDITOR
                string path = UnityEditor.AssetDatabase.GetAssetPath(reference.asset);
                AddComponent(
                    entity,
                    new InputActionReference
                    {
                        AssetId = new Hash128(UnityEditor.AssetDatabase.AssetPathToGUID(path)),
                        ActionId = new Hash128(action.id.ToString("N"))
                    });
#endif
            }
        }
    }
}
