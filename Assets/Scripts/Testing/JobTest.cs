using Parallax;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct HardJob : IJob
{
    public void Execute()
    {
        double a = 0;
        for (int i = 0; i < 5000000; i++)
        {
            a += Mathf.Sqrt(i);
        }
    }
}

public class AsyncSubdivision : MonoBehaviour
{
    // Data
    readonly Vector3[] originalVerts;
    readonly Vector3[] originalNormals;
    readonly Color[] originalColors;
    readonly int[] originalTris;

    private Mesh mesh;

    // Threadsafe params
    bool workCompleted = true;

    void Start()
    {
        Initialize();
    }
    // Get original mesh data 
    void Initialize()
    {
        mesh = GetComponent<MeshFilter>().sharedMesh;

        //originalVerts = mesh.vertices;

    }
    void Update()
    {
        float startTime = Time.realtimeSinceStartup;
        
        NativeList<JobHandle> handles = new NativeList<JobHandle>(Allocator.Temp);
        for (int i = 0; i < 15; i++)
        {
            JobHandle handle = DispatchJob();
            handles.Add(handle);
        }
        JobHandle.CompleteAll(handles);
        handles.Dispose();
        Debug.Log("Time elapsed: " + (Time.realtimeSinceStartup -  startTime));
    }
    public JobHandle DispatchJob()
    {
        HardJob job = new HardJob();
        return job.Schedule();
    }
}
