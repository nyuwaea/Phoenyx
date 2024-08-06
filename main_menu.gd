extends Control

func _on_button_pressed():
	get_node("FileDialog").visible = true

func _on_file_dialog_file_selected(path):
	var file = FileAccess.open(path, FileAccess.READ)
	var data = file.get_line()
	
	
