shader_type canvas_item;
render_mode unshaded, blend_mul;

uniform sampler2D region_mask : filter_nearest, repeat_disable;

void fragment() {
    COLOR = vec4(texture(region_mask, UV).r);
}
