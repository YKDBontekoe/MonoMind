using System;
using System.Numerics;
using Autonocraft.Engine;
using Autonocraft.Domain.Rendering;
using Autonocraft.Engine.Animation;
using Autonocraft.Entities;
using Autonocraft.Village;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class AnimationPoseTests
{
    [Fact]
    public void InteractionAnimator_ExpandsSwingPoseByKind()
    {
        var animator = new InteractionAnimator();
        animator.TriggerSwing(SwingKind.Melee);
        animator.Update(0.12f, new MotionStub());

        float meleeSwing = animator.GetHeldItemSwingDegrees();
        float meleeOffsetX = animator.GetHeldItemOffsetX();
        float meleeOffsetZ = animator.GetHeldItemOffsetZ();

        animator = new InteractionAnimator();
        animator.TriggerSwing(SwingKind.Mine);
        animator.Update(0.12f, new MotionStub());

        float mineSwing = animator.GetHeldItemSwingDegrees();
        float mineOffsetX = animator.GetHeldItemOffsetX();
        float mineOffsetZ = animator.GetHeldItemOffsetZ();

        Assert.True(MathF.Abs(meleeSwing) > MathF.Abs(mineSwing));
        Assert.True(meleeOffsetX < mineOffsetX);
        Assert.True(meleeOffsetZ > mineOffsetZ);
        Assert.NotEqual(Vector3.Zero, animator.PositionOffset);
    }

    [Fact]
    public void AnimalWalkPhase_AdvancesForIdleAndMovingStates()
    {
        var animal = new Animal(AnimalType.Sheep, new Vector3(10f, 64f, 10f), 17);

        float idleStart = AnimalVisuals.GetWalkPhase(animal, 0f);
        float idleLater = AnimalVisuals.GetWalkPhase(animal, 2f);

        animal.WanderDirection = Vector3.Normalize(new Vector3(1f, 0f, 0f));
        animal.WanderDistanceRemaining = 3f;
        float movingPhase = AnimalVisuals.GetWalkPhase(animal, 2f);

        Assert.NotEqual(idleStart, idleLater);
        Assert.NotEqual(idleLater, movingPhase);
    }

    [Fact]
    public void VillagerWalkPhase_AdvancesForIdleAndPathingStates()
    {
        var villager = new Villager(1, new Vector3(12f, 64f, 12f), 31);

        float idleStart = VillagerVisuals.GetWalkPhase(villager, 0f);
        float idleLater = VillagerVisuals.GetWalkPhase(villager, 2f);

        villager.WanderDirection = Vector3.Normalize(new Vector3(0f, 0f, 1f));
        villager.WanderDistanceRemaining = 2f;
        villager.SetAiPhase(VillagerAiPhase.PathTo);
        float movingPhase = VillagerVisuals.GetWalkPhase(villager, 2f);

        Assert.NotEqual(idleStart, idleLater);
        Assert.NotEqual(idleLater, movingPhase);
    }

    private sealed class MotionStub : IPlayerMotionView
    {
        public Vector3 Velocity => new(2f, 0f, 0f);
        public bool IsGrounded => true;
        public bool CreativeMode => false;
    }
}
