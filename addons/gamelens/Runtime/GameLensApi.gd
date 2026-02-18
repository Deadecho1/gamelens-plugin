extends Node
class_name GameLensApi

var _core
const _csAutoloadPath = "res://addons/gamelens/Runtime/GameLensOrchestrator.cs"

func _ready():
	# Spawn C# core once
	_core = preload(_csAutoloadPath).new()
	add_child(_core)

# Public API 
func enable() -> void:
	_core.Enable()

func disable() -> void:
	_core.Disable()

func snap() -> void:
	_core.Snap()
