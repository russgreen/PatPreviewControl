using System;

namespace FillPatternPreview.PatternMaker;
public class PatternLine
{

    public PatternPoint StartPoint { get; private set; }
    public PatternPoint EndPoint { get; private set; }
    private readonly XYZ uVector = new XYZ(1, 0, 0);

    public PatternLine(PatternPoint startP, PatternPoint endP)
    {
        StartPoint = startP.V <= endP.V ? startP : endP;
        EndPoint = startP.V <= endP.V ? endP : startP;
    }

    public override string ToString()
    {
        return $"<PatternLine Start:{StartPoint} End:{EndPoint} Length:{Length} Angle:{Angle}>";
    }

    public PatternPoint Direction
    {
        get
        {
            return new PatternPoint(EndPoint.U - StartPoint.U, EndPoint.V - StartPoint.V);
        }
    }

    public double Angle
    {
        get
        {
            // always return angle to u direction
            // todo: fix and use actual math for angles to remove revit dependency
            return uVector.AngleTo(new XYZ(Direction.U, Direction.V, 0));
        }
    }

    public PatternPoint CenterPoint
    {
        get
        {
            return new PatternPoint((EndPoint.U + StartPoint.U) / 2.0, (EndPoint.V + StartPoint.V) / 2.0);
        }
    }

    public double Length
    {
        get
        {
            return Math.Abs(Math.Sqrt(Direction.U * Direction.U + Direction.V * Direction.V));
        }
    }

    public bool PointOnLine(PatternPoint point, double tolerance = 5e-06)
    {
        var a = StartPoint;
        var b = EndPoint;
        var c = point;
        return 0.0 <= Math.Abs((a.U - c.U) * (b.V - c.V) - (a.V - c.V) * (b.U - c.U)) && Math.Abs((a.U - c.U) * (b.V - c.V) - (a.V - c.V) * (b.U - c.U)) <= tolerance;
    }

    public PatternPoint Intersect(PatternLine patLine)
    {
        var xdiff = new PatternPoint(StartPoint.U - EndPoint.U, patLine.StartPoint.U - patLine.EndPoint.U);
        var ydiff = new PatternPoint(StartPoint.V - EndPoint.V, patLine.StartPoint.V - patLine.EndPoint.V);

        double Det(PatternPoint a, PatternPoint b)
        {
            return a.U * b.V - a.V * b.U;
        }

        var div = Det(xdiff, ydiff);
        if (div == 0)
        {
            throw new Exception("Lines do not intersect.");
        }

        var d = new PatternPoint(Det(StartPoint, EndPoint), Det(patLine.StartPoint, patLine.EndPoint));
        var intPointX = Det(d, xdiff) / div;
        var intPointY = Det(d, ydiff) / div;

        return new PatternPoint(intPointX, intPointY);
    }

    public void Rotate(double angle, PatternPoint origin = null)
    {
        StartPoint.Rotate(angle, origin);
        EndPoint.Rotate(angle, origin);
    }
}
