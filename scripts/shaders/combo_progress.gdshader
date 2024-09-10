shader_type canvas_item;

uniform float progress : hint_range(0, 1) = 0;
uniform vec4 colour : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	vec2 diff = UV - vec2(0.5, 0.5);
	float angle = atan(-diff.x, diff.y) * (180.0 / PI) + 180.0;
	
	if (angle <= progress * 360.0)
	{
		COLOR = vec4(colour.rgb, COLOR.a);
	}
}