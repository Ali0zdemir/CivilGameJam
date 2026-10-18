using System;

namespace CivilGameJam.Disasters
{
    /// <summary>Finishes city placement before any disaster captures building bounds or rest poses.</summary>
    public static class CityLayoutEvents
    {
        public static event Action PreparingDisaster;
        public static void PrepareForDisaster() => PreparingDisaster?.Invoke();
    }
}
