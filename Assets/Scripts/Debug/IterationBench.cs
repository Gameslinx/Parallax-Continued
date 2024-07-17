using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;

public class MyClass
{
    public Vector3 MyVector;
    public Vector3 one;
    public float dist = 0;

    public MyClass(Vector3 vector)
    {
        MyVector = vector;
    }

    public void AddAndNormalize()
    {
        //dist = (MyVector - Vector3.one).sqrMagnitude;
        float dx = (MyVector.x - one.x);
        float dy = (MyVector.y - one.y);
        float dz = (MyVector.z - one.z);

        dist = dx * dx + dy * dy + dz * dz;
    }
}
public class Bclass
{
    public Bclass(int test1, Vector3 test2) 
    {
        this.test1 = test1;
        this.test2 = test2;
    }
    int test1 = 0;
    Vector3 test2 = Vector3.one * 4;
}

public class IterationBench : MonoBehaviour
{
    private Dictionary<Bclass, MyClass> myDictionary = new Dictionary<Bclass, MyClass>(10000);
    List<MyClass> myList = new List<MyClass>(10000);
    delegate void MyEvent();
    event MyEvent myEvent;

    float dist = 0;

    void Start()
    {
        
        // Instantiate and populate data structures with 1000 instances of MyClass
        for (int i = 0; i < 10000; i++)
        {
            MyClass instance = new MyClass(new Vector3(i, i, i));
            Bclass b = new Bclass(i, Vector3.one * i);
            myDictionary.Add(b, instance);
            myList.Add(instance);
            myEvent += instance.AddAndNormalize;

            
        }

        MyClass last = myList[9999];

        // Benchmark dictionary iteration
        Stopwatch stopwatch = Stopwatch.StartNew();
        foreach (KeyValuePair<Bclass, MyClass> kvp in myDictionary)
        {
            kvp.Value.AddAndNormalize();
        }
        stopwatch.Stop();
        UnityEngine.Debug.Log($"Dictionary iteration time: {stopwatch.Elapsed.TotalMilliseconds.ToString("F10")} ms");

        // Benchmark list iteration
        stopwatch.Restart();
        foreach (MyClass item in myList)
        {
            item.AddAndNormalize();
        }
        stopwatch.Stop();
        UnityEngine.Debug.Log($"List iteration time: {stopwatch.Elapsed.TotalMilliseconds.ToString("F10")} ms");

        // Benchmark event invocation
        stopwatch.Restart();
        myEvent?.Invoke();
        stopwatch.Stop();
        UnityEngine.Debug.Log($"Event invocation time: {stopwatch.Elapsed.TotalMilliseconds.ToString("F10")} ms");

        stopwatch.Restart();
        myList.Remove(last);
        stopwatch.Stop();
        UnityEngine.Debug.Log($"Remove invocation time: {stopwatch.Elapsed.TotalMilliseconds.ToString("F10")} ms");
    }
}