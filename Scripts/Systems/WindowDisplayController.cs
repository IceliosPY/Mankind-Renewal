using Godot;

namespace MankindRenewal.Systems;

public partial class WindowDisplayController : Node
{
    [Export] public bool UseExclusiveFullscreen { get; set; }

    private Vector2I _windowedSize;
    private Vector2I _windowedPosition;

    public override void _Ready()
    {
        Window window = GetWindow();
        _windowedSize = window.Size;
        _windowedPosition = window.Position;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo || keyEvent.Keycode != Key.F11)
            return;
        ToggleFullscreen();
        GetViewport().SetInputAsHandled();
    }

    public void ToggleFullscreen()
    {
        Window window = GetWindow();
        if (IsFullscreen(window.Mode))
        {
            window.Mode = Window.ModeEnum.Windowed;
            Callable.From(RestoreWindowedBounds).CallDeferred();
            return;
        }

        _windowedSize = window.Size;
        _windowedPosition = window.Position;
        window.Mode = UseExclusiveFullscreen
            ? Window.ModeEnum.ExclusiveFullscreen
            : Window.ModeEnum.Fullscreen;
    }

    public bool GetIsFullscreen() => IsFullscreen(GetWindow().Mode);

    private void RestoreWindowedBounds()
    {
        Window window = GetWindow();
        window.Size = _windowedSize;
        window.Position = _windowedPosition;
    }

    private static bool IsFullscreen(Window.ModeEnum mode)
    {
        return mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen;
    }
}
