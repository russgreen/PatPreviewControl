using System;
using System.Collections.Generic;
using System.Linq;


namespace FillPatternPreview.PatternMaker;
public class PatternGrid
{

    private PatternDomain _domain;
    private PatternSafeGrid _grid;
    private List<PatternLine> segmentLines;

    public double Angle { get; private set; }
    public double Span { get; private set; }
    public double Offset { get; private set; }
    public double Shift { get; private set; }

    public PatternGrid(PatternDomain patDomain, PatternLine initLine)
    {
        _domain = patDomain;
        _grid = _domain.GetBestAngle(initLine.Angle);
        Console.WriteLine($"Closest safe angle is: {_grid}");
        Angle = _grid.GridAngle;
        Span = _grid.Span;
        Offset = _grid.Offset;
        Shift = (double)_grid.Shift;

        segmentLines = new List<PatternLine>();
        initLine.Rotate(Angle - initLine.Angle, initLine.CenterPoint);
        segmentLines.Add(initLine);
    }

    public override string ToString()
    {
        return $"<_PatternGrid Angle:{Angle} Span:{Span} Offset:{Offset} Shift:{Shift}>";
    }

    public bool AdoptLine(PatternLine patLine)
    {
        // todo: optimise grid creation. check overlap and combine
        // overlapping lines into one grid
        return false;
    }

    public PatternPoint Origin
    {
        get
        {
            var pointList = new List<PatternPoint>();
            foreach (var segLine in segmentLines)
            {
                pointList.Add(segLine.StartPoint);
                pointList.Add(segLine.EndPoint);
            }


            if (Angle <= (Constants.HALF_PI))
            {
                return pointList.OrderBy(x => x.DistanceTo(new PatternPoint(0, 0))).First();
            }
            else
            {
                return pointList.OrderBy(x => x.DistanceTo(new PatternPoint(_domain.Uvec.Length, 0))).First();
            }
        }
    }

    public List<double> Segments
    {
        get
        {
            var penDown = segmentLines[0].Length;
            return new List<double> { penDown, Span - penDown };
        }
    }

    public List<PatternLine> SegmentsAsLines
    {
        get
        {
            // todo: see _RevitPattern.adjust_line()
            return segmentLines;
        }
    }
}
