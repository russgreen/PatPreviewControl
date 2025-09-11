using System;
using System.Collections.Generic;
using System.Linq;

namespace FillPatternPreview.PatternMaker;
public class PatternDomain
{
    public PatternLine Uvec;
    public PatternLine Vvec;

    private PatternPoint _origin; 
    private PatternPoint _corner;
    private PatternPoint _bounds;
    private PatternPoint _normalizedDomain;

    private double _maxDomain;
    private bool _expandable;
    private double _targetDomain;
    private PatternLine diagonal;
    private List<PatternSafeGrid> safeAngles;

    public PatternDomain(double startU, double startV, double endU, double endV, bool modelPattern, bool expandable)
    {
        _origin = new PatternPoint(Math.Min(startU, endU), Math.Min(startV, endV));
        _corner = new PatternPoint(Math.Max(startU, endU), Math.Max(startV, endV));
        _bounds = _corner - _origin;
        _normalizedDomain = new PatternPoint(1.0, 1.0 * (_bounds.V / _bounds.U));

        if (ZeroDomain())
        {
            throw new Exception("Can not process zero domain.");
        }

        Uvec = new PatternLine(new PatternPoint(0, 0), new PatternPoint(_bounds.U, 0));
        Vvec = new PatternLine(new PatternPoint(0, 0), new PatternPoint(0, _bounds.V));

        _maxDomain = modelPattern ? Constants.MAX_MODEL_DOMAIN : Constants.MAX_DETAIL_DOMAIN;
        _expandable = expandable;
        _targetDomain = _maxDomain;

        diagonal = new PatternLine(new PatternPoint(0.0, 0.0), new PatternPoint(_bounds.U, _bounds.V));

        CalculateSafeAngles();
    }

    public override string ToString()
    {
        return $"<PatternDomain U:{_bounds.U} V:{_bounds.V} SafeAngles:{safeAngles.Count}>";
    }

    private bool ZeroDomain()
    {
        return _bounds.U == 0 || _bounds.V == 0;
    }

    private void CalculateSafeAngles()
    {
        int uMult = 1, vMult = 1;
        safeAngles = new List<PatternSafeGrid>();
        HashSet<double> processedRatios = new HashSet<double> { 1.0 };

        safeAngles.Add(new PatternSafeGrid(_bounds, diagonal.Angle, uMult, 0));
        safeAngles.Add(new PatternSafeGrid(_bounds, diagonal.Angle, uMult, 0, true));
        safeAngles.Add(new PatternSafeGrid(_bounds, diagonal.Angle, uMult, vMult));
        safeAngles.Add(new PatternSafeGrid(_bounds, diagonal.Angle, uMult, vMult, true));
        safeAngles.Add(new PatternSafeGrid(_bounds, diagonal.Angle, 0, vMult));

        while (_bounds.U * uMult <= _targetDomain / 2.0)
        {
            vMult = 1;
            while (_bounds.V * vMult <= _targetDomain / 2.0)
            {
                double ratio = Math.Round(vMult / (double)uMult, Constants.RATIO_RESOLUTION);
                if (!processedRatios.Contains(ratio))
                {
                    var angle1 = new PatternSafeGrid(_bounds, diagonal.Angle, uMult, vMult);
                    var angle2 = new PatternSafeGrid(_bounds, diagonal.Angle, uMult, vMult, true);

                    if (angle1.IsValid() && angle2.IsValid())
                    {
                        safeAngles.Add(angle1);
                        safeAngles.Add(angle2);
                        processedRatios.Add(ratio);
                    }
                    else
                    {
                        Console.WriteLine("Skipping safe angle for grid point U:{uMult} V:{vMult}", uMult, vMult);
                    }
                }
                vMult++;
            }
            uMult++;
        }
    }

    public bool Expand()
    {
        if (_targetDomain > _maxDomain * Constants.MAX_DOMAIN_MULT)
        {
            return false;
        }
        else
        {
            _targetDomain += _maxDomain / 2;
            CalculateSafeAngles();
            return true;
        }
    }

    public PatternLine GetDomainCoords(PatternLine patLine)
    {
        return new PatternLine(patLine.StartPoint - _origin, patLine.EndPoint - _origin);
    }

    public PatternSafeGrid GetGridParams(double axisAngle)
    {
        return safeAngles.OrderBy(x => Math.Abs(x.GridAngle - axisAngle)).First();
    }

    public double GetRequiredCorrection(double axisAngle)
    {
        return Math.Abs(axisAngle - GetGridParams(axisAngle).GridAngle);
    }

    public PatternSafeGrid GetBestAngle(double axisAngle)
    {
        if (_expandable)
        {
            while (GetRequiredCorrection(axisAngle) >= Constants.ANGLE_CORR_RATIO)
            {
                if (!Expand())
                {
                    break;
                }
            }
        }
        return GetGridParams(axisAngle);
    }
}
