using Godot;

namespace Devilution.Client;

public partial class WorldPresentation
{
    private sealed partial class RetainedSceneCanvas
    {
        private Vector2 retainedPosition;
        private bool positionInitialized;

        internal void SetRetainedPosition(Vector2 value)
        {
            // Node2D.set_position notifies the whole canvas subtree even when the value is unchanged.
            if (positionInitialized && retainedPosition == value) return;
            retainedPosition = value;
            positionInitialized = true;
            Position = value;
        }
    }
}
