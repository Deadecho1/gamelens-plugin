extends Node
class_name GameLensApi

var _core: Node

func _ready():
    # Spawn C# core once
    _core = preload("res://addons/gamelens/Runtime/AnalyticsAutoload.cs").new()
    add_child(_core)

# Public API 
func enable() -> void:
    _core.Enable()

func disable() -> void:
    _core.Disable()

func set_sensitive_scene(scene_path: String, is_sensitive: bool) -> void:
    _core.SetSensitiveScene(scene_path, is_sensitive)

func snap(reason: String, meta: Dictionary = {}) -> void:
    _core.Snap(reason, meta)
