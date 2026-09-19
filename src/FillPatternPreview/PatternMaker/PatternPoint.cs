
using System;

namespace FillPatternPreview.PatternMaker;
public class PatternPoint
{
    public double U { get; private set; }
    public double V { get; private set; }

    public PatternPoint(double uPoint, double vPoint)
    {
        U = RoundVector(uPoint);
        V = RoundVector(vPoint);
    }

    public override string ToString()
    {
        return $"<PatternPoint U:{U:F20} V:{V:F20}>";
    }

    public override bool Equals(object obj)
    {
        if (obj is PatternPoint other)
        {
            return U == other.U && V == other.V;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(U, V);
    }

    public static PatternPoint operator +(PatternPoint a, PatternPoint b)
    {
        return new PatternPoint(a.U + b.U, a.V + b.V);
    }

    public static PatternPoint operator -(PatternPoint a, PatternPoint b)
    {
        return new PatternPoint(a.U - b.U, a.V - b.V);
    }

    public double DistanceTo(PatternPoint point)
    {
        return Math.Sqrt(Math.Pow(point.U - U, 2) + Math.Pow(point.V - V, 2));
    }

    public bool Rotate(double angle, PatternPoint origin = null)
    {
        origin = origin ?? new PatternPoint(0, 0);
        double tu = U - origin.U;
        double tv = V - origin.V;
        U = origin.U + (tu * Math.Cos(angle) - tv * Math.Sin(angle));
        V = origin.V + (tu * Math.Sin(angle) + tv * Math.Cos(angle));
        return true;
    }

    private double RoundVector(double length)
    {
        const double ZeroTol = 5e-06;
        const int CoordResolution = 15;
        length = Math.Abs(length) > ZeroTol ? length : 0.0;
        return Math.Round(length, CoordResolution);
    }
}
