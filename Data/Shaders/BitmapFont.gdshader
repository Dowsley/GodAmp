shader_type canvas_item;

void fragment() {
    // Sample background color from position (150,4) in 155x18 image
    // Same position Winamp uses: GetPixel(bmDC, 150, 4)
    vec4 bg = texture(TEXTURE, vec2(150.0/155.0, 4.0/18.0));
    vec4 c = texture(TEXTURE, UV);

    if (distance(c.rgb, bg.rgb) < 0.01) {
        c.a = 0.0;
    }
    COLOR = c;
}