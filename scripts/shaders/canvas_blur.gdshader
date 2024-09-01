shader_type canvas_item;

void fragment() {
    vec3 col = texture(TEXTURE, UV).xyz * 0.16;
	
    col += texture(TEXTURE, UV + vec2(TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.075;
    col += texture(TEXTURE, UV + vec2(-TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.075;
    col += texture(TEXTURE, UV + vec2(2.0 * TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.06;
    col += texture(TEXTURE, UV + vec2(2.0 * -TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.06;
    col += texture(TEXTURE, UV + vec2(3.0 * TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.045;
    col += texture(TEXTURE, UV + vec2(3.0 * -TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.045;
    col += texture(TEXTURE, UV + vec2(4.0 * TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.025;
    col += texture(TEXTURE, UV + vec2(4.0 * -TEXTURE_PIXEL_SIZE.x, 0.0)).xyz * 0.025;
	col += texture(TEXTURE, UV + vec2(0.0, TEXTURE_PIXEL_SIZE.y)).xyz * 0.075;
    col += texture(TEXTURE, UV + vec2(0.0, -TEXTURE_PIXEL_SIZE.y)).xyz * 0.075;
    col += texture(TEXTURE, UV + vec2(0.0, 2.0 * TEXTURE_PIXEL_SIZE.y)).xyz * 0.06;
    col += texture(TEXTURE, UV + vec2(0.0, 2.0 * -TEXTURE_PIXEL_SIZE.y)).xyz * 0.06;
    col += texture(TEXTURE, UV + vec2(0.0, 3.0 * TEXTURE_PIXEL_SIZE.y)).xyz * 0.045;
    col += texture(TEXTURE, UV + vec2(0.0, 3.0 * -TEXTURE_PIXEL_SIZE.y)).xyz * 0.045;
    col += texture(TEXTURE, UV + vec2(0.0, 4.0 * TEXTURE_PIXEL_SIZE.y)).xyz * 0.025;
    col += texture(TEXTURE, UV + vec2(0.0, 4.0 * -TEXTURE_PIXEL_SIZE.y)).xyz * 0.025;
	
	COLOR = vec4(col, 0.15f);
}