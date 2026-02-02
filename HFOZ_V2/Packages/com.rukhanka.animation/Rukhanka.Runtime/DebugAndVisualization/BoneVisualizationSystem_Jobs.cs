#if RUKHANKA_DEBUG_INFO

using Rukhanka.DebugDrawer;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
partial class BoneVisualizationSystem
{
[BurstCompile]
partial struct RenderBonesCPUAnimatorsJob: IJobEntity
{
	[ReadOnly]
	public NativeList<BoneTransform> bonePoses;
	[ReadOnly]
	public NativeParallelHashMap<Entity, RuntimeAnimationData.AnimatedEntityBoneDataProps> entityToDataOffsetMap;

    public uint colorTriangles;
    public uint colorLines;
    
    public Drawer drawer;
    
/////////////////////////////////////////////////////////////////////////////////

    public void Execute(Entity e, in RigDefinitionComponent rd, in BoneVisualizationComponent bvc)
    {
        var bt = RuntimeAnimationData.GetAnimationDataForRigRO(bonePoses, entityToDataOffsetMap, e);

        var len = bt.Length;
        
        for (int l = rd.rigBlob.Value.rootBoneIndex; l < len; ++l)
        {
            ref var rb = ref rd.rigBlob.Value.bones[l];

            if (rb.parentBoneIndex < 0)
                continue;

            var bonePose0 = bt[l];
            var bonePose1 = bt[rb.parentBoneIndex];

            if (math.any(math.abs(bonePose0.pos - bonePose1.pos)))
            {
                drawer.DrawBoneMesh(bonePose0.pos, bonePose1.pos, colorTriangles, colorLines);
                if (bvc.tripodSize > 0)
                {
                    var fwd = math.rotate(bonePose0.rot, math.forward()) * bvc.tripodSize;
                    var left = math.rotate(bonePose0.rot, math.up()) * bvc.tripodSize;
                    var up = math.rotate(bonePose0.rot, math.left()) * bvc.tripodSize;
                    drawer.DrawLine(bonePose0.pos, bonePose0.pos + fwd, 0x0000ffff);
                    drawer.DrawLine(bonePose0.pos, bonePose0.pos + up, 0x00ff00ff);
                    drawer.DrawLine(bonePose0.pos, bonePose0.pos + left, 0xff0000ff);
                }
            }
        }
    }
}

//------------------------------------------------------------------------------//

[BurstCompile]
[WithAll(typeof(GPUAnimationEngineTag), typeof(RigDefinitionComponent))]
partial struct PrepareGPURigsJob: IJobEntity
{
    [ReadOnly]
    public ComponentLookup<LocalTransform> localTransformLookup;
    [ReadOnly]
    public ComponentLookup<BoneVisualizationComponent> boneVisualizationLookup;
    [ReadOnly]
	public NativeParallelHashMap<Entity, GPURuntimeAnimationData.FrameOffsets> frameEntityAnimatedDataOffsetsMap;
    [NativeDisableParallelForRestriction]
    public NativeArray<GPUBoneRendererRigInfo> frameRigData;
    
/////////////////////////////////////////////////////////////////////////////////

    void Execute(Entity e)
    {
        if (!frameEntityAnimatedDataOffsetsMap.TryGetValue(e, out var rigOffsets))
            return;
        
        var rigInfo = new GPUBoneRendererRigInfo();
        if (boneVisualizationLookup.HasComponent(e))
        {
            localTransformLookup.TryGetComponent(e, out rigInfo.rigWorldPose);
        }
        frameRigData[rigOffsets.rigIndex] = rigInfo;
    }
}
}
}
#endif
