﻿using System.Collections.Generic;
using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObject.RelevantDrawable;

namespace Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObjectGenerators.GeneratorTypes {
    public interface IGeneratePointsFromRelevantObjects {
        List<RelevantPoint> GetRelevantObjects(List<RelevantPoint> points, List<RelevantLine> lines, List<RelevantCircle> circles);
    }
}