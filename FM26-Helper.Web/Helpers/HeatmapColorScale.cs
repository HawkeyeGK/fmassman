using System;
using System.Collections.Generic;
using System.Linq;

namespace FM26_Helper.Web.Helpers
{
    public class HeatmapColorScale
    {
        private readonly double _min;
        private readonly double _max;
        private readonly double _range;

        public HeatmapColorScale(IEnumerable<double> scores)
        {
            var scoreList = scores.ToList();
            if (scoreList.Count == 0)
            {
                _min = 0;
                _max = 100;
                _range = 100;
                return;
            }

            _min = scoreList.Min();
            _max = scoreList.Max();
            _range = _max - _min;

            // Guard: If the range is too small, artificially lower the Min
            // to prevent "Rainbow Noise" (e.g., 69% Red vs 71% Green).
            // We want a minimum spread of ~15 points for a full gradient.
            if (_range < 15)
            {
                // Lower the min so that (Max - newMin) = 15
                // effectively making the current Min somewhere in the middle-top of the new range
                // or simply extending the bottom of the scale.
                _min = _max - 15;
                _range = 15;
            }
        }

        public HeatmapColorScale(double min, double max)
        {
            _min = min;
            _max = max;
            _range = _max - _min;

            if (_range < 15)
            {
                _min = _max - 15;
                _range = 15;
            }
        }

        public string GetColorStyle(double score)
        {
            // Normalize score to 0.0 - 1.0
            // If score < _min, clamp to 0.
            if (score <= _min) score = _min;
            if (score >= _max) score = _max;

            double normalized = (score - _min) / _range;

            // Cubic Curve: y = x^3
            // This pushes the "center" of the gradient even further down towards Red/Orange.
            normalized = normalized * normalized * normalized;

            // Calculate Hue: 0 (Red) -> 120 (Green)
            double hue = normalized * 120.0;

            // Saturation 70%, Lightness 35-40% to insure white text readability
            // Let's stick to 35% as requested.
            return $"background-color: hsl({hue:F0}, 70%, 35%); color: white;";
        }
    }
}
