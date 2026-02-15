extends Node

func _ready():
	print("[Test] GameLens public API dot-call test")

	# Disable then snap (should drop / no-op depending on your C# logic)
	GameLens.disable()
	GameLens.snap("should_drop_disabled")

	# Enable then snap
	GameLens.enable()
	GameLens.snap("should_queue_enabled")

	# Mark sensitive scene (only blocks if current scene path matches)
	GameLens.set_sensitive_scene("res://Scenes/Login.tscn", true)
	GameLens.snap("after_marking_sensitive_scene")

	# Snap with metadata
	GameLens.snap("with_meta", {"source": "gdscript", "hp": 10, "tags": ["test"]})

	print("[Test] done")
