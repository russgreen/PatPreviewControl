using System;

namespace FillPatternPreview.PatternMaker;

public class XYZ
{
    private readonly double z;
    private readonly double y;
    private readonly double x;

    public double Z => z;
    public double Y => y;
    public double X => x;

    public XYZ(double x, double y, double z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public double AngleTo(XYZ vec)
    {
        if (vec == null)
        {
            throw new ArgumentNullException(nameof(vec));
        }

        var dot = DotProduct(vec);
        var magA = Math.Sqrt(X * X + Y * Y + Z * Z);
        var magB = Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
        if (magA == 0 || magB == 0)
        {
            return 0.0;
        }

        var cosAngle = dot / (magA * magB);
        cosAngle = Math.Clamp(cosAngle, -1.0, 1.0);
        return Math.Acos(cosAngle);
    }

    private double DotProduct(XYZ vec) => X * vec.X + Y * vec.Y + Z * vec.Z;
}