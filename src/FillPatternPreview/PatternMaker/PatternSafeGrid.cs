using System;

namespace FillPatternPreview.PatternMaker;
public class PatternSafeGrid
{
    private PatternPoint _domain;
    private bool _flipped;
    private double _diagAngle;
    private PatternLine _axisLine;
    private double _offsetDirection;
    private double _angle;
    private int _uTiles;
    private int _vTiles;
    private double _domainU;
    private double _domainV;

    public PatternSafeGrid(PatternPoint domain, double diagAngle, int uTiles, int vTiles, bool flipped = false)
    {
        _domain = domain;
        _flipped = flipped;
        _diagAngle = diagAngle;
        _axisLine = new PatternLine(new PatternPoint(0, 0), new PatternPoint(_domain.U * uTiles, _domain.V * vTiles));
        DetermineAbstractParams(uTiles, vTiles);
    }

    public override bool Equals(object obj)
    {
        return obj is PatternSafeGrid other && Math.Abs(GridAngle - other.GridAngle) <= Constants.ZERO_TOL;
    }

    public override int GetHashCode()
    {
        return GridAngle.GetHashCode();
    }

    private void DetermineAbstractParams(int uTiles, int vTiles)
    {
        if (_axisLine.Angle <= _diagAngle)
        {
            _offsetDirection = _flipped ? 1.0 : -1.0;
            _angle = _axisLine.Angle;
            _uTiles = uTiles;
            _vTiles = vTiles;
            _domainU = _domain.U;
            _domainV = _domain.V;
        }
        else
        {
            _offsetDirection = _flipped ? -1.0 : 1.0;
            _angle = _flipped ? _axisLine.Angle - Constants.HALF_PI : Constants.HALF_PI - _axisLine.Angle;
            _uTiles = vTiles;
            _vTiles = uTiles;
            _domainU = _domain.V;
            _domainV = _domain.U;
        }
    }

    public bool IsValid()
    {
        return Shift != null;
    }

    public override string ToString()
    {
        return $"<PatternSafeGrid GridAngle:{GridAngle} Angle:{_angle} U_Tiles:{_uTiles} V_Tiles:{_vTiles} Domain_U:{_domainU} Domain_V:{_domainV} Offset_Dir:{_offsetDirection} Span:{Span} Offset:{Offset} Shift:{Shift}>";
    }

    public double GridAngle => _flipped ? Math.PI - _axisLine.Angle : _axisLine.Angle;

    public double Span => _axisLine.Length;

    public double Offset
    {
        get
        {
            if (_angle == 0.0)
            {
                return _domainV * _offsetDirection;
            }
            else
            {
                return Math.Abs(_domainU * Math.Sin(_angle) / _vTiles) * _offsetDirection;
            }
        }
    }

    public double? Shift
    {
        get
        {
            if (_angle == 0.0)
            {
                return 0;
            }

            PatternPoint FindNextGridPoint(PatternLine offsetLine)
            {
                int uMult = 0;
                while (uMult < _uTiles)
                {
                    for (int vMult = 0; vMult < _vTiles; vMult++)
                    {
                        var gridPoint = new PatternPoint(_domainU * uMult, _domainV * vMult);
                        if (offsetLine.PointOnLine(gridPoint))
                        {
                            return gridPoint;
                        }
                    }
                    uMult++;
                }
                if (uMult >= _uTiles)
                {
                    Console.WriteLine("Can not determine next repeating grid.");
                    return null;
                }
                return null;
            }

            if (_uTiles == 1 && _vTiles == 1)
            {
                return Math.Abs(_domainU * Math.Cos(_angle));
            }
            else
            {
                var offsetU = Math.Abs(Offset * Math.Sin(_angle));
                var offsetV = -Math.Abs(Offset * Math.Cos(_angle));
                var offsetVector = new PatternPoint(offsetU, offsetV);

                var abstractAxisStartPoint = new PatternPoint(0, 0);
                var abstractAxisEndPoint = new PatternPoint(_domainU * _uTiles, _domainV * _vTiles);
                var offsetVectorStart = abstractAxisStartPoint + offsetVector;
                var offsetVectorEnd = abstractAxisEndPoint + offsetVector;
                var offsetAxis = new PatternLine(offsetVectorStart, offsetVectorEnd);

                var nextGridPoint = FindNextGridPoint(offsetAxis);

                if (nextGridPoint != null)
                {
                    return offsetAxis.StartPoint.DistanceTo(nextGridPoint);
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
