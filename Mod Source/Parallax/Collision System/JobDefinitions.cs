using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Parallax
{
    [BurstCompile]
    public struct InitalizeArray : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> array;
        [ReadOnly] public float initializeTo;
        void IJobParallelFor.Execute(int index)
        {
            array[index] = initializeTo;
        }
    }
}
