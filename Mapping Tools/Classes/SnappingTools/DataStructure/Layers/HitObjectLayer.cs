﻿using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObject;
using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObjectCollection;
using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObjectGenerators;
using Mapping_Tools.Classes.SnappingTools.DataStructure.RelevantObjectGenerators.GeneratorCollection;

namespace Mapping_Tools.Classes.SnappingTools.DataStructure.Layers {
    /// <summary>
    /// Container for a list of HitObjects
    /// </summary>
    public class HitObjectLayer : ObjectLayer {
        // This list must always be sorted by time
        public HitObjectCollection HitObjects {
            get => (HitObjectCollection) Objects;
            set => Objects = value;
        }

        public override HitObjectGeneratorCollection GeneratorCollection { get; set; }
        public RelevantObjectLayer NextLayer;

        public override void Add(object obj) {
            if (!(obj is RelevantHitObject hitObject)) return;

            // Check if this object or something similar exists anywhere in the context or in this layer
            if (HitObjects.FindSimilar(hitObject, ParentCollection.AcceptableDifference, out var similarObject)) {
                similarObject.Consume(hitObject);
            }

            Objects.SortedInsert(hitObject);

            NextLayer.DeleteObjectsFromConcurrent();

            GeneratorCollection.GenerateNewObjects(this, NextLayer, hitObject);


            // Sort the object list
            // Redo any generators that need concurrent HitObjects
            // An object generated by a concurrent generator only cares about the index distance of the whole context of their parents to stay the same.
            // An object must remember what generator generated it.
            // An object becomes invalid once any of the parents become invalid or its generator got disabled
            // The entire context must be updated on a change of any layer
            // Before adding the object it must be checked if there are similar objects in the whole context
            // Similar in whole context means the object won't be added
            // Similar in this layer will merge it with that object
            // How do I know for concurrent generators which gaps to fill?
            // A concurrent generator will loop through it's context in the changed area to find the objects to invalidate and new objects to generate
            // Context is the sorted previous layer or all previous layers sorted by time
            // Non-concurrent generators will never have to remove objects from an add event
            // Generate objects to add to the next layer
            // Invoke event so the layer collection can update the next layer
        }
    }
}
