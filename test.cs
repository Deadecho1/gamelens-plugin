using Godot;
using System;
using GameLensAnalytics.Runtime;

public partial class test : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AnalyticsAutoload.Instance.Snap("test_ready");
    	AnalyticsAutoload.Instance.SetSensitiveScene("res://Scenes/Login.tscn", true);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
