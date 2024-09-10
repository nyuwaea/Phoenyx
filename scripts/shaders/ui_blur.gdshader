shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float brightness = 1.25;

void fragment() {
	COLOR.rgb = textureLod(screen_texture, SCREEN_UV, 2.0).rgb * brightness;
}