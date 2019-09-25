﻿using System.Windows.Media;
using Mapping_Tools.Classes.MathUtil;

namespace Mapping_Tools.Classes.SnappingTools {
    public interface IRelevantDrawable : IRelevantObject {
        double DistanceTo(Vector2 point);
        Vector2 NearestPoint(Vector2 point);
        bool Intersection(IRelevantObject other, out Vector2[] intersections);
        void DrawYourself(DrawingContext context, CoordinateConverter converter, SnappingToolsPreferences preferences);
    }
}