shader_type canvas_item;

uniform float shift : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	COLOR = mix(COLOR, COLOR.bgra, shift);
}