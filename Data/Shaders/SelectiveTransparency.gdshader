shader_type canvas_item;

uniform vec4 transparent_color : source_color = vec4(1.0, 0.0, 1.0, 1.0); // Magenta by default
uniform float threshold : hint_range(0.0, 1.0) = 0.1;

void fragment() {
    vec4 color = texture(TEXTURE, UV);
    
    // Calculate color difference
    float diff = distance(color.rgb, transparent_color.rgb);
    
    // Make transparent if color matches within threshold
    if (diff < threshold) {
        COLOR.a = 0.0;
    } else {
        COLOR = color;
    }
}