using Godot;
using System;
using System.IO;
using GameLensAnalytics.Runtime;

public partial class GameLensSmokeTest : Node
{
    [Export] public float WaitSecondsAfterSnap = 0.8f;
    [Export] public float WaitSecondsAfterStoreEnqueue = 0.6f;

    public override async void _Ready()
    {
        GD.Print("[GameLensBasicSmokeTest] START");

        await Test_Capturer_ReturnsPngBytes();
        await Test_Orchestrator_SnapCreatesFiles();

        GD.Print("[GameLensBasicSmokeTest] DONE");
        GD.Print(ProjectSettings.GlobalizePath("user://gamelens"));
    }

    private async System.Threading.Tasks.Task Test_Capturer_ReturnsPngBytes()
    {
        GD.Print("[GameLensBasicSmokeTest] Test_Capturer_ReturnsPngBytes...");

        // Create a capturer standalone (so we can validate Capture() without the rest)
        var capturer = new GameLensCapturer();
        AddChild(capturer);

        // wait a frame so _Ready runs and SubViewport is created
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var reasons = new Godot.Collections.Array<SnapReason> { SnapReason.Manual };
        var pkt = capturer.Capture(reasons, Time.GetUnixTimeFromSystem());

        if (pkt == null)
        {
            GD.PushError("[GameLensBasicSmokeTest] Capture() returned null packet.");
            return;
        }

        if (pkt.ImageBytes == null || pkt.ImageBytes.Length == 0)
        {
            GD.PushError("[GameLensBasicSmokeTest] Capture() produced empty PNG buffer.");
            return;
        }

        if (pkt.ImageExt != ".png")
        {
            GD.PushError($"[GameLensBasicSmokeTest] Expected ImageExt '.png' but got '{pkt.ImageExt}'.");
            return;
        }

        GD.Print($"[GameLensBasicSmokeTest] OK: png bytes={pkt.ImageBytes.Length}, id={pkt.CaptureId}");

        capturer.QueueFree();
        GD.Print("[GameLensBasicSmokeTest] Test_Capturer_ReturnsPngBytes PASSED.");
    }

    private async System.Threading.Tasks.Task Test_Orchestrator_SnapCreatesFiles()
    {
        GD.Print("[GameLensBasicSmokeTest] Test_Orchestrator_SnapCreatesFiles...");

        // Your autoload/orchestrator names itself "GameLens" in _EnterTree
        var orchestrator = GetNodeOrNull<GameLensOrchestrator>("/root/GameLens/Orchestrator");
        if (orchestrator == null)
        {
            GD.PushError("[GameLensSmokeTest] Could not find /root/GameLens/Orchestrator. Is the autoload enabled and named 'GameLens'?");
            return;
        }

        // Count existing capture files
        var capturesRootGlobal = ProjectSettings.GlobalizePath("user://gamelens/captures");
        int beforePng = CountFilesSafe(capturesRootGlobal, "*.png", true);
        int beforeJson = CountFilesSafe(capturesRootGlobal, "*.json", true);

        // Same frame: multiple calls, should coalesce to 1 capture
        orchestrator.Snap(SnapReason.Manual);
        orchestrator.Snap(SnapReason.Manual);
        orchestrator.Snap(SnapReason.AutoTimer);

        // Wait for deferred finalize + storage thread to write
        await ToSignal(GetTree().CreateTimer(WaitSecondsAfterSnap), "timeout");

        int afterPng = CountFilesSafe(capturesRootGlobal, "*.png", true);
        int afterJson = CountFilesSafe(capturesRootGlobal, "*.json", true);

        int deltaPng = afterPng - beforePng;
        int deltaJson = afterJson - beforeJson;

        GD.Print($"[GameLensBasicSmokeTest] Captures delta: png={deltaPng}, json={deltaJson}");

        if (deltaPng <= 0 || deltaJson <= 0)
        {
            GD.PushError("[GameLensBasicSmokeTest] Expected at least 1 new PNG+JSON after Snap(), but did not see it.");
            return;
        }

        // Usually 1/1 unless something else is snapping in the same run
        if (deltaPng != 1 || deltaJson != 1)
        {
            GD.Print("[GameLensBasicSmokeTest] NOTE: Delta != 1. If other snaps are happening, that’s normal. If not, coalescing may not be working.");
        }

        // Sanity: newest PNG exists and is non-empty
        var newestPng = FindNewestFileSafe(capturesRootGlobal, "*.png", true);
        if (!string.IsNullOrEmpty(newestPng))
        {
            var fi = new FileInfo(newestPng);
            if (fi.Length <= 0)
            {
                GD.PushError("[GameLensBasicSmokeTest] Newest PNG file exists but is empty.");
                return;
            }
            GD.Print($"[GameLensBasicSmokeTest] Newest PNG: {Path.GetFileName(newestPng)} ({fi.Length} bytes)");
        }

        // Your capturer currently writes "{}" as JSON, so we only assert it's present & readable
        var newestJson = FindNewestFileSafe(capturesRootGlobal, "*.json", true);
        if (!string.IsNullOrEmpty(newestJson))
        {
            try
            {
                string txt = File.ReadAllText(newestJson);
                if (string.IsNullOrWhiteSpace(txt))
                {
                    GD.PushError("[GameLensBasicSmokeTest] Newest JSON exists but is empty.");
                    return;
                }

                // Should parse as JSON at least
                var v = Json.ParseString(txt);
                if (v.VariantType == Variant.Type.Nil)
                {
                    GD.PushError("[GameLensBasicSmokeTest] Newest JSON did not parse.");
                    return;
                }

                GD.Print($"[GameLensBasicSmokeTest] Newest JSON: {Path.GetFileName(newestJson)} ok");
            }
            catch (Exception e)
            {
                GD.PushError($"[GameLensBasicSmokeTest] Failed reading/parsing newest JSON: {e.Message}");
                return;
            }
        }

        GD.Print("[GameLensBasicSmokeTest] Test_Orchestrator_SnapCreatesFiles PASSED (basic).");
    }

    private static int CountFilesSafe(string root, string pattern, bool recursive)
    {
        try
        {
            if (!Directory.Exists(root)) return 0;
            return Directory.GetFiles(root, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Length;
        }
        catch { return 0; }
    }

    private static string FindNewestFileSafe(string root, string pattern, bool recursive)
    {
        try
        {
            if (!Directory.Exists(root)) return "";
            var files = Directory.GetFiles(root, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            if (files.Length == 0) return "";

            string newest = files[0];
            var newestTime = File.GetLastWriteTimeUtc(newest);

            for (int i = 1; i < files.Length; i++)
            {
                var t = File.GetLastWriteTimeUtc(files[i]);
                if (t > newestTime)
                {
                    newest = files[i];
                    newestTime = t;
                }
            }
            return newest;
        }
        catch { return ""; }
    }
}
