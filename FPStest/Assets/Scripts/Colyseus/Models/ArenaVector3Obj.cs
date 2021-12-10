using System;
using UnityEngine;

[Serializable]
public class ExampleVector3Obj
{
    public ExampleVector3Obj()
    {
        x = 0;
        y = 0;
        z = 0;
    }

    public ExampleVector3Obj(Vector3 vector3)
    {
        x = vector3.x;
        y = vector3.y;
        z = vector3.z;
    }

    public double x { get; set; }
    public double y { get; set; }
    public double z { get; set; }
}