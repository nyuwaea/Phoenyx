shader_type canvas_item;

void fragment() {
	float alpha = (0.5f - abs(UV.x - 0.5f)) * 3.0f;

	COLOR = vec4(COLOR.rgb, alpha / 1.5f);
}