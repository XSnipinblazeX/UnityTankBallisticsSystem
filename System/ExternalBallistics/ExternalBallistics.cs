using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ExternalBallistics
{
    public static class G7
    {
        public static SortedList<float, float> dragMultipliers = new SortedList<float, float>()
        {
            { 5.00f, 1.05f },
            { 4.50f, 1.04f },
            { 4.00f, 1.03f },
            { 3.50f, 1.02f },
            { 3.00f, 1.01f },
            { 2.50f, 1.00f },
            { 2.00f, 1.00f },
            { 1.50f, 1.05f },
            { 1.20f, 1.20f },
            { 1.00f, 1.45f }, // Transonic region
            { 0.90f, 1.50f },
            { 0.80f, 1.40f }, // Subsonic region
            { 0.70f, 1.30f },
            { 0.60f, 1.20f },
            { 0.50f, 1.10f },
        };
        public static float GetMultiplier(float machNumber)
        {
            // Handle edge cases where the Mach number is outside the defined range.
            if (machNumber >= dragMultipliers.Keys[dragMultipliers.Count - 1])
            {
                return dragMultipliers.Values[dragMultipliers.Count - 1];
            }
            if (machNumber <= dragMultipliers.Keys[0])
            {
                return dragMultipliers.Values[0];
            }

            // Find the two closest Mach numbers in the table.
            int upperIndex = 0;
            for (int i = 0; i < dragMultipliers.Count; i++)
            {
                if (dragMultipliers.Keys[i] > machNumber)
                {
                    upperIndex = i;
                    break;
                }
            }

            int lowerIndex = upperIndex - 1;

            // Get the values for interpolation.
            float lowerMach = dragMultipliers.Keys[lowerIndex];
            float upperMach = dragMultipliers.Keys[upperIndex];
            float lowerMultiplier = dragMultipliers.Values[lowerIndex];
            float upperMultiplier = dragMultipliers.Values[upperIndex];

            // Perform linear interpolation.
            float t = (machNumber - lowerMach) / (upperMach - lowerMach);
            return Mathf.Lerp(lowerMultiplier, upperMultiplier, t);
        }        
    }
    public static class G1
    {
        public static SortedList<float, float> dragMultipliers = new SortedList<float, float>()
        {
            { 5.00f, 1.05f },
            { 4.00f, 1.02f },
            { 3.00f, 1.00f },
            { 2.00f, 1.00f },
            { 1.50f, 1.08f },
            { 1.20f, 1.25f },
            { 1.00f, 1.50f },
            { 0.90f, 1.55f },
            { 0.80f, 1.45f },
            { 0.70f, 1.35f },
            { 0.60f, 1.25f },
            { 0.50f, 1.15f },
        };
        public static float GetMultiplier(float machNumber)
        {
            // Handle edge cases where the Mach number is outside the defined range.
            if (machNumber >= dragMultipliers.Keys[dragMultipliers.Count - 1])
            {
                return dragMultipliers.Values[dragMultipliers.Count - 1];
            }
            if (machNumber <= dragMultipliers.Keys[0])
            {
                return dragMultipliers.Values[0];
            }

            // Find the two closest Mach numbers in the table.
            int upperIndex = 0;
            for (int i = 0; i < dragMultipliers.Count; i++)
            {
                if (dragMultipliers.Keys[i] > machNumber)
                {
                    upperIndex = i;
                    break;
                }
            }

            int lowerIndex = upperIndex - 1;

            // Get the values for interpolation.
            float lowerMach = dragMultipliers.Keys[lowerIndex];
            float upperMach = dragMultipliers.Keys[upperIndex];
            float lowerMultiplier = dragMultipliers.Values[lowerIndex];
            float upperMultiplier = dragMultipliers.Values[upperIndex];

            // Perform linear interpolation.
            float t = (machNumber - lowerMach) / (upperMach - lowerMach);
            return Mathf.Lerp(lowerMultiplier, upperMultiplier, t);
        }

    }


        public static class OTHER
        {
            private static SortedList<float, float> DragMultipliers = new SortedList<float, float>()
            {
                { 5.00f, 1.10f },
                { 4.00f, 1.05f },
                { 3.00f, 1.00f },
                { 2.00f, 1.00f },
                { 1.50f, 1.10f },
                { 1.20f, 1.30f },
                { 1.00f, 1.50f }, // Transonic region peak drag
                { 0.90f, 1.60f },
                { 0.80f, 1.55f },
                { 0.70f, 1.45f },
                { 0.60f, 1.35f },
                { 0.50f, 1.25f }
                };
        
        public static float GetMultiplier(float machNumber)
        {
            // Handle edge cases where the Mach number is outside the defined range.
            if (machNumber >= DragMultipliers.Keys[DragMultipliers.Count - 1])
            {
                return DragMultipliers.Values[DragMultipliers.Count - 1];
            }
            if (machNumber <= DragMultipliers.Keys[0])
            {
                return DragMultipliers.Values[0];
            }

            // Find the two closest Mach numbers in the table.
            int upperIndex = 0;
            for (int i = 0; i < DragMultipliers.Count; i++)
            {
                if (DragMultipliers.Keys[i] > machNumber)
                {
                    upperIndex = i;
                    break;
                }
            }

            int lowerIndex = upperIndex - 1;

            // Get the values for interpolation.
            float lowerMach = DragMultipliers.Keys[lowerIndex];
            float upperMach = DragMultipliers.Keys[upperIndex];
            float lowerMultiplier = DragMultipliers.Values[lowerIndex];
            float upperMultiplier = DragMultipliers.Values[upperIndex];

            // Perform linear interpolation.
            float t = (machNumber - lowerMach) / (upperMach - lowerMach);
            return Mathf.Lerp(lowerMultiplier, upperMultiplier, t);
        }
    }

}
