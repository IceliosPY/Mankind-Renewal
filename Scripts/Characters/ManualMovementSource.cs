using Godot;

namespace MankindRenewal.Characters;

public partial class ManualMovementSource : Node
{
    public Vector2 GetInputVector() => Input.GetVector("move_left", "move_right", "move_forward", "move_backward");

    public bool HasInput() => !GetInputVector().IsZeroApprox();

    public Vector3 GetWorldDirection(Basis cameraBasis)
    {
        Vector2 input = GetInputVector();
        Vector3 cameraForward = -cameraBasis.Z;
        Vector3 cameraRight = cameraBasis.X;
        cameraForward.Y = 0.0f;
        cameraRight.Y = 0.0f;

        return (cameraRight.Normalized() * input.X + cameraForward.Normalized() * -input.Y).Normalized();
    }
}

