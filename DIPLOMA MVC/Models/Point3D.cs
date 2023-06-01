﻿using DelaunatorSharp;

namespace DIPLOMA_MVC.Controllers;

public class Point3D : IPoint
{
    public Point3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}