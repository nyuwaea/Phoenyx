func Set(property: String, value):
	match property:
		"app_id": DiscordRPC.app_id = value
		"details": DiscordRPC.details = value
		"state": DiscordRPC.state = value
		"large_image": DiscordRPC.large_image = value
		"large_image_text": DiscordRPC.large_image_text = value
		"small_image": DiscordRPC.small_image = value
		"small_image_text": DiscordRPC.small_image_text = value
		"start_timestamp": DiscordRPC.start_timestamp = value
		"end_timestmap": DiscordRPC.end_timestamp = value

	Refresh()
	
func Refresh():
	DiscordRPC.refresh()
