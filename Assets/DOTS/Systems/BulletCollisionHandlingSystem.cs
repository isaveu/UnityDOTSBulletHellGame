﻿using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Collections;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Burst;
public class BulletCollisionHandlingSystem : JobComponentSystem
{
    private EntityCommandBufferSystem bufferSystem;
    protected override void OnCreate()
    {
        bufferSystem = World.Active.GetOrCreateSystem<EntityCommandBufferSystem>();
    }

    struct BulletCollisionHandlingJob : IJobForEachWithEntity<PhysicsVelocity, LocalToWorld, BulletCollisionComponent>
    {
        [ReadOnly] public EntityCommandBuffer CommandBuffer;
        [ReadOnly] public PhysicsWorld _World;
        [ReadOnly] public ComponentDataFromEntity<Health> HealthComponents;
        [ReadOnly] public ComponentDataFromEntity<ExplosionComponent> ExplosionComponents;
        [ReadOnly] public ComponentDataFromEntity<Translation> TranslationComponents;
        public void Execute(Entity e, int index,
            ref PhysicsVelocity physicsVelocity, ref LocalToWorld localToWorld, ref BulletCollisionComponent bulletCollision)
        {
            if (bulletCollision.Exploded)
                return;
            RaycastInput input = new RaycastInput
            {

                Start = localToWorld.Position,
                End = localToWorld.Forward,
                Filter = new CollisionFilter()
                {
                    // MaskBits is "A bitmask which describes which layers a collider belongs too"
                    //CategoryBits is "A bitmask which describes which layers this collider should interact with"
                    BelongsTo = ~0u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                }
            };
            bulletCollision.Raycasting = _World.CollisionWorld.CastRay(input, out RaycastHit hit);
            if (bulletCollision.Raycasting)
            {
                bulletCollision.Exploded = true;
                bulletCollision.CastDistance = math.distance(localToWorld.Position, hit.Position);
                if (bulletCollision.CastDistance < .5f)
                {
                    if (hit.RigidBodyIndex == -1)
                        return;
                    var collisionEntity = _World.Bodies[hit.RigidBodyIndex].Entity;
                    if (!HealthComponents.Exists(collisionEntity))
                        return;
                    var healthComponent = HealthComponents[collisionEntity];
                    var explosionComponent = ExplosionComponents[collisionEntity];
                    Health health = new Health
                    {
                        Value = healthComponent.Value - 10
                    };
                    CommandBuffer.SetComponent(collisionEntity, health);

                    ExplosionComponent exposion = new ExplosionComponent
                    {
                        Prefab = explosionComponent.Prefab,
                        Position = TranslationComponents[collisionEntity].Value
                    };
                    CommandBuffer.SetComponent(collisionEntity, exposion);
                    CommandBuffer.DestroyEntity(e);//Destroy Bullet
                }
            }
        }

    }

    struct HealthManagementJob : IJobForEachWithEntity<Health, ExplosionComponent>
    {
        [ReadOnly] public EntityCommandBuffer CommandBuffer;
        public void Execute(Entity e, int index, ref Health health, ref ExplosionComponent explosion)
        {
            if (health.Value > 0f)
                return;
            var explosive = CommandBuffer.Instantiate(explosion.Prefab);
            Translation position = new Translation
            {
                Value = explosion.Position
            };
            CommandBuffer.SetComponent(explosive, position);
            CommandBuffer.AddComponent(e, new Disabled());
        }
    }

    struct ParentDisableCheckJob : IJobForEachWithEntity<Parent>
    {
        [ReadOnly] public EntityCommandBuffer CommandBuffer;
        [ReadOnly] public ComponentDataFromEntity<Disabled> DisabledEntities;
        public void Execute(Entity entity, int index, ref Parent parent)
        {
            if (DisabledEntities.Exists(parent.Value))
                CommandBuffer.AddComponent(entity, new Disabled());
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        EntityCommandBuffer buffer = bufferSystem.CreateCommandBuffer();
        var job = new BulletCollisionHandlingJob
        {
            _World = World.Active.GetExistingSystem<BuildPhysicsWorld>().PhysicsWorld,
            CommandBuffer = buffer,
            HealthComponents = GetComponentDataFromEntity<Health>(true),
            ExplosionComponents = GetComponentDataFromEntity<ExplosionComponent>(true),
            TranslationComponents = GetComponentDataFromEntity<Translation>(true)
        }.Schedule(this, inputDeps);
        bufferSystem.AddJobHandleForProducer(job);
        job.Complete();

        var healthManagementJob = new HealthManagementJob
        {
            CommandBuffer = buffer,
        }.Schedule(this, job);

        var disablerJob = new ParentDisableCheckJob
        {
            CommandBuffer = buffer,
            DisabledEntities = GetComponentDataFromEntity<Disabled>(true)
        }.Schedule(this, healthManagementJob);
        return disablerJob;
    }
}

public struct BulletCollisionComponent : IComponentData
{
    public Entity ExplosionPrefab;
    public bool Raycasting;
    public float CastDistance;
    public bool Exploded;
    public int InteractionLayer;
    public int BelongingLayer;
    public bool BelongToPlayer;
}
