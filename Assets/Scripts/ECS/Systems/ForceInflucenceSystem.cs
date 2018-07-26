using Unity.Collections;
using Unity.Entities;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateBefore(typeof(ForceVelocitySystem))]
[UpdateAfter(typeof(CopyTransformToGameObject))]
public class ForceInfluenceSystem : JobComponentSystem {
  public struct ForceRecievers {
    public readonly int Length;
    public ComponentDataArray<Physical> physical;
    [ReadOnly] public ComponentDataArray<Position> position;
  }

  [Inject] private ForceRecievers forceRecievers;
  
  public struct ForceGenerators {
    public readonly int Length;
    [ReadOnly] public ComponentDataArray<Position> position;
    [ReadOnly] public ComponentDataArray<ForceGenerator> forceGenerator;
  }

  [Inject] private ForceGenerators forceGenerators;

  [BurstCompile]
  struct ForceInfluenceJob : IJobParallelFor {
    public ComponentDataArray<Physical> recieverPhysicals;
    [ReadOnly] public ComponentDataArray<Position> recieverPositions;
    [ReadOnly] public ComponentDataArray<Position> generatorPositions;
    [ReadOnly] public ComponentDataArray<ForceGenerator> generatorForces;
    [ReadOnly] public int generatorCount;

    public void Execute(int i) {
      // V1
      for (int j = 0; j < generatorCount; ++j) {
        float3 distance = recieverPositions[i].Value - generatorPositions[j].Value;
        float distanceMagSqr = math.lengthSquared(distance);
        if (distanceMagSqr < generatorForces[j].distance * generatorForces[j].distance) {
          float f = 1 - math.sqrt(distanceMagSqr) / generatorForces[j].distance;
          recieverPhysicals[i] = new Physical{ 
            Force = recieverPhysicals[i].Force + math.normalize(distance) * f * f * generatorForces[j].force,
            Mass = recieverPhysicals[i].Mass
          };
        }
      }
    }
  }

  protected override JobHandle OnUpdate(JobHandle inputDeps) {
    if (forceRecievers.Length == 0 || forceGenerators.Length == 0)
      return inputDeps;

    ForceInfluenceJob job = new ForceInfluenceJob {
      recieverPhysicals = forceRecievers.physical,
      recieverPositions = forceRecievers.position,
      generatorPositions = forceGenerators.position,
      generatorForces = forceGenerators.forceGenerator,
      generatorCount = forceGenerators.Length
    };

    JobHandle jobHandle = job.Schedule(forceRecievers.Length, 64, inputDeps);
    return jobHandle;
  }
}