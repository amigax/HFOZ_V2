using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[WorldSystemFilter(WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.Default)]
[UpdateInGroup(typeof(RukhankaDeformationSystemGroup))]
[UpdateAfter(typeof(MeshDeformationSystem))]
partial class GPUAttachmentsUpdateSystem: SystemBase
{
    GraphicsBuffer dummyGB;
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected override void OnCreate()
    {
        var gpuAttachmentsQuery = SystemAPI.QueryBuilder()
            .WithAll<GPUAttachmentBoneIndexMPComponent>()
            .Build();
        RequireForUpdate(gpuAttachmentsQuery);
        
        //  Make small dummy bone transform buffer to prevent "attempted to draw with missing bindings" warnings and missed meshes for GPU attachments in edit mode
		dummyGB = new (GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 1, 4);
		Shader.SetGlobalBuffer(GPUAnimationSystem.ShaderID_rigSpaceBoneTransformsBuf, dummyGB);
		Shader.SetGlobalBuffer(GPUAnimationSystem.ShaderID_boneLocalTransforms, dummyGB);
    }
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected override void OnDestroy()
    {
        dummyGB?.Dispose();
    }
    
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    protected override void OnUpdate()
    {
        var animatedDataOffsetsMap =
            SystemAPI.TryGetSingleton<GPURuntimeAnimationData>(out var rad)
            ? rad.frameEntityAnimatedDataOffsetsMap
            : new (0x4, WorldUpdateAllocator);
        
        var attachmentBoneIndexUpdateJob = new UpdateGPUAttachmentBoneIndexJob()
        {
            frameEntityAnimatedDataOffsetsMap = animatedDataOffsetsMap,
            localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
            parentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            rigBoneRefLookup = SystemAPI.GetComponentLookup<AnimatorEntityRefComponent>(true),
        };
        var attachmentBoneIndexUpdateJH = attachmentBoneIndexUpdateJob.ScheduleParallel(Dependency);
        Dependency = attachmentBoneIndexUpdateJH;
        Dependency.Complete();
    }
}
}
