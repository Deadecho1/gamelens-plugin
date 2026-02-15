#if TOOLS
using Godot;
using System;

[Tool]
public partial class GameLensEditorPlugin : EditorPlugin
{
    private const string AutoloadName = "GameLens";
    private const string AutoloadPath = "res://addons/gamelens/Runtime/GameLensApi.gd";

    public override void _EnterTree()
    {
        if (!FileAccess.FileExists(AutoloadPath))
        {
            GD.PushError($"[GameLens] Autoload file missing: {AutoloadPath}");
            return;
        }

        var key = $"autoload/{AutoloadName}";
        if (!ProjectSettings.HasSetting(key))
        {
            AddAutoloadSingleton(AutoloadName, AutoloadPath);
            ProjectSettings.Save();
            GD.Print($"[GameLens] Added autoload '{AutoloadName}' -> {AutoloadPath}");
        }
        else
        {
            GD.Print($"[GameLens] Autoload already exists: {AutoloadName}");
        }
    }

    public override void _ExitTree()
    {
        try
        {
            var key = $"autoload/{AutoloadName}";
            if (ProjectSettings.HasSetting(key))
            {
                RemoveAutoloadSingleton(AutoloadName);
                ProjectSettings.Save();
                GD.Print($"[GameLens] Removed autoload '{AutoloadName}'");
            }
        }
        catch (Exception e)
        {
            GD.PushError("[GameLens] EditorPlugin _ExitTree crashed: " + e);
        }
    }
}
#endif
