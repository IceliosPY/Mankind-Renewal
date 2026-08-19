using Godot;

namespace MankindRenewal.Camera;

public partial class TacticalCameraController : Node3D
{
    [Export]
    public NodePath TargetPath { get; set; } = new("../Player");

    [ExportGroup("Follow")]
    [Export(PropertyHint.Range, "0.1,30.0,0.1")]
    public float FollowSmoothing { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0.0,5.0,0.1")]
    public float TargetHeight { get; set; } = 0.8f;

    [ExportGroup("Rotation")]
    [Export(PropertyHint.Range, "0.01,1.0,0.01")]
    public float RotationSensitivity { get; set; } = 0.18f;

    [Export(PropertyHint.Range, "-89.0,-1.0,1.0")]
    public float MinimumPitchDegrees { get; set; } = -75.0f;

    [Export(PropertyHint.Range, "-89.0,-1.0,1.0")]
    public float MaximumPitchDegrees { get; set; } = -30.0f;

    [ExportGroup("Zoom")]
    [Export(PropertyHint.Range, "1.0,30.0,0.5")]
    public float MinimumZoom { get; set; } = 6.0f;

    [Export(PropertyHint.Range, "1.0,40.0,0.5")]
    public float MaximumZoom { get; set; } = 18.0f;

    [Export(PropertyHint.Range, "0.1,5.0,0.1")]
    public float ZoomStep { get; set; } = 1.5f;

    [ExportGroup("Pan")]
    [Export(PropertyHint.Range, "0.001,0.1,0.001")]
    public float PanSensitivity { get; set; } = 0.012f;

    private Node3D _target = null!;
    private Node3D _pitchPivot = null!;
    private SpringArm3D _springArm = null!;
    private Vector3 _panOffset;
    private float _pitchRadians;

    public override void _Ready()
    {
        _target = GetNode<Node3D>(TargetPath);
        _pitchPivot = GetNode<Node3D>("PitchPivot");
        _springArm = GetNode<SpringArm3D>("PitchPivot/SpringArm3D");
        _pitchRadians = _pitchPivot.Rotation.X;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Recenter(true);
    }

    public override void _Process(double delta)
    {
        Vector3 targetPosition = _target.GlobalPosition + Vector3.Up * TargetHeight + _panOffset;
        float blend = 1.0f - Mathf.Exp(-FollowSmoothing * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(targetPosition, blend);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseMotion mouseMotion)
        {
            if (Input.IsActionPressed("camera_rotate"))
                ApplyRotation(mouseMotion.Relative);
            else if (Input.IsActionPressed("camera_pan"))
                ApplyPan(mouseMotion.Relative);
        }

        if (inputEvent.IsActionPressed("camera_zoom_in"))
            ApplyZoom(-ZoomStep);
        else if (inputEvent.IsActionPressed("camera_zoom_out"))
            ApplyZoom(ZoomStep);

        if (inputEvent.IsActionPressed("camera_recenter"))
            Recenter();
    }

    public void ApplyRotation(Vector2 mouseDelta)
    {
        RotateY(Mathf.DegToRad(-mouseDelta.X * RotationSensitivity));
        _pitchRadians += Mathf.DegToRad(-mouseDelta.Y * RotationSensitivity);
        _pitchRadians = Mathf.Clamp(_pitchRadians, Mathf.DegToRad(MinimumPitchDegrees), Mathf.DegToRad(MaximumPitchDegrees));
        _pitchPivot.Rotation = new Vector3(_pitchRadians, 0.0f, 0.0f);
    }

    public void ApplyZoom(float amount)
    {
        _springArm.SpringLength = Mathf.Clamp(_springArm.SpringLength + amount, MinimumZoom, MaximumZoom);
    }

    public void ApplyPan(Vector2 mouseDelta)
    {
        Vector3 cameraRight = GlobalBasis.X;
        Vector3 cameraForward = -GlobalBasis.Z;
        cameraRight.Y = 0.0f;
        cameraForward.Y = 0.0f;

        float zoomScale = _springArm.SpringLength * PanSensitivity;
        _panOffset += (-cameraRight.Normalized() * mouseDelta.X + cameraForward.Normalized() * mouseDelta.Y) * zoomScale;
    }

    public void Recenter(bool immediate = false)
    {
        _panOffset = Vector3.Zero;
        if (immediate && IsInstanceValid(_target))
            GlobalPosition = _target.GlobalPosition + Vector3.Up * TargetHeight;
    }

    public float GetZoom() => _springArm.SpringLength;

    public Vector3 GetPanOffset() => _panOffset;
}

