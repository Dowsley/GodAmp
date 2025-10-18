shader_type canvas_item;

uniform vec4 line_color : source_color;
uniform float glow_intensity : hint_range(0.0, 2.0) = 1.0;
uniform float time_offset : hint_range(0.0, 1000.0);
uniform float rotation_speed : hint_range(0.1, 5.0) = 1.0;
uniform float rotation_direction : hint_range(-1.0, 1.0) = 1.0;
uniform float tunnel_depth : hint_range(0.1, 2.0) = 1.0;
uniform float distortion : hint_range(0.1, 2.0) = 1.0;
uniform sampler2D previous_frame; // Texture from the other viewport
uniform float decay : hint_range(0.0, 1.0) = 0.85; // How much of the previous frame persists
uniform float color_decay : hint_range(0.0, 1.0) = 0.92;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture;

void fragment() {
    // Get the warped coordinates for both current and previous frame
    vec2 uv = SCREEN_UV - 0.5;
    float radius = length(uv);
    float angle = atan(uv.y, uv.x);

    // Add rotation and tunnel effect with direction control
    angle += time_offset * rotation_speed * rotation_direction * (1.0 - radius);

    // Make tunnel depth respond to rotation direction with very subtle variation
    float dynamic_depth = tunnel_depth * (1.0 + abs(rotation_direction) * 0.05);
    radius = pow(radius, dynamic_depth);

    vec2 warped_uv = vec2(
        radius * cos(angle),
        radius * sin(angle)
    ) + 0.5;

    // Sample current and previous frame
    vec4 current = texture(SCREEN_TEXTURE, warped_uv);
    vec4 previous = texture(previous_frame, warped_uv);

    // Apply proper strong decay to previous frame
    previous.rgb *= decay;

    // Simple additive blending - this will definitely show trails
    COLOR = max(current, previous);
}