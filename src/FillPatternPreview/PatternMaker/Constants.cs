using System;

namespace FillPatternPreview.PatternMaker;
internal static class Constants
{
    public const double PI = Math.PI;
    public const double HALF_PI = PI / 2.0;
    public const double ZERO_TOL = 5e-06;
    //public const int COORD_RESOLUTION = 16;
    public const double MAX_MODEL_DOMAIN = 100.0;
    public const double MAX_DETAIL_DOMAIN = MAX_MODEL_DOMAIN / 10.0;
    public const int MAX_DOMAIN_MULT = 8;
    public const int RATIO_RESOLUTION = 2;
    public const double ANGLE_CORR_RATIO = 0.01;
    public const string PAT_SEPARATOR = ", ";
    public const string PAT_FILE_TEMPLATE =
        ";        Written by ECE Tools Maker Pattern" +
        ";-Date                                   : {0}\n" +
        ";-Time                                   : {1}\n" +
        ";---------------------------------------------------------------------\n" +
        ";%UNITS={2}\n" +
        "*{3},exported by pyRevit\n" +
        ";%TYPE={4}\n";
}
