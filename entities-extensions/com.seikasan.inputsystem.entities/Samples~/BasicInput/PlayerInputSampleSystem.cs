using InputActions = InputSystem.Entities.Generated.PlayerControls.InputActionAsset;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace InputSystem.Entities.Samples
{
    public struct PlayerInputSampleState : IComponentData
    {
        public float2 Move;
        public bool JumpPressed;
    }

    [BurstCompile]
    public partial struct PlayerInputSampleSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateEntity(typeof(PlayerInputSampleState));
            state.RequireForUpdate<InputActions>();
            state.RequireForUpdate<PlayerInputSampleState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var actions = SystemAPI.GetSingleton<InputActions>();
            SystemAPI.SetSingleton(new PlayerInputSampleState
            {
                Move = actions.Gameplay.Move.ReadValue<float2>(),
                JumpPressed = actions.Gameplay.Jump.WasPressedThisFrame()
            });
        }
    }
}
