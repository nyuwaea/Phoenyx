shader_type canvas_item;

void fragment() {
	if (mod(floor(UV.x * 64.0), 2.0) != 0.0)
	{
		COLOR = vec4(0.0, 0.0, 0.0, 0.0);
	}
}