using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class AsyncSubdivision : MonoBehaviour
{
    // Start is called before the first frame update
    bool workCompleted = true;
    async void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (workCompleted)
        {
            Dispatch();
        }
    }
    public async void Dispatch()
    {
        workCompleted = false;

        double result = await Task.Run(() => ComplexCalculation());
        Debug.Log("Task completed");

        workCompleted = true;
    }
    public double ComplexCalculation()
    {
        double result = 0;
        for (int i = 0; i < 10000000; i++)
        {
            float distance = Vector3.Distance(Vector3.zero, Vector3.one * 3);
            distance = Mathf.Pow(distance, 10);
            distance = Mathf.Sqrt(distance);
            result += distance;
        }
        return result;
    }
}
