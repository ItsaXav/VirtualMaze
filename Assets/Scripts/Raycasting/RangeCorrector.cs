using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace RangeCorrector{
    public class RangeCorrector {

        private Rect  originalRange;
        //x-start, x-end, y-start, y-end
        private Rect newRange; // Target range
        

        /// <summary>
        /// Width of HD screen res (1920x1080), HD should be the default for most experiments
        /// </summary>
        public static const int HD_WIDTH = 1920;

        /// <summary>
        /// Height of HD, HD should be the default for most experiments
        /// </summary>
        public static const int HD_HEIGHT = 1080;

        public static const Rect VIEWPORT_RECT = new Rect(0,0,1,1);


        private static readonly Lazy<RangeCorrector> HD_TO_VIEWPORT_INTERNAL = 
        new Lazy<RangeCorrector>(() => new RangeCorrector(
            new Rect(0,0,HD_WIDTH,HD_HEIGHT),
            VIEWPORT_RECT
            ));
        
        
        

        /// <summary>
        /// Returns a range corrector to use for HD to viewport space, i.e. normalises assuming original is 1920x1080
        /// </summary>
        /// <value> the RangeCorrector object that normalises values captured on 1920x1080 space</value>
        public static RangeCorrector HD_TO_VIEWPORT {get {return HD_TO_VIEWPORT_INTERNAL.Value;}}
        
        /// <summary>
        /// Constructor for a RangeCorrector object
        /// </summary>
        /// <param name="orig">the original range in (x-start,y-start,x-stop,y-stop)</param>
        /// <param name="newRect">the range to map into in (x-start,y-start,x-stop,y-stop)</param>
        public RangeCorrector(Rect orig, Rect newRect) {
            this.originalRange = orig;
            this.newRange = newRect;
        }
        



        /// <summary>
        /// Corrects the vector to the supplies values
        /// </summary>
        /// <param name="value">the input 2-d coordinate to map into new range</param>
        /// <returns>the normalised/corrected point</returns>
        public Vector2 correctVector(Vector2 value) {
            
            return Rect.NormalizedToPoint(this.newRange,Rect.PointToNormalized(this.originalRange,value));
        }

    }
}
