shader_type canvas_item;

void vertex() {
	VERTEX += vec2(200.0 * cos(VERTEX.y + TIME * 10.0), 20.0 * sin(VERTEX.x + TIME * 10.0));
}

void fragment() {
	COLOR = vec4(COLOR.rgb, mod(-TIME, 5) / 5.0 - 0.5);
}