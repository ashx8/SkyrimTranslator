//窗口吸附
using Godot;
using System;

namespace SkyrimModTranslator.Common
{
    public class WinDockHelper
    {
        public const int DEFAULT_SNAP_DISTANCE = 50;

        public static Vector2I CalculateDockPosition(Window parentWindow)
        {
            if (parentWindow == null) return Vector2I.Zero;
            return parentWindow.Position + new Vector2I(parentWindow.Size.X + 2, 0);
        }

        public static bool ShouldSnap(Vector2I currentPosition, Vector2I targetPosition, int snapDistance = DEFAULT_SNAP_DISTANCE)
        {
            return currentPosition.DistanceTo(targetPosition) < snapDistance;
        }

        public static Vector2I GetSnappedPosition(Vector2I currentPosition, Vector2I targetPosition, int snapDistance = DEFAULT_SNAP_DISTANCE)
        {
            if (ShouldSnap(currentPosition, targetPosition, snapDistance))
            {
                return targetPosition;
            }
            return currentPosition;
        }
    }
}
