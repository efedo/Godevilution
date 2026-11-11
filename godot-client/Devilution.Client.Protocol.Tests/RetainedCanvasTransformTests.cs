using Godot;
using Xunit;

namespace Devilution.Client
{
    public partial class WorldPresentation
    {
        // The linked production guard runs without loading the Godot native runtime.
        private sealed partial class RetainedSceneCanvas
        {
            internal int Assignments;
            private Vector2 position;
            internal Vector2 Position { get => position; set { position = value; Assignments++; } }
        }

        internal static (int Assignments, long Allocated, Vector2 Position) ExerciseCanvasTransforms()
        {
            var canvas = new RetainedSceneCanvas();
            canvas.SetRetainedPosition(new(0, 0));
            Action<int> idleFrame = _ => canvas.SetRetainedPosition(new(0, 0));
            Protocol.Tests.HotPathAllocation.Warm(idleFrame);
            var measured = Protocol.Tests.HotPathAllocation.Measure(idleFrame, 4096);
            canvas.SetRetainedPosition(new(12.5f, -4));
            canvas.SetRetainedPosition(new(12.5f, -4));
            canvas.SetRetainedPosition(new(0, 0));
            return (canvas.Assignments, measured.Bytes, canvas.Position);
        }
    }
}

namespace Devilution.Client.Protocol.Tests
{
    [Collection(AllocationMeasurementCollection.Name)]
    public sealed class RetainedCanvasTransformTests
    {
        [Fact]
        public void IdleFramesSkipNativeTransformNotificationsAndMovingFramesKeepExactPositions()
        {
            var result = WorldPresentation.ExerciseCanvasTransforms();
            Assert.Equal(3, result.Assignments);
            Assert.Equal(0, result.Allocated);
            Assert.Equal(new Vector2(0, 0), result.Position);
        }
    }
}
