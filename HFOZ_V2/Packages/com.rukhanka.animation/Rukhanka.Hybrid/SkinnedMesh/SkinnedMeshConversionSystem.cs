using Unity.Entities;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Hybrid
{
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[UpdateAfter(typeof(RigDefinitionConversionSystem))]
public partial class SkinnedMeshConversionSystem : SystemBase
{
	protected override void OnUpdate()
	{
		var actualizeSkinnedMeshDataJob = new ActualizeSkinnedMeshDataJob()
		{
			animEntityRefLookup = SystemAPI.GetComponentLookup<AnimatorEntityRefComponent>(true),
		};
		actualizeSkinnedMeshDataJob.ScheduleParallel();
	}
}
}
